using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace NxOpenFile.App
{
    /// <summary>NX 브리지 DLL의 응답 (내부용)</summary>
    internal sealed class BridgeResult
    {
        public BridgeResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public bool Success { get; private set; }
        public string Message { get; private set; }
    }

    /// <summary>NX(브리지)가 요청을 처리하지 못하고 오류를 돌려준 경우</summary>
    public sealed class BridgeException : Exception
    {
        public BridgeException(string message) : base(message) { }
    }

    /// <summary>실행 중인 NX 안의 브리지 DLL(Named Pipe 서버)에 요청을 보냅니다.</summary>
    public static class NxBridgeClient
    {
        /// <summary>NX(브리지 DLL)에 연결할 때 기다리는 최대 시간</summary>
        public const int ConnectTimeoutMs = 3000;

        /// <summary>
        /// NX 안의 [NxCommand("command")] 함수를 실행하고 그 반환 문자열을 받습니다.
        /// 함수는 NX 메인 스레드에서 현재 Session과 함께 실행됩니다 (NxCommands.cs 참고).
        /// </summary>
        /// <param name="command">NxCommands.cs의 [NxCommand] 이름 (예: NxCommandNames.GetComponents)</param>
        /// <param name="argument">함수의 arg로 전달할 문자열 (여러 줄 가능)</param>
        /// <exception cref="BridgeException">NX 함수가 예외를 던졌거나 명령이 등록되지 않은 경우</exception>
        /// <exception cref="TimeoutException">NX(브리지 DLL)에 연결하지 못한 경우</exception>
        public static string Call(string command, string argument = "")
        {
            if (string.IsNullOrEmpty(command) || command.IndexOfAny(new[] { BridgeProtocol.Separator, '\r', '\n' }) >= 0)
                throw new ArgumentException("명령 이름이 비어 있거나 탭/줄바꿈이 들어 있습니다.", "command");

            BridgeResult result = Send(command + BridgeProtocol.Separator + BridgeProtocol.Escape(argument));
            if (!result.Success)
                throw new BridgeException(result.Message);
            return result.Message;
        }

        /// <summary>
        /// 현재 NX 디스플레이 파트의 컴포넌트 목록 (Call + ComponentRow.Parse 예시).
        /// 첫 행(Level 0)은 디스플레이 파트 자신입니다.
        /// </summary>
        public static List<ComponentRow> GetComponents()
        {
            return ComponentRow.Parse(Call(NxCommandNames.GetComponents));
        }

        /// <summary>브리지 DLL이 로드되어 있고 응답하는지 확인합니다.</summary>
        public static bool IsNxReady()
        {
            if (!IsBridgeListening()) return false;
            try
            {
                return Send(BridgeProtocol.CmdPing).Success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// NX 안의 브리지가 파이프를 열어 두었는지 예외 없이 확인합니다.
        /// (없는 파이프에 Connect하면 TimeoutException이 나서 디버거가 멈추므로 먼저 확인)
        /// 브리지는 요청 하나를 처리할 때마다 파이프를 다시 만들기 때문에 잠깐 재시도합니다.
        /// </summary>
        public static bool IsBridgeListening()
        {
            for (int i = 0; i < 10; i++)
            {
                bool? exists = PipeExists(BridgeProtocol.PipeName);
                if (exists != false) return true; // 존재하거나, 확인 자체가 불가능하면 연결 시도에 맡김
                Thread.Sleep(100);
            }
            return false;
        }

        /// <returns>true=있음, false=없음, null=목록을 읽을 수 없음</returns>
        private static bool? PipeExists(string pipeName)
        {
            try
            {
                foreach (string pipe in Directory.GetFiles(@"\\.\pipe\"))
                {
                    if (string.Equals(Path.GetFileName(pipe), pipeName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }
            catch (Exception)
            {
                // 일부 시스템 파이프 이름에 경로에 쓸 수 없는 문자가 있으면 목록 조회가 실패할 수 있음
                return null;
            }
        }

        private static BridgeResult Send(string request)
        {
            using (NamedPipeClientStream pipe = new NamedPipeClientStream(".", BridgeProtocol.PipeName, PipeDirection.InOut))
            {
                // 브리지 DLL이 로드되지 않았으면 TimeoutException
                pipe.Connect(ConnectTimeoutMs);

                // StreamWriter는 Dispose 시 끊긴 파이프에 Flush하다 "파이프가 끊어졌습니다" 예외가 나므로
                // 요청은 바이트로 직접 씀
                byte[] bytes = BridgeProtocol.Encoding.GetBytes(request + "\n");
                pipe.Write(bytes, 0, bytes.Length);
                pipe.Flush();

                // 큰 어셈블리는 여는 데 오래 걸릴 수 있으므로 응답은 시간 제한 없이 대기
                string response;
                using (StreamReader reader = new StreamReader(pipe, BridgeProtocol.Encoding, false, 4096, true))
                {
                    response = reader.ReadLine();
                }

                if (response == null)
                    return new BridgeResult(false,
                        "NX가 응답 없이 연결을 끊었습니다.\n%TEMP%\\NxOpenFile.NxBridge.log 파일을 확인하세요.");

                int tab = response.IndexOf(BridgeProtocol.Separator);
                string status = tab < 0 ? response : response.Substring(0, tab);
                string message = tab < 0 ? string.Empty : BridgeProtocol.Unescape(response.Substring(tab + 1));
                return new BridgeResult(status == BridgeProtocol.RespOk, message);
            }
        }
    }
}
