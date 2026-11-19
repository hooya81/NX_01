using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using NXOpen;

namespace NxOpenFile.NxBridge
{
    /// <summary>
    /// 실행 중인 NX 안에서 동작하는 브리지 DLL.
    /// 외부 EXE는 NX 세션에 직접 붙을 수 없으므로, 이 DLL이 NX 안에서 Named Pipe 서버를 열고
    /// EXE가 보낸 "파일 열기" 요청을 받아 현재 NX 세션에서 파일을 엽니다.
    ///
    /// 로드 방법
    ///   1) 수동: NX에서 파일 → 실행 → NX Open(Ctrl+U) → NxOpenFile.NxBridge.dll 선택 (NX 실행당 1회)
    ///   2) 자동: 이 DLL을 사용자 정의 디렉터리의 startup 폴더에 두면 NX 시작 시 Startup()이 호출됨
    /// </summary>
    public static class BridgeEntry
    {
        private static readonly object StartSync = new object();
        private static readonly object NxSync = new object();
        private static Thread _serverThread;

        /// <summary>Ctrl+U(NX Open 실행)로 로드했을 때의 진입점.</summary>
        public static void Main(string[] args)
        {
            bool started = Start();
            string message = started
                ? "NX 파일 열기 브리지가 시작되었습니다.\n이제 NxOpenFile.exe에서 파일을 열 수 있습니다.\n\n파이프: " + BridgeProtocol.PipeName
                : "NX 파일 열기 브리지가 이미 실행 중입니다.\n\n파이프: " + BridgeProtocol.PipeName;

            try
            {
                UI.GetUI().NXMessageBox.Show("NxOpenFile", NXMessageBox.DialogType.Information, message);
            }
            catch (Exception ex)
            {
                Log("메시지 표시 실패: " + ex.Message);
            }
        }

        /// <summary>startup 폴더에서 NX 시작 시 자동 로드될 때의 진입점 (UI 메시지 없음).</summary>
        public static int Startup()
        {
            Start();
            return 0;
        }

        /// <summary>
        /// NX 종료 시까지 DLL을 메모리에 유지해야 파이프 서버가 계속 살아 있습니다.
        /// Immediately로 바꾸면 Main() 직후 언로드되어 EXE가 연결할 수 없습니다.
        /// </summary>
        public static int GetUnloadOption(string arg)
        {
            return (int)Session.LibraryUnloadOption.AtTermination;
        }

        /// <summary>
        /// NX Open API는 NX 메인 스레드에서만 호출할 수 있습니다
        /// ("function may only be called from the main thread").
        /// Main/Startup(= NX 메인 스레드)에서 숨은 컨트롤을 만들어 두고,
        /// 파이프 스레드는 이 컨트롤의 Invoke로 작업을 메인 스레드에 넘깁니다.
        /// </summary>
        private static System.Windows.Forms.Control _mainThreadInvoker;

        private static bool Start()
        {
            lock (StartSync)
            {
                if (_serverThread != null) return false;

                // 반드시 NX 메인 스레드(Main/Startup 호출 스레드)에서 생성해야 함
                _mainThreadInvoker = new System.Windows.Forms.Control();
                IntPtr handle = _mainThreadInvoker.Handle; // 핸들을 지금(메인 스레드에서) 강제로 생성
                Log("메인 스레드 Invoker 생성. 스레드 ID=" + Thread.CurrentThread.ManagedThreadId + ", 핸들=" + handle);

                _serverThread = new Thread(ServerLoop)
                {
                    IsBackground = true,
                    Name = "NxOpenFile.NxBridge"
                };
                _serverThread.Start();
                Log("브리지 시작. 파이프=" + BridgeProtocol.PipeName);
                LogLoadedNxAssemblies();
                return true;
            }
        }

