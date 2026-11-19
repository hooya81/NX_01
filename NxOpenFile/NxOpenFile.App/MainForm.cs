using System;
using System.Collections.Generic;
using System.Drawing;
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
                ? "NX 연결됨. [NX에서 파일 열기] 또는 [컴포넌트 목록]을 누르세요."
                : "NX에 연결되지 않음. NX에서 NxOpenFile.NxBridge.dll을 먼저 실행하세요 (Ctrl+U).\n파이프: " + BridgeProtocol.PipeName);
        }

        // ------------------------------------------------------------------
        // 파일 열기
        // ------------------------------------------------------------------

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
            SetBusy(true);
            SetStatus("NX에서 여는 중... " + Path.GetFileName(path));

            try
            {
                if (!await Task.Run(() => NxBridgeClient.IsBridgeListening()))
                {
                    ShowNotConnected();
                    return;
                }

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
                // 파이프는 있지만 NX가 3초 안에 연결을 받지 못한 경우 (다른 요청 처리 중 등)
                ShowNotConnected();
            }
            catch (Exception ex)
            {
                SetStatus("오류: " + ex.Message);
                MessageBox.Show(this, ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ------------------------------------------------------------------
        // 컴포넌트 목록
        // ------------------------------------------------------------------

        private async void btnComponents_Click(object sender, EventArgs e)
        {
            SetBusy(true);
            SetStatus("NX에서 컴포넌트 목록을 가져오는 중...");

            try
            {
                if (!await Task.Run(() => NxBridgeClient.IsBridgeListening()))
                {
                    ShowNotConnected();
                    return;
                }

                List<ComponentRow> rows = await Task.Run(() => NxBridgeClient.GetComponents());
                ShowComponents(rows);

                if (rows.Count == 0)
                    SetStatus("받은 목록이 비어 있습니다.");
                else if (rows.Count == 1)
                    SetStatus(rows[0].Name + ": 어셈블리가 아니거나 컴포넌트가 없습니다.");
                else
                    SetStatus(rows[0].Name + ": 컴포넌트 " + (rows.Count - 1) + "개");
            }
            catch (BridgeException ex)
            {
                // NX가 처리하지 못한 경우 (열린 파트 없음 등)
                lvComponents.Items.Clear();
                SetStatus(ex.Message);
                MessageBox.Show(this, ex.Message, "컴포넌트 목록", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (TimeoutException)
            {
                ShowNotConnected();
            }
            catch (Exception ex)
            {
                SetStatus("오류: " + ex.Message);
                MessageBox.Show(this, ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void ShowComponents(List<ComponentRow> rows)
        {
            lvComponents.BeginUpdate();
            try
            {
                lvComponents.Items.Clear();
                foreach (ComponentRow row in rows)
                {
                    // 레벨만큼 들여써서 트리처럼 표시
                    string indent = new string(' ', row.Level * 4);
                    ListViewItem item = new ListViewItem(indent + row.Name);
                    item.SubItems.Add(row.Level.ToString());
                    item.SubItems.Add(row.Loaded ? row.FilePath : "(로드 안 됨)");
                    item.SubItems.Add(row.Suppressed ? "억제됨" : (row.Loaded ? "" : "미로드"));

                    if (row.Level == 0)
                        item.Font = new Font(lvComponents.Font, FontStyle.Bold);
                    if (row.Suppressed || !row.Loaded)
                        item.ForeColor = SystemColors.GrayText;

                    lvComponents.Items.Add(item);
                }
            }
            finally
            {
                lvComponents.EndUpdate();
            }
        }

        // ------------------------------------------------------------------
        // 공통
        // ------------------------------------------------------------------

        private void ShowNotConnected()
        {
            SetStatus("NX에 연결되지 않음. 파이프: " + BridgeProtocol.PipeName);
            MessageBox.Show(this,
                "실행 중인 NX에 연결할 수 없습니다.\n\n" +
                "NX에서 파일 → 실행 → NX Open(Ctrl+U)으로\n" +
                "NxOpenFile.NxBridge.dll을 먼저 실행했는지 확인하세요.\n\n" +
                "이 프로그램이 찾는 파이프: " + BridgeProtocol.PipeName + "\n" +
                "(NX의 브리지 시작 메시지에 표시된 파이프 이름과 같아야 합니다)",
                "NX 연결 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void SetBusy(bool busy)
        {
            btnOpen.Enabled = !busy;
            btnComponents.Enabled = !busy;
            UseWaitCursor = busy;
        }

        private void SetStatus(string text)
        {
            lblStatus.Text = text;
        }
    }
}
