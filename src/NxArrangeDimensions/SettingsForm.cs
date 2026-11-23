using System;
using System.Drawing;
using System.Windows.Forms;

namespace NxArrangeDimensions
{
    /// <summary>간격/정렬 순서 입력 대화상자 (디자이너 없이 코드로 구성)</summary>
    public sealed class SettingsForm : Form
    {
        private readonly NumericUpDown _spacing;
        private readonly RadioButton _keepCurrent;
        private readonly RadioButton _bySize;

        private SettingsForm(ArrangeSettings initial, int targetCount)
        {
            Text = "Arrange Dimensions";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(300, 190);

            var info = new Label
            {
                Text = "정렬 대상 치수: " + targetCount + "개",
                Location = new Point(12, 12),
                AutoSize = true,
            };

            var spacingLabel = new Label
            {
                Text = "치수 간격:",
                Location = new Point(12, 44),
                AutoSize = true,
            };
            _spacing = new NumericUpDown
            {
                Location = new Point(110, 41),
                Width = 100,
                DecimalPlaces = 2,
                Minimum = 0.01m,
                Maximum = 10000m,
                Increment = 1m,
                Value = (decimal)Math.Max(0.01, Math.Min(10000, initial.Spacing)),
            };

            var orderGroup = new GroupBox
            {
                Text = "정렬 순서",
                Location = new Point(12, 72),
                Size = new Size(276, 70),
            };
            _keepCurrent = new RadioButton
            {
                Text = "현재 위치 순서 유지",
                Location = new Point(10, 20),
                AutoSize = true,
                Checked = initial.Order == ArrangeOrder.KeepCurrent,
            };
            _bySize = new RadioButton
            {
                Text = "치수값 작은 것 안쪽",
                Location = new Point(10, 42),
                AutoSize = true,
                Checked = initial.Order == ArrangeOrder.BySizeAscending,
            };
            orderGroup.Controls.Add(_keepCurrent);
            orderGroup.Controls.Add(_bySize);

            var ok = new Button { Text = "확인", DialogResult = DialogResult.OK, Location = new Point(132, 152), Width = 75 };
            var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, Location = new Point(213, 152), Width = 75 };
            AcceptButton = ok;
            CancelButton = cancel;

            Controls.AddRange(new Control[] { info, spacingLabel, _spacing, orderGroup, ok, cancel });
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

                return new ArrangeSettings
                {
                    Spacing = (double)form._spacing.Value,
                    Order = form._bySize.Checked ? ArrangeOrder.BySizeAscending : ArrangeOrder.KeepCurrent,
                };
            }
        }
    }
}
