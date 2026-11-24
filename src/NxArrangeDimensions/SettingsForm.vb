Imports System.Drawing
Imports System.Windows.Forms

''' <summary>배치 형태/간격/쌓는 순서 입력 대화상자 (디자이너 없이 코드로 구성)</summary>
Public NotInheritable Class SettingsForm
    Inherits Form

    Private ReadOnly _serial As RadioButton
    Private ReadOnly _parallel As RadioButton
    Private ReadOnly _mixed As RadioButton
    Private ReadOnly _spacing As NumericUpDown
    Private ReadOnly _orderGroup As GroupBox
    Private ReadOnly _bySize As RadioButton
    Private ReadOnly _keepCurrent As RadioButton
    Private ReadOnly _axisY As RadioButton
    Private ReadOnly _axisX As RadioButton
    Private ReadOnly _side As ComboBox

    Private Sub New(initial As ArrangeSettings, targetCount As Integer, askAxis As Boolean)
        Text = "Arrange Dimensions"
        FormBorderStyle = FormBorderStyle.FixedDialog
        StartPosition = FormStartPosition.CenterParent
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        AutoScaleMode = AutoScaleMode.Font
        ' 기준치수 방향을 판정하지 못한 경우에만 "쌓는 방향" 그룹 표시
        Dim dy As Integer = If(askAxis, 76, 0)
        ClientSize = New Size(300, 320 + dy)

        Dim info As New Label With {
            .Text = "정렬 대상 치수: " & targetCount & "개",
            .Location = New Point(12, 12),
            .AutoSize = True
        }

        ' 배치 형태
        Dim modeGroup As New GroupBox With {
            .Text = "배치 형태",
            .Location = New Point(12, 36 + dy),
            .Size = New Size(276, 92)
        }
        _serial = New RadioButton With {
            .Text = "직렬 (기준치수와 한 줄로)",
            .Location = New Point(10, 20),
            .AutoSize = True,
            .Checked = (initial.Mode = ArrangeMode.Serial)
        }
        _parallel = New RadioButton With {
            .Text = "병렬 (한 줄씩 간격으로 쌓기)",
            .Location = New Point(10, 43),
            .AutoSize = True,
            .Checked = (initial.Mode = ArrangeMode.Parallel)
        }
        _mixed = New RadioButton With {
            .Text = "혼합 (같은 줄 치수는 묶어서 쌓기)",
            .Location = New Point(10, 66),
            .AutoSize = True,
            .Checked = (initial.Mode = ArrangeMode.Mixed)
        }
        modeGroup.Controls.AddRange(New Control() {_serial, _parallel, _mixed})

        ' 간격
        Dim spacingLabel As New Label With {
            .Text = "치수 간격:",
            .Location = New Point(12, 141 + dy),
            .AutoSize = True
        }
        _spacing = New NumericUpDown With {
            .Location = New Point(110, 138 + dy),
            .Width = 100,
            .DecimalPlaces = 2,
            .Minimum = 0.01D,
            .Maximum = 10000D,
            .Increment = 1D,
            .Value = CDec(Math.Max(0.01, Math.Min(10000, initial.Spacing)))
        }

        ' 쌓는 쪽 (병렬)
        Dim sideLabel As New Label With {
            .Text = "쌓는 쪽:",
            .Location = New Point(12, 171 + dy),
            .AutoSize = True
        }
        _side = New ComboBox With {
            .Location = New Point(110, 168 + dy),
            .Width = 178,
            .DropDownStyle = ComboBoxStyle.DropDownList
        }
        _side.Items.AddRange(New Object() {"자동 (형상 바깥쪽)", "위 / 오른쪽 (+)", "아래 / 왼쪽 (-)"})
        _side.SelectedIndex = If(initial.Side > 0, 1, If(initial.Side < 0, 2, 0))

        ' 쌓는 순서
        _orderGroup = New GroupBox With {
            .Text = "쌓는 순서",
            .Location = New Point(12, 198 + dy),
            .Size = New Size(276, 70)
        }
        _bySize = New RadioButton With {
            .Text = "치수값 작은 것 안쪽",
            .Location = New Point(10, 20),
            .AutoSize = True,
            .Checked = (initial.Order = ArrangeOrder.BySizeAscending)
        }
        _keepCurrent = New RadioButton With {
            .Text = "현재 위치 순서 유지",
            .Location = New Point(10, 42),
            .AutoSize = True,
            .Checked = (initial.Order = ArrangeOrder.KeepCurrent)
        }
        _orderGroup.Controls.AddRange(New Control() {_bySize, _keepCurrent})

        Dim ok As New Button With {.Text = "확인", .DialogResult = DialogResult.OK, .Location = New Point(132, 282 + dy), .Width = 75}
        Dim cancel As New Button With {.Text = "취소", .DialogResult = DialogResult.Cancel, .Location = New Point(213, 282 + dy), .Width = 75}
        AcceptButton = ok
        CancelButton = cancel

        Controls.AddRange(New Control() {info, modeGroup, spacingLabel, _spacing, sideLabel, _side, _orderGroup, ok, cancel})

        ' 쌓는 방향 (기준치수 유형으로 방향을 알 수 없을 때)
        _axisY = New RadioButton With {
            .Text = "위/아래로 쌓기 (수평 측정 치수)",
            .Location = New Point(10, 20),
            .AutoSize = True,
            .Checked = (initial.Axis <> StackAxis.X)
        }
        _axisX = New RadioButton With {
            .Text = "좌/우로 쌓기 (수직 측정 치수)",
            .Location = New Point(10, 43),
            .AutoSize = True,
            .Checked = (initial.Axis = StackAxis.X)
        }
        If askAxis Then
            Dim axisGroup As New GroupBox With {
                .Text = "쌓는 방향 (기준치수 방향을 판정하지 못함)",
                .Location = New Point(12, 36),
                .Size = New Size(276, 70)
            }
            axisGroup.Controls.AddRange(New Control() {_axisY, _axisX})
            Controls.Add(axisGroup)
        End If

        ' 직렬은 간격/순서가 필요 없으므로 비활성화
        AddHandler _serial.CheckedChanged, AddressOf SyncEnabled
        AddHandler _parallel.CheckedChanged, AddressOf SyncEnabled
        AddHandler _mixed.CheckedChanged, AddressOf SyncEnabled
        SyncEnabled(Me, EventArgs.Empty)
    End Sub

    Private Sub SyncEnabled(sender As Object, e As EventArgs)
        Dim stacked As Boolean = Not _serial.Checked
        _spacing.Enabled = stacked
        _orderGroup.Enabled = stacked
        _side.Enabled = _parallel.Checked ' 쌓는 쪽은 병렬에서만 사용 (혼합은 현재 쪽 유지)
    End Sub

    ''' <summary>대화상자를 띄우고 입력값을 반환. 취소 시 Nothing.</summary>
    ''' <param name="askAxis">True면 "쌓는 방향" 선택을 표시 (기준치수 방향을 판정하지 못한 경우)</param>
    Public Shared Function Ask(initial As ArrangeSettings, targetCount As Integer, askAxis As Boolean) As ArrangeSettings
        Using form As New SettingsForm(initial, targetCount, askAxis)
            Try
                ' NX 메인 창을 부모로 지정 (NX 뒤로 숨는 문제 방지)
                NXOpenUI.FormUtilities.SetApplicationIcon(form)
                NXOpenUI.FormUtilities.ReparentForm(form)
            Catch
                ' 실패해도 대화상자는 동작
            End Try

            If form.ShowDialog() <> DialogResult.OK Then Return Nothing

            Dim mode As ArrangeMode
            If form._serial.Checked Then
                mode = ArrangeMode.Serial
            ElseIf form._mixed.Checked Then
                mode = ArrangeMode.Mixed
            Else
                mode = ArrangeMode.Parallel
            End If

            Return New ArrangeSettings With {
                .Mode = mode,
                .Spacing = CDbl(form._spacing.Value),
                .Order = If(form._keepCurrent.Checked, ArrangeOrder.KeepCurrent, ArrangeOrder.BySizeAscending),
                .Side = If(form._side.SelectedIndex = 1, 1, If(form._side.SelectedIndex = 2, -1, 0)),
                .Axis = If(Not askAxis, initial.Axis,
                           If(form._axisX.Checked, StackAxis.X, StackAxis.Y))
            }
        End Using
    End Function

End Class
