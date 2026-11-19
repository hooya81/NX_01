using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using NXOpen;

namespace NxOpenFile.NxBridge
{
    /// <summary>
    /// 브리지 "배관" 코드. NX 작업 코드는 여기가 아니라 NxCommands.cs에 작성합니다.
    ///
    /// 하는 일
    ///   1) NX 진입점 (Main / Startup / GetUnloadOption)
    ///   2) Named Pipe 서버: EXE의 요청("명령이름\t인수")을 받음
    ///   3) [NxCommand]가 붙은 함수를 찾아 NX 메인 스레드에서 Session과 함께 실행하고 결과를 돌려줌
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

        /// <summary>
        /// NX Open API는 NX 메인 스레드에서만 호출할 수 있습니다
        /// ("function may only be called from the main thread").
        /// Main/Startup(= NX 메인 스레드)에서 숨은 컨트롤을 만들어 두고,
        /// 파이프 스레드는 이 컨트롤의 Invoke로 작업을 메인 스레드에 넘깁니다.
        /// </summary>
        private static System.Windows.Forms.Control _mainThreadInvoker;

        /// <summary>명령 이름 → NxCommands의 함수</summary>
        private static Dictionary<string, Func<Session, string, string>> _commands =
            new Dictionary<string, Func<Session, string, string>>(StringComparer.OrdinalIgnoreCase);

        // ------------------------------------------------------------------
        // NX 진입점
        // ------------------------------------------------------------------

