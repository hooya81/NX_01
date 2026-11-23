using System;
using System.Drawing;
using System.Windows.Forms;

namespace NxArrangeDimensions
{
    /// <summary>배치 형태/간격/정렬 순서 입력 대화상자 (디자이너 없이 코드로 구성)</summary>
    public sealed class SettingsForm : Form
    {
        private readonly RadioButton _serial;
        private readonly RadioButton _parallel;
        private readonly RadioButton _mixed;
        private readonly NumericUpDown _spacing;
        private readonly GroupBox _orderGroup;
        private readonly RadioButton _bySize;
        private readonly RadioButton _keepCurrent;

        private SettingsForm(ArrangeSettings initial, int targetCount)
        {
            Text = "Arrange Dimensions";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(300, 290);

            var info = new Label
            {
                Text = "정렬 대상 치수: " + targetCount + "개",
                Location = new Point(12, 12),
                AutoSize = true,
            };

            // 배치 형태
            var modeGroup = new GroupBox
            {
                Text = "배치 형태",
                Location = new Point(12, 36),
                Size = new Size(276, 92),
            };
            _serial = new RadioButton
            {
                Text = "직렬 (기준치수와 한 줄로)",
                Location = new Point(10, 20),
                AutoSize = true,
                Checked = initial.Mode == ArrangeMode.Serial,
            };
            _parallel = new RadioButton
            {
                Text = "병렬 (한 줄씩 간격으로 쌓기)",
                Location = new Point(10, 43),
                AutoSize = true,
                Checked = initial.Mode == ArrangeMode.Parallel,
            };
            _mixed = new RadioButton
            {
                Text = "혼합 (같은 줄 치수는 묶어서 쌓기)",
                Location = new Point(10, 66),
                AutoSize = true,
                Checked = initial.Mode == ArrangeMode.Mixed,
            };
            modeGroup.Controls.AddRange(new Control[] { _serial, _parallel, _mixed });

            // 간격
            var spacingLabel = new Label
            {
                Text = "치수 간격:",
                Location = new Point(12, 141),
                AutoSize = true,
            };
            _spacing = new NumericUpDown
            {
                Location = new Point(110, 138),
                Width = 100,
                DecimalPlaces = 2,
                Minimum = 0.01m,
                Maximum = 10000m,
                Increment = 1m,
                Value = (decimal)Math.Max(0.01, Math.Min(10000, initial.Spacing)),
            };

            // 정렬 순서
            _orderGroup = new GroupBox
            {
                Text = "쌓는 순서",
                Location = new Point(12, 168),
                Size = new Size(276, 70),
            };
            _bySize = new RadioButton
            {
                Text = "치수값 작은 것 안쪽",
                Location = new Point(10, 20),
                AutoSize = true,
                Checked = initial.Order == ArrangeOrder.BySizeAscending,
            };
            _keepCurrent = new RadioButton
            {
                Text = "현재 위치 순서 유지",
                Location = new Point(10, 42),
                AutoSize = true,
                Checked = initial.Order == ArrangeOrder.KeepCurrent,
            };
            _orderGroup.Controls.AddRange(new Control[] { _bySize, _keepCurrent });

            var ok = new Button { Text = "확인", DialogResult = DialogResult.OK, Location = new Point(132, 252), Width = 75 };
            var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, Location = new Point(213, 252), Width = 75 };
            AcceptButton = ok;
            CancelButton = cancel;

            Controls.AddRange(new Control[] { info, modeGroup, spacingLabel, _spacing, _orderGroup, ok, cancel });

            // 직렬은 간격/순서가 필요 없으므로 비활성화
            EventHandler sync = (s, e) =>
            {
                bool stacked = !_serial.Checked;
                _spacing.Enabled = stacked;
                _orderGroup.Enabled = stacked;
            };
            _serial.CheckedChanged += sync;
            _parallel.CheckedChanged += sync;
            _mixed.CheckedChanged += sync;
            sync(this, EventArgs.Empty);
        }

        /// <summary>대화상자를 띄우고 입력값을 반환. 취소 시 null.</summary>
        public static ArrangeSettings Ask(ArrangeSettings initial, int targetCount)
        {
            using (var form = new SettingsForm(initial, targetCount))
            {
                try
                {
                    // NX 메인 창을 부모로 지정 (NX 뒤로 숨는 문제 방지)
                    NXOpenUI.FormUtilities.SetApplicationIcon(form);
                    NXOpenUI.FormUtilities.ReparentForm(form);
                }
                catch
                {
                    // 실패해도 대화상자는 동작
                }

                if (form.ShowDialog() != DialogResult.OK) return null;

                ArrangeMode mode = form._serial.Checked ? ArrangeMode.Serial
                                 : form._mixed.Checked ? ArrangeMode.Mixed
                                 : ArrangeMode.Parallel;

                return new ArrangeSettings
                {
                    Mode = mode,
                    Spacing = (double)form._spacing.Value,
                    Order = form._keepCurrent.Checked ? ArrangeOrder.KeepCurrent : ArrangeOrder.BySizeAscending,
                };
            }
        }
    }
}
