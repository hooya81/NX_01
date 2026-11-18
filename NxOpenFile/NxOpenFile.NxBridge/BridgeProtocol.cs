using System;
using System.Diagnostics;
using System.Text;

namespace NxOpenFile
{
    /// <summary>
    /// EXE(클라이언트)와 NX 안의 브리지 DLL(서버)이 공유하는 통신 규약.
    /// 이 파일은 NxOpenFile.App 프로젝트에 "링크"로 포함되므로 NXOpen을 참조하면 안 됩니다.
    ///
    /// 요청 1회 = 파이프 연결 1회, 한 줄 요청 / 한 줄 응답 (UTF-8, BOM 없음)
    ///   요청: "PING"            응답: "OK\t&lt;메시지&gt;"
    ///   요청: "OPEN\t&lt;경로&gt;"     응답: "OK\t&lt;메시지&gt;" 또는 "ERR\t&lt;오류 메시지&gt;"
    /// </summary>
    public static class BridgeProtocol
    {
        public const string CmdPing = "PING";
        public const string CmdOpen = "OPEN";
        public const string RespOk = "OK";
        public const string RespError = "ERR";
        public const char Separator = '\t';

        public static readonly Encoding Encoding = new UTF8Encoding(false);

        /// <summary>
        /// 같은 Windows 로그온 세션 + 같은 사용자끼리만 연결되도록 파이프 이름을 구성합니다.
        /// (원격 데스크톱 등으로 여러 세션에서 NX를 띄워도 서로 섞이지 않음)
        /// </summary>
        public static string PipeName
        {
            get
            {
                int sessionId = Process.GetCurrentProcess().SessionId;
                return "NxOpenFile_" + sessionId + "_" + Environment.UserName;
            }
        }

        /// <summary>응답/요청 한 줄 안에 탭·줄바꿈이 섞이지 않도록 정리합니다.</summary>
        public static string ToSingleLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
        }
    }
}