        /// <summary>
        /// 문제 진단용: 현재 NX에 로드된 NXOpen 어셈블리와 위치를 기록합니다.
        /// 같은 이름이 두 번 나오거나 NX 설치 폴더(NXBIN\managed) 밖에서 로드됐다면
        /// 빌드 출력 폴더에 NXOpen DLL이 복사된 것입니다.
        /// (NXOpen 형식을 쓰지 않으므로 NXOpen 로드가 실패해도 동작함)
        /// </summary>
        private static void LogLoadedNxAssemblies()
        {
            try
            {
                Log("브리지 DLL 위치: " + typeof(BridgeEntry).Assembly.Location);
                foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string name = assembly.GetName().Name;
                    if (!name.StartsWith("NXOpen", StringComparison.OrdinalIgnoreCase)) continue;

                    string location;
                    try { location = assembly.IsDynamic ? "(dynamic)" : assembly.Location; }
                    catch (Exception) { location = "?"; }
                    Log("  로드된 어셈블리: " + assembly.FullName + " @ " + location);
                }
            }
            catch (Exception ex)
            {
                Log("어셈블리 목록 기록 실패: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------
        // Named Pipe 서버
        // ------------------------------------------------------------------

        private static void ServerLoop()
        {
            while (true)
            {
                NamedPipeServerStream pipe;
                try
                {
                    pipe = CreatePipe();
                }
                catch (Exception ex)
                {
                    // 같은 이름의 파이프를 다른 NX가 이미 열어 둔 경우 등
                    Log("파이프 생성 실패: " + ex.Message);
                    Thread.Sleep(2000);
                    continue;
                }

                using (pipe)
                {
                    try
                    {
                        pipe.WaitForConnection();
                        HandleClient(pipe);
                    }
                    catch (IOException ex)
                    {
                        // 클라이언트가 중간에 끊은 경우: 바로 다음 연결을 받음
                        Log("클라이언트 연결 끊김: " + ex.Message);
                    }
                    catch (Exception ex)
                    {
                        Log("요청 처리 오류: " + ex);
                    }
                }
            }
        }

        private static NamedPipeServerStream CreatePipe()
        {
            // 현재 Windows 사용자만 접속 가능하도록 제한
            PipeSecurity security = new PipeSecurity();
            security.AddAccessRule(new PipeAccessRule(
                WindowsIdentity.GetCurrent().User,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));

            return new NamedPipeServerStream(
                BridgeProtocol.PipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.None,
                4096,
                4096,
                security);
        }

        private static void HandleClient(NamedPipeServerStream pipe)
        {
            // StreamWriter는 Dispose 시 끊긴 파이프에 Flush하다 예외가 나므로 바이트로 직접 씀
            string request;
            using (StreamReader reader = new StreamReader(pipe, BridgeProtocol.Encoding, false, 4096, true))
            {
                request = reader.ReadLine();
            }

            Log("요청: " + request);
            string response = ProcessRequest(request);
            Log("응답: " + response);

            byte[] bytes = BridgeProtocol.Encoding.GetBytes(response + "\n");
            pipe.Write(bytes, 0, bytes.Length);
            pipe.Flush();

            try { pipe.WaitForPipeDrain(); }
            catch (IOException) { /* 클라이언트가 응답을 읽고 먼저 끊은 경우 무시 */ }
        }

        private static string ProcessRequest(string request)
        {
            try
            {
                return ProcessRequestCore(request);
            }
            catch (Exception ex)
            {
                Log("요청 처리 중 예외: " + ex);
                return Error(ex.Message);
            }
        }

        private static string ProcessRequestCore(string request)
        {
            if (string.IsNullOrEmpty(request))
                return Error("빈 요청입니다.");

            int tab = request.IndexOf(BridgeProtocol.Separator);
            string command = tab < 0 ? request : request.Substring(0, tab);
            string argument = tab < 0 ? string.Empty : request.Substring(tab + 1);

            switch (command)
            {
                case BridgeProtocol.CmdPing:
                    return Ok("NX 연결됨");
                case BridgeProtocol.CmdOpen:
                    lock (NxSync)
                    {
                        return RunOnMainThread(() => OpenPart(argument));
                    }
                default:
                    return Error("알 수 없는 명령: " + command);
            }
        }

        // ------------------------------------------------------------------
        // NX 파일 열기
        // ------------------------------------------------------------------

        /// <summary>NX 메인 스레드에서 작업을 실행하고 끝날 때까지 기다립니다.</summary>
        private static string RunOnMainThread(Func<string> work)
        {
            System.Windows.Forms.Control invoker = _mainThreadInvoker;
            if (invoker == null || !invoker.IsHandleCreated)
                return Error("NX 메인 스레드에 연결되지 않았습니다. NX를 재시작한 뒤 브리지 DLL을 다시 실행하세요.");

            // NX가 다른 작업(대화상자 등) 중이면 끝날 때까지 기다렸다가 실행됨
            return (string)invoker.Invoke(work);
        }

        private static string OpenPart(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Error("파일 경로가 비어 있습니다.");
            if (!File.Exists(path))
                return Error("파일을 찾을 수 없습니다: " + path);

            Session theSession = Session.GetSession();
            PartLoadStatus loadStatus = null;

            try
            {
                // 이미 세션에 로드된 파일이면 다시 열지 않고 Display Part로 전환
                BasePart loaded = FindLoadedPart(theSession, path);
                if (loaded != null)
                {
                    theSession.Parts.SetActiveDisplay(
                        loaded,
                        DisplayPartOption.AllowAdditional,
                        PartDisplayPartWorkPartOption.UseLast,
                        out loadStatus);

                    Log("이미 로드된 파일 표시: " + path);
                    return Ok("이미 열려 있는 파일로 전환했습니다: " + loaded.Leaf + DescribeLoadStatus(loadStatus));
                }

                BasePart opened = theSession.Parts.OpenBaseDisplay(path, out loadStatus);
                Log("파일 열기: " + path);
                return Ok("열기 완료: " + opened.Leaf + DescribeLoadStatus(loadStatus));
            }
            catch (NXException ex)
            {
                Log("NX 오류: " + path + " / " + ex.Message);
                return Error("NX 오류: " + ex.Message);
            }
            catch (Exception ex)
            {
                Log("오류: " + path + " / " + ex);
                return Error(ex.Message);
            }
            finally
            {
                if (loadStatus != null) loadStatus.Dispose();
            }
        }

        private static BasePart FindLoadedPart(Session theSession, string path)
        {
            foreach (BasePart part in theSession.Parts)
            {
                string fullPath;
                try { fullPath = part.FullPath; }
                catch (NXException) { continue; }

                if (string.Equals(fullPath, path, StringComparison.OrdinalIgnoreCase))
                    return part;
            }
            return null;
        }

        private static string DescribeLoadStatus(PartLoadStatus status)
        {
            if (status == null) return string.Empty;
            int unloaded = status.NumberUnloadedParts;
            return unloaded > 0 ? " (로드되지 않은 하위 파트 " + unloaded + "개)" : string.Empty;
        }

        // ------------------------------------------------------------------
        // 유틸
        // ------------------------------------------------------------------

        private static string Ok(string message)
        {
            return BridgeProtocol.RespOk + BridgeProtocol.Separator + BridgeProtocol.ToSingleLine(message);
        }

        private static string Error(string message)
        {
            return BridgeProtocol.RespError + BridgeProtocol.Separator + BridgeProtocol.ToSingleLine(message);
        }

        /// <summary>%TEMP%\NxOpenFile.NxBridge.log 에 기록 (문제 확인용)</summary>
        private static void Log(string message)
        {
            try
            {
                string file = Path.Combine(Path.GetTempPath(), "NxOpenFile.NxBridge.log");
                File.AppendAllText(file, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
            }
            catch
            {
                // 로그 실패는 무시
            }
        }
    }
}