        /// <summary>Ctrl+U(NX Open 실행)로 로드했을 때의 진입점.</summary>
        public static void Main(string[] args)
        {
            bool started = Start();
            string message = started
                ? "NX 브리지가 시작되었습니다.\n이제 NxOpenFile.exe에서 NX를 사용할 수 있습니다.\n\n" +
                  "등록된 명령: " + string.Join(", ", _commands.Keys) + "\n파이프: " + BridgeProtocol.PipeName
                : "NX 브리지가 이미 실행 중입니다.\n\n파이프: " + BridgeProtocol.PipeName;

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

        private static bool Start()
        {
            lock (StartSync)
            {
                if (_serverThread != null) return false;

                // 반드시 NX 메인 스레드(Main/Startup 호출 스레드)에서 생성해야 함
                _mainThreadInvoker = new System.Windows.Forms.Control();
                IntPtr handle = _mainThreadInvoker.Handle; // 핸들을 지금(메인 스레드에서) 강제로 생성
                Log("메인 스레드 Invoker 생성. 스레드 ID=" + Thread.CurrentThread.ManagedThreadId + ", 핸들=" + handle);

                _commands = LoadCommands();

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

        // ------------------------------------------------------------------
        // 명령 등록: 이 DLL 안의 [NxCommand] 함수를 모두 찾음
        // ------------------------------------------------------------------

        private static Dictionary<string, Func<Session, string, string>> LoadCommands()
        {
            Dictionary<string, Func<Session, string, string>> commands =
                new Dictionary<string, Func<Session, string, string>>(StringComparer.OrdinalIgnoreCase);

            foreach (Type type in typeof(BridgeEntry).Assembly.GetTypes())
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                {
                    NxCommandAttribute attribute =
                        (NxCommandAttribute)Attribute.GetCustomAttribute(method, typeof(NxCommandAttribute));
                    if (attribute == null) continue;

                    string name = string.IsNullOrEmpty(attribute.Name) ? method.Name : attribute.Name;
                    string where = type.Name + "." + method.Name;

                    if (name.IndexOfAny(new[] { BridgeProtocol.Separator, '\r', '\n' }) >= 0)
                    {
                        Log("명령 등록 실패 (이름에 탭/줄바꿈 불가): " + where);
                        continue;
                    }
                    if (commands.ContainsKey(name))
                    {
                        Log("명령 등록 실패 (이름 중복 '" + name + "'): " + where);
                        continue;
                    }

                    try
                    {
                        commands[name] = (Func<Session, string, string>)Delegate.CreateDelegate(
                            typeof(Func<Session, string, string>), method);
                        Log("명령 등록: " + name + " → " + where);
                    }
                    catch (ArgumentException)
                    {
                        Log("명령 등록 실패 (형식 불일치): " + where +
                            "  필요한 형식: public static string 함수이름(Session session, string arg)");
                    }
                }
            }
            return commands;
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

            Log("요청: " + Shorten(request));
            string response = ProcessRequest(request);
            Log("응답: " + Shorten(response));

            byte[] bytes = BridgeProtocol.Encoding.GetBytes(response + "\n");
            pipe.Write(bytes, 0, bytes.Length);
            pipe.Flush();

            try { pipe.WaitForPipeDrain(); }
            catch (IOException) { /* 클라이언트가 응답을 읽고 먼저 끊은 경우 무시 */ }
        }

        private static string ProcessRequest(string request)
        {
            if (string.IsNullOrEmpty(request))
                return Error("빈 요청입니다.");

            int tab = request.IndexOf(BridgeProtocol.Separator);
            string command = tab < 0 ? request : request.Substring(0, tab);
            string argument = tab < 0 ? string.Empty : BridgeProtocol.Unescape(request.Substring(tab + 1));

            if (command == BridgeProtocol.CmdPing)
                return Ok("NX 연결됨");

            Func<Session, string, string> handler;
            if (!_commands.TryGetValue(command, out handler))
                return Error("등록되지 않은 명령: " + command +
                             "  (NxCommands.cs에 [NxCommand(\"" + command + "\")] 함수가 있는지 확인하세요)");

            try
            {
                lock (NxSync)
                {
                    // NX 작업은 반드시 메인 스레드에서. Session도 메인 스레드에서 가져옴
                    string result = RunOnMainThread(() => handler(Session.GetSession(), argument));
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                // NxCommands 함수에서 던진 예외 → EXE에서는 BridgeException(메시지)으로 받음
                Log("명령 실패: " + command + " / " + ex);
                return Error(ex.Message);
            }
        }

        /// <summary>NX 메인 스레드에서 작업을 실행하고 끝날 때까지 기다립니다.</summary>
        private static string RunOnMainThread(Func<string> work)
        {
            System.Windows.Forms.Control invoker = _mainThreadInvoker;
            if (invoker == null || !invoker.IsHandleCreated)
                throw new InvalidOperationException("NX 메인 스레드에 연결되지 않았습니다. NX를 재시작한 뒤 브리지 DLL을 다시 실행하세요.");

            // NX가 다른 작업(대화상자 등) 중이면 끝날 때까지 기다렸다가 실행됨
            return (string)invoker.Invoke(work);
        }

        // ------------------------------------------------------------------
        // 유틸
        // ------------------------------------------------------------------

        private static string Ok(string message)
        {
            return BridgeProtocol.RespOk + BridgeProtocol.Separator + BridgeProtocol.Escape(message);
        }

        private static string Error(string message)
        {
            return BridgeProtocol.RespError + BridgeProtocol.Separator + BridgeProtocol.Escape(message);
        }

        private static string Shorten(string text)
        {
            if (text == null) return "(null)";
            return text.Length > 300 ? text.Substring(0, 300) + "... (" + text.Length + "자)" : text;
        }

        /// <summary>
        /// 문제 진단용: 현재 NX에 로드된 NXOpen 어셈블리와 위치를 기록합니다.
        /// 같은 이름이 두 번 나오거나 NX 설치 폴더(NXBIN\managed) 밖에서 로드됐다면
        /// 빌드 출력 폴더에 NXOpen DLL이 복사된 것입니다.
        /// </summary>
        private static void LogLoadedNxAssemblies()
        {
            try
            {
                Log("브리지 DLL 위치: " + typeof(BridgeEntry).Assembly.Location);
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
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

        /// <summary>%TEMP%\NxOpenFile.NxBridge.log 에 기록. NxCommands에서도 사용할 수 있습니다.</summary>
        internal static void Log(string message)
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
