using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NxOpenFile.App
{
    public partial class MainForm : Form
    {
        private const string FileFilter =
            "NX 파트 (*.prt)|*.prt|모든 파일 (*.*)|*.*";

        private string _lastDirectory;

        public MainForm()
        {
            InitializeComponent();
        }

        private async void MainForm_Shown(object sender, EventArgs e)
        {
            bool ready = await Task.Run(() => NxBridgeClient.IsNxReady());
            SetStatus(ready
                ? "NX 연결됨. [NX에서 파일 열기]를 누르세요."
                : "NX에 연결되지 않음. NX에서 NxOpenFile.NxBridge.dll을 먼저 실행하세요 (Ctrl+U).");
        }

        private async void btnOpen_Click(object sender, EventArgs e)
        {
            string path;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "NX에서 열 파일 선택";
                dialog.Filter = FileFilter;
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (!string.IsNullOrEmpty(_lastDirectory) && Directory.Exists(_lastDirectory))
                    dialog.InitialDirectory = _lastDirectory;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                path = dialog.FileName;
            }

            _lastDirectory = Path.GetDirectoryName(path);
            btnOpen.Enabled = false;
            UseWaitCursor = true;
            SetStatus("NX에서 여는 중... " + Path.GetFileName(path));

            try
            {
                // 파이프 통신은 UI 스레드를 막지 않도록 백그라운드에서 실행
                BridgeResult result = await Task.Run(() => NxBridgeClient.OpenFile(path));

                if (result.Success)
                {
                    SetStatus(result.Message);
                }
                else
                {
                    SetStatus("열기 실패");
                    MessageBox.Show(this, result.Message, "NX 파일 열기 실패",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (TimeoutException)
            {
                SetStatus("NX에 연결되지 않음");
                MessageBox.Show(this,
                    "실행 중인 NX에 연결할 수 없습니다.\n\n" +
                    "NX에서 파일 → 실행 → NX Open(Ctrl+U)으로\n" +
                    "NxOpenFile.NxBridge.dll을 먼저 실행했는지 확인하세요.",
                    "NX 연결 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                SetStatus("오류: " + ex.Message);
                MessageBox.Show(this, ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
                btnOpen.Enabled = true;
            }
        }

        private void SetStatus(string text)
        {
            lblStatus.Text = text;
        }
    }
}
