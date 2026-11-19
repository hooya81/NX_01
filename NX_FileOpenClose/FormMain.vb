Imports System
Imports System.IO
Imports System.Text
Imports System.Windows.Forms
Imports NXOpen
Imports NXOpen.Drawings

' Visual Studio 2019 + VB.NET + NX2606 DLL용 WinForm
' .NET Framework 4.8 / x64 / OutputType=Library
' NX에서 파일 -> 실행 -> NX Open 으로 이 DLL 실행
Public Class FormMain
    Inherits Form

    Private theSession As Session = Nothing

    Private txtFilePath As TextBox
    Private btnBrowse As Button
    Private btnOpen As Button
    Private btnCloseDisplay As Button
    Private btnCloseAll As Button
    Private btnSheetSize As Button
    Private txtImagePath As TextBox
    Private btnImageBrowse As Button
    Private numGapX As NumericUpDown
    Private numGapY As NumericUpDown
    Private chkAllSheets As CheckBox
    Private btnInsertImage As Button
    Private lblStatus As Label

    ' 도면 규격별 이미지 표시 폭 (시트 단위: ISO=mm, ANSI=inch). 필요에 맞게 수정
    ' 높이는 원본 이미지 비율로 자동 계산
    Private Shared ReadOnly ImageWidthBySize As New Dictionary(Of String, Double) From {
        {"A0", 120}, {"A1", 100}, {"A2", 80}, {"A3", 60}, {"A4", 40},
        {"A", 1.5}, {"B", 2.0}, {"C", 3.0}, {"D", 4.0}, {"E", 5.0}
    }
    ' 규격에 없는 사용자 정의 시트: 긴 변 x 이 비율을 이미지 폭으로 사용
    Private Const CustomSheetImageRatio As Double = 0.05
    ' 원본이 이 해상도(표시 크기 기준)보다 작으면 고해상도로 키워서 깨짐 방지
    Private Const ImageDpi As Double = 300

    ' NxMain.Main()에서 Session을 전달받아 생성
    Public Sub New(passedSession As Session)
        theSession = passedSession
        InitializeUI()
    End Sub

    ' NX 외부에서 단독 테스트용 기본 생성자 (그대로 GetSession 시도)
    Public Sub New()
        Me.New(Nothing)
    End Sub

    Private Sub InitializeUI()
        Me.Text = "NX 파일 열고 닫기 (VB.NET DLL / NX2606 / net48)"
        Me.Width = 620
        Me.Height = 420
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False

        Dim lbl As New Label With {
            .Text = "Part 경로 (.prt):",
            .Left = 12, .Top = 15, .Width = 120
        }
        Me.Controls.Add(lbl)

        txtFilePath = New TextBox With {
            .Left = 12, .Top = 38, .Width = 480
        }
        Me.Controls.Add(txtFilePath)

        btnBrowse = New Button With {
            .Text = "찾아보기...",
            .Left = 498, .Top = 36, .Width = 95
        }
        AddHandler btnBrowse.Click, AddressOf BtnBrowse_Click
        Me.Controls.Add(btnBrowse)

        btnOpen = New Button With {
            .Text = "파일 열기",
            .Left = 12, .Top = 75, .Width = 180, .Height = 35
        }
        AddHandler btnOpen.Click, AddressOf BtnOpen_Click
        Me.Controls.Add(btnOpen)

        btnCloseDisplay = New Button With {
            .Text = "현재 파일 닫기",
            .Left = 202, .Top = 75, .Width = 180, .Height = 35
        }
        AddHandler btnCloseDisplay.Click, AddressOf BtnCloseDisplay_Click
        Me.Controls.Add(btnCloseDisplay)

        btnCloseAll = New Button With {
            .Text = "모두 닫기",
            .Left = 392, .Top = 75, .Width = 180, .Height = 35
        }
        AddHandler btnCloseAll.Click, AddressOf BtnCloseAll_Click
        Me.Controls.Add(btnCloseAll)

        btnSheetSize = New Button With {
            .Text = "도면 사이즈 가져오기",
            .Left = 12, .Top = 118, .Width = 180, .Height = 35
        }
        AddHandler btnSheetSize.Click, AddressOf BtnSheetSize_Click
        Me.Controls.Add(btnSheetSize)

        '--- 도면 좌측 상단 이미지 추가 ---
        Me.Controls.Add(New Label With {
            .Text = "도면 이미지 (.png/.jpg/.bmp):",
            .Left = 12, .Top = 165, .Width = 300
        })

        txtImagePath = New TextBox With {
            .Left = 12, .Top = 188, .Width = 480
        }
        Me.Controls.Add(txtImagePath)

        btnImageBrowse = New Button With {
            .Text = "찾아보기...",
            .Left = 498, .Top = 186, .Width = 95
        }
        AddHandler btnImageBrowse.Click, AddressOf BtnImageBrowse_Click
        Me.Controls.Add(btnImageBrowse)

        Me.Controls.Add(New Label With {
            .Text = "여유 X:", .Left = 12, .Top = 227, .Width = 50
        })
        numGapX = New NumericUpDown With {
            .Left = 62, .Top = 224, .Width = 70,
            .DecimalPlaces = 2, .Minimum = 0D, .Maximum = 10000D, .Value = 10D
        }
        Me.Controls.Add(numGapX)

        Me.Controls.Add(New Label With {
            .Text = "여유 Y:", .Left = 142, .Top = 227, .Width = 50
        })
        numGapY = New NumericUpDown With {
            .Left = 192, .Top = 224, .Width = 70,
            .DecimalPlaces = 2, .Minimum = 0D, .Maximum = 10000D, .Value = 10D
        }
        Me.Controls.Add(numGapY)

        chkAllSheets = New CheckBox With {
            .Text = "모든 시트에 적용", .Left = 272, .Top = 225, .Width = 130
        }
        Me.Controls.Add(chkAllSheets)

        btnInsertImage = New Button With {
            .Text = "좌측 상단에 이미지 추가",
            .Left = 412, .Top = 218, .Width = 180, .Height = 35
        }
        AddHandler btnInsertImage.Click, AddressOf BtnInsertImage_Click
        Me.Controls.Add(btnInsertImage)

        lblStatus = New Label With {
            .Left = 12, .Top = 268, .Width = 580, .Height = 90,
            .BorderStyle = BorderStyle.FixedSingle,
            .Text = "상태: 준비됨. 파일을 선택 후 열기/닫기를 실행하세요."
        }
        Me.Controls.Add(lblStatus)
    End Sub

    Private Sub SetStatus(msg As String)
        lblStatus.Text = "상태: " & msg
    End Sub

    '--- NX 세션 확보 (DLL 내부 실행이므로 GetSession은 항상 성공해야 정상) ---
    Private Function EnsureSession() As Boolean
        If theSession IsNot Nothing Then Return True
        Try
            theSession = Session.GetSession()
            If theSession Is Nothing Then
                SetStatus("NX 세션을 가져오지 못했습니다.")
                Return False
            End If
            Return True
        Catch ex As Exception
            theSession = Nothing
            SetStatus("NX 세션 오류: " & ex.Message)
            Return False
        End Try
    End Function

    Private Sub BtnBrowse_Click(sender As Object, e As EventArgs)
        Using dlg As New OpenFileDialog()
            dlg.Filter = "NX Part (*.prt)|*.prt|모든 파일 (*.*)|*.*"
            dlg.Title = "열 NX 파일 선택"
            If dlg.ShowDialog() = DialogResult.OK Then
                txtFilePath.Text = dlg.FileName
            End If
        End Using
    End Sub

    '--- 파일 열기 ---
    Private Sub BtnOpen_Click(sender As Object, e As EventArgs)
        If Not EnsureSession() Then Return

        Dim filePath As String = txtFilePath.Text.Trim().Trim(""""c)
        If String.IsNullOrWhiteSpace(filePath) Then
            MessageBox.Show(Me, "먼저 .prt 파일 경로를 입력하세요.", "입력 필요",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        If Not File.Exists(filePath) Then
            MessageBox.Show(Me, "파일이 존재하지 않습니다:" & Environment.NewLine & filePath,
                            "파일 없음", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Try
            Dim loadStatus As PartLoadStatus = Nothing
            ' .prt는 OpenDisplay, 그 외 형식(STEP 등)은 OpenBaseDisplay 사용
            Dim ext As String = Path.GetExtension(filePath).ToLowerInvariant()
            Dim opened As BasePart
            If ext = ".prt" Then
                opened = theSession.Parts.OpenDisplay(filePath, loadStatus)
            Else
                opened = theSession.Parts.OpenBaseDisplay(filePath, loadStatus)
            End If

            If loadStatus IsNot Nothing Then
                loadStatus.Dispose()
            End If

            SetStatus("열기 성공: " & filePath)
        Catch ex As NXException
            SetStatus("열기 실패(NX): " & ex.Message & " (ErrorCode=" & ex.ErrorCode.ToString() & ")")
        Catch ex As Exception
            SetStatus("열기 실패: " & ex.Message)
        End Try
    End Sub

    '--- 현재 Display Part 닫기 ---
    Private Sub BtnCloseDisplay_Click(sender As Object, e As EventArgs)
        If Not EnsureSession() Then Return
        Try
            Dim dispPart As Part = theSession.Parts.Display
            If dispPart Is Nothing Then
                SetStatus("닫을 Display Part가 없습니다.")
                Return
            End If

            Dim partName As String = dispPart.FullPath
            ' CloseModified = 저장 없이 닫기. 저장 후 닫으려면 먼저 Save 후 호출.
            dispPart.Close(BasePart.CloseWholeTree.True,
                           BasePart.CloseModified.CloseModified,
                           Nothing)
            SetStatus("닫기 성공: " & partName)
        Catch ex As NXException
            SetStatus("닫기 실패(NX): " & ex.Message)
        Catch ex As Exception
            SetStatus("닫기 실패: " & ex.Message)
        End Try
    End Sub

    '--- 세션의 모든 Part 닫기 ---
    Private Sub BtnCloseAll_Click(sender As Object, e As EventArgs)
        If Not EnsureSession() Then Return
        Try
            theSession.Parts.CloseAll(BasePart.CloseModified.CloseModified, Nothing)
            SetStatus("모든 파트 닫기 완료.")
        Catch ex As NXException
            SetStatus("모두 닫기 실패(NX): " & ex.Message)
        Catch ex As Exception
            SetStatus("모두 닫기 실패: " & ex.Message)
        End Try
    End Sub

    '--- 도면(DrawingSheet) 사이즈 가져오기 ---
    Private Sub BtnSheetSize_Click(sender As Object, e As EventArgs)
        If Not EnsureSession() Then Return
        Try
            Dim dispPart As Part = theSession.Parts.Display
            If dispPart Is Nothing Then
                SetStatus("열려 있는 Display Part가 없습니다.")
                Return
            End If

            Dim sheets As DrawingSheet() = dispPart.DrawingSheets.ToArray()
            If sheets Is Nothing OrElse sheets.Length = 0 Then
                SetStatus("도면 시트(DrawingSheet)가 없습니다: " & dispPart.Leaf)
                Return
            End If

            Dim current As DrawingSheet = dispPart.DrawingSheets.CurrentDrawingSheet
            Dim sb As New StringBuilder()
            sb.AppendLine("Part: " & dispPart.Leaf & "  (시트 " & sheets.Length.ToString() & "개)")
            sb.AppendLine()

            For Each sheet As DrawingSheet In sheets
                Dim mark As String = If(current IsNot Nothing AndAlso sheet.Tag = current.Tag, " [현재]", "")
                sb.AppendLine(GetDrawingSheetSize(sheet) & mark)

                ' 시트에 배치된 DrawingView(DraftingView) 목록
                Dim views As DraftingView() = sheet.GetDraftingViews()
                If views IsNot Nothing Then
                    For Each v As DraftingView In views
                        sb.AppendLine("    - View: " & v.Name & "  (Scale " & v.Scale.ToString("0.####") & ")")
                    Next
                End If
            Next

            If current IsNot Nothing Then
                SetStatus("현재 시트 " & GetDrawingSheetSize(current))
            Else
                SetStatus("도면 시트 " & sheets.Length.ToString() & "개 조회 완료 (현재 시트 없음)")
            End If

            MessageBox.Show(Me, sb.ToString(), "도면 사이즈",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As NXException
            SetStatus("도면 사이즈 조회 실패(NX): " & ex.Message)
        Catch ex As Exception
            SetStatus("도면 사이즈 조회 실패: " & ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' DrawingSheet의 사이즈(가로 x 세로, 단위, 규격명, 축척)를 문자열로 반환
    ''' NX API: Length = 가로(폭), Height = 세로(높이)
    ''' </summary>
    Public Shared Function GetDrawingSheetSize(ByVal sheet As DrawingSheet) As String
        Dim width As Double = sheet.Length
        Dim height As Double = sheet.Height
        Dim isMetric As Boolean = (sheet.Units = DrawingSheet.Unit.Millimeters)
        Dim unitText As String = If(isMetric, "mm", "in")

        Dim num As Double = 1.0, den As Double = 1.0
        sheet.GetScale(num, den)

        Return String.Format("{0}: {1} x {2} {3} ({4}, Scale {5}:{6})",
                             sheet.Name,
                             width.ToString("0.##"), height.ToString("0.##"), unitText,
                             GetStandardSizeName(width, height, isMetric),
                             num.ToString("0.##"), den.ToString("0.##"))
    End Function

    ''' <summary>
    ''' 가로/세로 값으로 표준 도면 규격명 추정 (ISO A0~A4 / ANSI A~E)
    ''' </summary>
    Private Shared Function GetStandardSizeName(ByVal w As Double, ByVal h As Double, ByVal isMetric As Boolean) As String
        Dim key As String = GetStandardSizeKey(w, h, isMetric)
        If key Is Nothing Then Return "사용자 정의"
        Return key & If(w >= h, " 가로", " 세로")
    End Function

    ''' <summary>
    ''' 가로/세로 값으로 표준 규격 키 반환 (A0~A4 / A~E). 해당 없으면 Nothing
    ''' </summary>
    Private Shared Function GetStandardSizeKey(ByVal w As Double, ByVal h As Double, ByVal isMetric As Boolean) As String
        Dim shortSide As Double = Math.Min(w, h)
        Dim longSide As Double = Math.Max(w, h)

        Dim names As String()
        Dim sizes As Double(,)
        Dim tol As Double
        If isMetric Then
            names = {"A0", "A1", "A2", "A3", "A4"}
            sizes = {{841, 1189}, {594, 841}, {420, 594}, {297, 420}, {210, 297}}
            tol = 1.0
        Else
            names = {"A", "B", "C", "D", "E"}
            sizes = {{8.5, 11}, {11, 17}, {17, 22}, {22, 34}, {34, 44}}
            tol = 0.05
        End If

        For i As Integer = 0 To names.Length - 1
            If Math.Abs(shortSide - sizes(i, 0)) <= tol AndAlso Math.Abs(longSide - sizes(i, 1)) <= tol Then
                Return names(i)
            End If
        Next
        Return Nothing
    End Function

    Private Sub BtnImageBrowse_Click(sender As Object, e As EventArgs)
        Using dlg As New OpenFileDialog()
            dlg.Filter = "이미지 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|모든 파일 (*.*)|*.*"
            dlg.Title = "도면에 넣을 이미지 선택"
            If dlg.ShowDialog() = DialogResult.OK Then
                txtImagePath.Text = dlg.FileName
            End If
        End Using
    End Sub

    '--- 도면 좌측 상단에 이미지 추가 ---
    Private Sub BtnInsertImage_Click(sender As Object, e As EventArgs)
        If Not EnsureSession() Then Return

        Dim imagePath As String = txtImagePath.Text.Trim().Trim(""""c)
        If String.IsNullOrWhiteSpace(imagePath) OrElse Not File.Exists(imagePath) Then
            MessageBox.Show(Me, "이미지 파일을 선택하세요.", "입력 필요",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim dispPart As Part = theSession.Parts.Display
        If dispPart Is Nothing Then
            SetStatus("열려 있는 Display Part가 없습니다.")
            Return
        End If

        Dim current As DrawingSheet = dispPart.DrawingSheets.CurrentDrawingSheet
        Dim targets As DrawingSheet()
        If chkAllSheets.Checked Then
            targets = dispPart.DrawingSheets.ToArray()
        ElseIf current IsNot Nothing Then
            targets = {current}
        Else
            targets = New DrawingSheet() {}
        End If
        If targets.Length = 0 Then
            SetStatus("이미지를 넣을 도면 시트가 없습니다. (현재 시트 없음)")
            Return
        End If

        Dim gapX As Double = CDbl(numGapX.Value)
        Dim gapY As Double = CDbl(numGapY.Value)
        Dim markId As Session.UndoMarkId =
            theSession.SetUndoMark(Session.MarkVisibility.Visible, "도면 이미지 추가")

        Dim sb As New StringBuilder()
        Dim okCount As Integer = 0
        For Each sheet As DrawingSheet In targets
            Try
                sb.AppendLine(InsertImageTopLeft(dispPart, sheet, imagePath, gapX, gapY))
                okCount += 1
            Catch ex As Exception
                sb.AppendLine(sheet.Name & ": 실패 - " & ex.Message)
            End Try
        Next

        ' 원래 보던 시트로 복귀
        Try
            If current IsNot Nothing Then current.Open()
        Catch
        End Try

        SetStatus("이미지 추가 " & okCount.ToString() & "/" & targets.Length.ToString() & " 시트 완료")
        MessageBox.Show(Me, sb.ToString(), "도면 이미지 추가",
                        MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    ''' <summary>
    ''' 시트 좌측 상단 모서리에서 (gapX, gapY)만큼 떨어진 위치에
    ''' 규격별 폭(ImageWidthBySize)으로 이미지를 배치. 높이는 원본 비율 유지.
    ''' 시트 좌표: 원점 = 좌측 하단, 가로 = Length, 세로 = Height
    ''' </summary>
    Private Function InsertImageTopLeft(ByVal prt As Part, ByVal sheet As DrawingSheet,
                                        ByVal srcImage As String,
                                        ByVal gapX As Double, ByVal gapY As Double) As String
        Dim isMetric As Boolean = (sheet.Units = DrawingSheet.Unit.Millimeters)
        Dim unitText As String = If(isMetric, "mm", "in")
        Dim key As String = GetStandardSizeKey(sheet.Length, sheet.Height, isMetric)

        Dim dispW As Double
        If key Is Nothing OrElse Not ImageWidthBySize.TryGetValue(key, dispW) Then
            dispW = Math.Max(sheet.Length, sheet.Height) * CustomSheetImageRatio
        End If

        ' 1) 표시 크기 대비 해상도가 부족하면 고해상도 PNG로 먼저 키움
        Dim pxW As Integer, pxH As Integer
        Dim imgFile As String = PrepareHighResImage(srcImage, dispW, isMetric, pxW, pxH)
        Dim dispH As Double = dispW * pxH / pxW

        ' 2) 좌측 상단 기준점 = (여유X, 시트높이 - 여유Y)
        Dim topLeft As New Point3d(gapX, sheet.Height - gapY, 0.0)

        sheet.Open() ' 대상 시트를 현재 시트로
        Dim builder As NXOpen.Annotations.DraftingImageBuilder =
            prt.Annotations.DraftingImages.CreateDraftingImageBuilder(Nothing)
        Try
            Dim img As NXOpen.Annotations.RasterImageBuilder = builder.Image
            img.ImageFile = imgFile
            img.InsertPointView = sheet.View
            img.SizeOption = NXOpen.Display.ImageBaseBuilder.SizeOptions.UserDefined
            img.LockAspectRatio = False
            img.Width.RightHandSide = dispW.ToString("0.####", Globalization.CultureInfo.InvariantCulture)
            img.Height.RightHandSide = dispH.ToString("0.####", Globalization.CultureInfo.InvariantCulture)
            img.LockAspectRatio = True
            img.BasePointChoice = NXOpen.Display.ImageBaseBuilder.BasePointChoices.TopLeft
            img.InsertionPointOption = NXOpen.Display.ImageBaseBuilder.InsertionPoint.UserDefined
            img.UserInsertionPoint = prt.Points.CreatePoint(topLeft)
            img.InsertionPointLocation = topLeft
            builder.Commit()
        Finally
            builder.Destroy()
        End Try

        Return String.Format("{0} ({1}): 이미지 {2} x {3} {4} @ 좌상단+({5}, {6}), 원본→{7}x{8}px",
                             sheet.Name, If(key, "사용자 정의"),
                             dispW.ToString("0.##"), dispH.ToString("0.##"), unitText,
                             gapX.ToString("0.##"), gapY.ToString("0.##"), pxW, pxH)
    End Function

    ''' <summary>
    ''' 표시 폭(dispW)을 ImageDpi로 출력할 때 필요한 픽셀 수보다 원본이 작으면
    ''' 고품질 보간(HighQualityBicubic)으로 확대한 PNG를 임시폴더에 만들어 경로 반환.
    ''' 충분히 크면 원본 경로를 그대로 반환. pxW/pxH = 최종 이미지 픽셀 크기.
    ''' </summary>
    Private Shared Function PrepareHighResImage(ByVal src As String, ByVal dispW As Double, ByVal isMetric As Boolean,
                                                ByRef pxW As Integer, ByRef pxH As Integer) As String
        Dim inches As Double = If(isMetric, dispW / 25.4, dispW)
        Dim needW As Integer = CInt(Math.Ceiling(inches * ImageDpi))

        Using bmp As New System.Drawing.Bitmap(src)
            pxW = bmp.Width
            pxH = bmp.Height
            If bmp.Width >= needW Then Return src

            Dim newW As Integer = needW
            Dim newH As Integer = Math.Max(1, CInt(Math.Round(bmp.Height * (needW / CDbl(bmp.Width)))))
            Dim outPath As String = Path.Combine(Path.GetTempPath(),
                Path.GetFileNameWithoutExtension(src) & "_" & newW.ToString() & "x" & newH.ToString() & ".png")

            Using big As New System.Drawing.Bitmap(newW, newH, System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                big.SetResolution(CSng(ImageDpi), CSng(ImageDpi))
                Using g As System.Drawing.Graphics = System.Drawing.Graphics.FromImage(big)
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality
                    Using attr As New System.Drawing.Imaging.ImageAttributes()
                        ' 가장자리 번짐 방지
                        attr.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY)
                        g.DrawImage(bmp, New System.Drawing.Rectangle(0, 0, newW, newH),
                                    0, 0, bmp.Width, bmp.Height, System.Drawing.GraphicsUnit.Pixel, attr)
                    End Using
                End Using
                big.Save(outPath, System.Drawing.Imaging.ImageFormat.Png)
            End Using

            pxW = newW
            pxH = newH
            Return outPath
        End Using
    End Function

End Class
