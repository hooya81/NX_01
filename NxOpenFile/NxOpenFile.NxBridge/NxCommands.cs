using System;
using System.Collections.Generic;
using System.IO;
using NXOpen;
using NXOpen.Assemblies;

namespace NxOpenFile.NxBridge
{
    /// <summary>
    /// ★ NX 작업 코드는 여기에 작성합니다. ★
    ///
    /// 새 기능 추가 방법
    ///   1) 아래 형식의 함수를 만들고 [NxCommand("이름")]을 붙입니다.
    ///        [NxCommand("이름")]
    ///        public static string 함수(Session session, string arg) { ... }
    ///   2) EXE에서 NxBridgeClient.Call("이름", 인수)로 호출합니다.
    ///   (BridgeEntry.cs는 고칠 필요 없음. 함수는 NX 메인 스레드에서 실행됨)
    ///
    /// 명령 이름은 BridgeProtocol.cs의 NxCommandNames에 상수로 두면
    /// EXE와 여기서 같은 이름을 오타 없이 쓸 수 있습니다 (선택).
    /// </summary>
    public static class NxCommands
    {
        // ------------------------------------------------------------------
        // 파일 열기   arg = 파일 전체 경로   반환 = 결과 메시지
        // ------------------------------------------------------------------

        [NxCommand(NxCommandNames.OpenPart)]
        public static string OpenPart(Session session, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("파일 경로가 비어 있습니다.");
            if (!File.Exists(path))
                throw new FileNotFoundException("파일을 찾을 수 없습니다: " + path);

            PartLoadStatus loadStatus = null;
            try
            {
                // 이미 세션에 로드된 파일이면 다시 열지 않고 Display Part로 전환
                BasePart loaded = FindLoadedPart(session, path);
                if (loaded != null)
                {
                    session.Parts.SetActiveDisplay(
                        loaded,
                        DisplayPartOption.AllowAdditional,
                        PartDisplayPartWorkPartOption.UseLast,
                        out loadStatus);
                    return "이미 열려 있는 파일로 전환했습니다: " + loaded.Leaf + DescribeLoadStatus(loadStatus);
                }

                BasePart opened = session.Parts.OpenBaseDisplay(path, out loadStatus);
                return "열기 완료: " + opened.Leaf + DescribeLoadStatus(loadStatus);
            }
            finally
            {
                if (loadStatus != null) loadStatus.Dispose();
            }
        }

        // ------------------------------------------------------------------
        // 컴포넌트 목록   arg = 없음   반환 = ComponentRow 목록 (EXE에서 ComponentRow.Parse)
        // ------------------------------------------------------------------

        [NxCommand(NxCommandNames.GetComponents)]
        public static string GetComponents(Session session, string arg)
        {
            Part display = session.Parts.Display;
            if (display == null)
                throw new InvalidOperationException("NX에 열린 파트(디스플레이 파트)가 없습니다.");

            List<ComponentRow> rows = new List<ComponentRow>();

            // 0레벨: 현재 디스플레이 파트(어셈블리 최상위)
            rows.Add(new ComponentRow(0, display.Leaf, SafeFullPath(display), false, true));

            // 어셈블리가 아니면 RootComponent가 null
            Component root = display.ComponentAssembly.RootComponent;
            if (root != null)
                AddChildren(root, 1, rows);

            return ComponentRow.Serialize(rows);
        }

        // ------------------------------------------------------------------
        // 도우미 함수 ([NxCommand]가 없으므로 EXE에서 직접 호출되지 않음)
        // ------------------------------------------------------------------

        private static void AddChildren(Component parent, int level, List<ComponentRow> rows)
        {
            foreach (Component child in parent.GetChildren())
            {
                // 부분 로드/미로드 컴포넌트는 Prototype이 없을 수 있음
                BasePart prototype = child.Prototype as BasePart;
                bool loaded = prototype != null;

                rows.Add(new ComponentRow(
                    level,
                    child.DisplayName,
                    loaded ? SafeFullPath(prototype) : string.Empty,
                    child.IsSuppressed,
                    loaded));

                AddChildren(child, level + 1, rows);
            }
        }

        private static string SafeFullPath(BasePart part)
        {
            try { return part.FullPath; }
            catch (NXException) { return string.Empty; }
        }

        private static BasePart FindLoadedPart(Session session, string path)
        {
            foreach (BasePart part in session.Parts)
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
    }
}
