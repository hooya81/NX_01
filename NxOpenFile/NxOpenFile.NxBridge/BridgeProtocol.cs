using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        /// <summary>현재 디스플레이 파트의 컴포넌트 목록. 응답: "OK\t&lt;ComponentRow 직렬화&gt;"</summary>
        public const string CmdComponents = "COMPONENTS";

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
