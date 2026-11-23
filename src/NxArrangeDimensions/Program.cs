using System;
using NXOpen;

namespace NxArrangeDimensions
{
    /// <summary>NX Open 진입점 (File > Execute > NX Open 또는 메뉴/리본 버튼에서 호출)</summary>
    public static class Program
    {
        public static void Main(string[] args)
        {
            Session session = Session.GetSession();
            UI ui = UI.GetUI();

            try
            {
                new ArrangeDimensionsCommand(session, ui).Run();
            }
            catch (NXException ex)
            {
                ui.NXMessageBox.Show("Arrange Dimensions", NXMessageBox.DialogType.Error,
                    "NX 오류: " + ex.Message + " (" + ex.ErrorCode + ")");
            }
            catch (Exception ex)
            {
                ui.NXMessageBox.Show("Arrange Dimensions", NXMessageBox.DialogType.Error,
                    "오류: " + ex.Message + "\n\n" + ex.StackTrace);
            }
        }

        /// <summary>실행 후 DLL 즉시 언로드 (개발 중 재빌드 편의)</summary>
        public static int GetUnloadOption(string arg)
        {
            return (int)Session.LibraryUnloadOption.Immediately;
        }
    }
}
