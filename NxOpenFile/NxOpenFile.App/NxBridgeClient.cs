using System;
using System.IO;
using System.IO.Pipes;

namespace NxOpenFile.App
{
    /// <summary>NX 브리지 DLL의 응답</summary>
    public sealed class BridgeResult
    {
        public BridgeResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public bool Success { get; private set; }
        public string Message { get; private set; }
    }

    /// <summary>실행 중인 NX 안의 브리지 DLL(Named Pipe 서버)에 요청을 보냅니다.</summary>
    public static class NxBridgeClient
    {
        /// <summary>NX(브리지 DLL)에 연결할 때 기다리는 최대 시간</summary>
        public const int ConnectTimeoutMs = 3000;

        /// <summary>NX에 파일 열기를 요청합니다. 연결 실패 시 TimeoutException이 발생합니다.</summary>
        public static BridgeResult OpenFile(string path)
        {
            return Send(BridgeProtocol.CmdOpen + BridgeProtocol.Separator + path);
        }

        /// <summary>브리지 DLL이 로드되어 있는지 확인합니다.</summary>
        public static bool IsNxReady()
        {
            try
            {
                return Send(BridgeProtocol.CmdPing).Success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static BridgeResult Send(string request)
        {
            using (NamedPipeClientStream pipe = new NamedPipeClientStream(".", BridgeProtocol.PipeName, PipeDirection.InOut))
            {
                // 브리지 DLL이 로드되지 않았으면 TimeoutException
                pipe.Connect(ConnectTimeoutMs);

                using (StreamWriter writer = new StreamWriter(pipe, BridgeProtocol.Encoding, 4096, true))
                using (StreamReader reader = new StreamReader(pipe, BridgeProtocol.Encoding, false, 4096, true))
                {
                    writer.AutoFlush = true;
                    writer.WriteLine(request);

                    // 큰 어셈블리는 여는 데 오래 걸릴 수 있으므로 응답은 시간 제한 없이 대기
                    string response = reader.ReadLine();
                    if (response == null)
                        return new BridgeResult(false, "NX로부터 응답이 없습니다.");

                    int tab = response.IndexOf(BridgeProtocol.Separator);
                    string status = tab < 0 ? response : response.Substring(0, tab);
                    string message = tab < 0 ? string.Empty : response.Substring(tab + 1);
                    return new BridgeResult(status == BridgeProtocol.RespOk, message);
                }
            }
        }
    }
}
