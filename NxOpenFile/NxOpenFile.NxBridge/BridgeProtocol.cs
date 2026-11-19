using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace NxOpenFile
{
    /// <summary>
    /// NxCommands.cs의 [NxCommand] 이름 모음. EXE와 브리지가 같은 이름을 쓰도록 공유합니다.
    /// (새 명령을 만들 때 여기에 추가하는 것은 선택 사항. 문자열을 직접 써도 됩니다.)
    /// </summary>
    public static class NxCommandNames
    {
        /// <summary>arg = 파일 전체 경로, 반환 = 결과 메시지</summary>
        public const string OpenPart = "OpenPart";
        /// <summary>arg 없음, 반환 = ComponentRow.Serialize 결과</summary>
        public const string GetComponents = "GetComponents";
    }

    /// <summary>
    /// EXE(클라이언트)와 NX 안의 브리지 DLL(서버)이 공유하는 통신 규약.
    /// 이 파일은 NxOpenFile.App 프로젝트에 "링크"로 포함되므로 NXOpen을 참조하면 안 됩니다.
    ///
    /// 요청 1회 = 파이프 연결 1회, 한 줄 요청 / 한 줄 응답 (UTF-8, BOM 없음)
    ///   요청: "&lt;명령이름&gt;\t&lt;인수&gt;"   응답: "OK\t&lt;결과&gt;" 또는 "ERR\t&lt;오류 메시지&gt;"
    ///   인수와 결과는 Escape되어 있어 여러 줄 문자열도 주고받을 수 있습니다.
    ///   "PING"은 브리지 자체가 응답하는 연결 확인용 명령입니다.
    /// </summary>
    public static class BridgeProtocol
    {
        public const string CmdPing = "PING";

        /// <summary>목록 응답에서 행 구분 (ASCII Record Separator)</summary>
        public const char RecordSeparator = '\u001E';
        /// <summary>목록 응답에서 열 구분 (ASCII Unit Separator)</summary>
        public const char FieldSeparator = '\u001F';
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

        /// <summary>
        /// 줄바꿈을 \n, \r 문자로 바꿔 한 줄로 만듭니다 (역슬래시는 \\).
        /// 요청 인수와 응답 결과에 사용하며, 받는 쪽에서 Unescape로 원래대로 되돌립니다.
        /// </summary>
        public static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        public static string Unescape(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('\\') < 0) return text ?? string.Empty;

            StringBuilder sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\' && i + 1 < text.Length)
                {
                    char next = text[i + 1];
                    if (next == 'n') { sb.Append('\n'); i++; continue; }
                    if (next == 'r') { sb.Append('\r'); i++; continue; }
                    if (next == '\\') { sb.Append('\\'); i++; continue; }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>응답/요청 한 줄 안에 탭·줄바꿈이 섞이지 않도록 정리합니다.</summary>
        public static string ToSingleLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
        }

        /// <summary>목록의 한 칸 값: 줄바꿈·탭·구분 문자를 제거합니다.</summary>
        public static string ToField(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return ToSingleLine(text)
                .Replace(RecordSeparator, ' ')
                .Replace(FieldSeparator, ' ');
        }
    }

    /// <summary>컴포넌트 목록의 한 행. 브리지(NX)에서 만들고 EXE에서 읽습니다.</summary>
    public sealed class ComponentRow
    {
        public ComponentRow(int level, string name, string filePath, bool suppressed, bool loaded)
        {
            Level = level;
            Name = name ?? string.Empty;
            FilePath = filePath ?? string.Empty;
            Suppressed = suppressed;
            Loaded = loaded;
        }

        /// <summary>0 = 최상위(디스플레이 파트), 1 = 바로 아래 컴포넌트, ...</summary>
        public int Level { get; private set; }
        public string Name { get; private set; }
        public string FilePath { get; private set; }
        public bool Suppressed { get; private set; }
        public bool Loaded { get; private set; }

        public static string Serialize(IEnumerable<ComponentRow> rows)
        {
            StringBuilder sb = new StringBuilder();
            foreach (ComponentRow row in rows)
            {
                if (sb.Length > 0) sb.Append(BridgeProtocol.RecordSeparator);
                sb.Append(row.Level.ToString(CultureInfo.InvariantCulture)).Append(BridgeProtocol.FieldSeparator)
                  .Append(BridgeProtocol.ToField(row.Name)).Append(BridgeProtocol.FieldSeparator)
                  .Append(BridgeProtocol.ToField(row.FilePath)).Append(BridgeProtocol.FieldSeparator)
                  .Append(row.Suppressed ? "1" : "0").Append(BridgeProtocol.FieldSeparator)
                  .Append(row.Loaded ? "1" : "0");
            }
            return sb.ToString();
        }

        public static List<ComponentRow> Parse(string payload)
        {
            List<ComponentRow> rows = new List<ComponentRow>();
            if (string.IsNullOrEmpty(payload)) return rows;

            foreach (string record in payload.Split(BridgeProtocol.RecordSeparator))
            {
                string[] f = record.Split(BridgeProtocol.FieldSeparator);
                if (f.Length < 5) continue;

                int level;
                int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out level);
                rows.Add(new ComponentRow(level, f[1], f[2], f[3] == "1", f[4] == "1"));
            }
            return rows;
        }
    }
}
