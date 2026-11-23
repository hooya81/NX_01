Imports NXOpen
Imports NXOpen.Annotations
Imports NXOpen.UF

''' <summary>치수를 쌓는 축 (치수선에 수직인 방향)</summary>
Public Enum StackAxis
    None
    X ' 수직 측정 치수: 좌/우로 쌓음
    Y ' 수평 측정 치수: 위/아래로 쌓음
End Enum

''' <summary>
''' 기준치수 선택 → 정렬할 치수 선택 → 형태(직렬/병렬/혼합)·간격 입력 → 기준치수 기준으로 배치.
''' 방향 판정: 치수 형식 → 선형 치수 측정 방법 → 형식 이름 순. 판정할 수 없으면 사용자가 방향을 고름.
''' </summary>
Friend NotInheritable Class ArrangeDimensionsCommand

    Private Const Title As String = "Arrange Dimensions"

    Private ReadOnly _session As Session
    Private ReadOnly _ui As UI
    Private _workPart As Part

    Public Sub New(theSession As Session, theUI As UI)
        _session = theSession
        _ui = theUI
    End Sub

    Public Sub Run()
        _workPart = _session.Parts.Work
        If _workPart Is Nothing Then
            ShowError("작업 파트가 없습니다. 도면 파트를 연 뒤 실행하세요.")
            Return
        End If

        ' 1) 기준치수 선택
        Dim refDim As Dimension = SelectReferenceDimension()
        If refDim Is Nothing Then Return

        ' 방향을 판정하지 못해도(None) 진행: 입력 창에서 사용자가 방향을 고름
        Dim refAxis As StackAxis = GetStackAxis(refDim)

        refDim.Highlight()
        Try
            ' 2) 정렬할 치수 선택
            Dim selected As List(Of Dimension) = SelectTargetDimensions(refDim)
            If selected Is Nothing OrElse selected.Count = 0 Then Return

            ' 3) 형태/간격/순서(+ 필요 시 방향) 입력
            Dim settings As ArrangeSettings = SettingsForm.Ask(ArrangeSettings.Load(), selected.Count, refAxis)
            If settings Is Nothing Then Return
            settings.Save()

            Dim axis As StackAxis = If(refAxis <> StackAxis.None, refAxis, settings.Axis)

            ' 4) 방향이 확실히 반대인 치수만 제외. 판정 불가(None) 치수는 사용자가 고른 것이므로 포함.
            Dim targets As New List(Of Dimension)()
            Dim skipped As Integer = 0
            For Each d In selected
                Dim a As StackAxis = GetStackAxis(d)
                If a = StackAxis.None OrElse a = axis Then
                    targets.Add(d)
                Else
                    skipped += 1
                End If
            Next

            If targets.Count = 0 Then
                ShowError("기준치수와 같은 방향의 치수가 선택되지 않았습니다.")
                Return
            End If
            If skipped > 0 Then
                ShowInfo("방향이 다른 치수 " & skipped & "개는 제외됩니다.")
            End If

            ' 5) 배치 계산 및 적용
            Apply(refDim, refAxis, targets, axis, settings)
        Finally
            refDim.Unhighlight()
        End Try
    End Sub

    ' ---------------------------------------------------------------- 선택

    Private Shared Function DimensionMask() As Selection.MaskTriple()
        Return New Selection.MaskTriple() {New Selection.MaskTriple(UFConstants.UF_dimension_type, 0, 0)}
    End Function

    Private Function SelectReferenceDimension() As Dimension
        Do
            Dim obj As TaggedObject = Nothing
            Dim cursor As Point3d
            Dim resp As Selection.Response = _ui.SelectionManager.SelectTaggedObject(
                "기준치수를 선택하세요",
                Title & " - 기준치수",
                Selection.SelectionScope.WorkPart,
                Selection.SelectionAction.ClearAndEnableSpecific,
                False, False,
                DimensionMask(),
                obj, cursor)

            If resp <> Selection.Response.ObjectSelected AndAlso
               resp <> Selection.Response.ObjectSelectedByName Then
                Return Nothing ' 취소/뒤로
            End If

            Dim d = TryCast(obj, Dimension)
            If d IsNot Nothing Then Return d

            ShowError("치수를 선택하세요.")
        Loop
    End Function

    ''' <summary>정렬할 치수 선택. 기준치수와 치수가 아닌 객체만 걸러내고 방향 필터는 나중에 적용.</summary>
    Private Function SelectTargetDimensions(refDim As Dimension) As List(Of Dimension)
        Dim objs As TaggedObject() = Nothing
        Dim resp As Selection.Response = _ui.SelectionManager.SelectTaggedObjects(
            "정렬할 치수를 선택하세요 (기준치수와 같은 방향)",
            Title & " - 정렬 대상",
            Selection.SelectionScope.WorkPart,
            Selection.SelectionAction.ClearAndEnableSpecific,
            False, False,
            DimensionMask(),
            objs)

        If resp <> Selection.Response.Ok OrElse objs Is Nothing OrElse objs.Length = 0 Then
            Return Nothing
        End If

        Dim result As New List(Of Dimension)()
        For Each o In objs
            Dim d = TryCast(o, Dimension)
            If d Is Nothing OrElse d.Tag = refDim.Tag Then Continue For
            result.Add(d)
        Next

        If result.Count = 0 Then
            ShowError("정렬할 치수가 선택되지 않았습니다.")
            Return Nothing
        End If
        Return result
    End Function

    ''' <summary>
    ''' 치수의 쌓는 축 판정.
    ''' 1) 형식: HorizontalDimension / VerticalDimension
    ''' 2) 선형 치수 빌더의 측정 방법(Measurement.Method): Horizontal / Vertical
    '''    (Rapid/Linear 명령으로 만들어 형식이 다르게 나오는 경우 대비)
    ''' 3) 형식 이름이 Horizontal*/Vertical* 로 시작 (Ordinate 제외)
    ''' 판정 불가 시 None.
    ''' </summary>
    Friend Function GetStackAxis(d As Dimension) As StackAxis
        If TypeOf d Is HorizontalDimension Then Return StackAxis.Y
        If TypeOf d Is VerticalDimension Then Return StackAxis.X

        Dim byMethod As StackAxis = AxisFromMeasurementMethod(d)
        If byMethod <> StackAxis.None Then Return byMethod

        Dim name As String = d.GetType().Name
        If name.IndexOf("Ordinate", StringComparison.OrdinalIgnoreCase) < 0 Then
            If name.StartsWith("Horizontal", StringComparison.OrdinalIgnoreCase) Then Return StackAxis.Y
            If name.StartsWith("Vertical", StringComparison.OrdinalIgnoreCase) Then Return StackAxis.X
        End If

        Return StackAxis.None
    End Function

    Private Function AxisFromMeasurementMethod(d As Dimension) As StackAxis
        Dim b As LinearDimensionBuilder = Nothing
        Try
            ' 읽기만 하고 Commit 하지 않음
            b = _workPart.Dimensions.CreateLinearDimensionBuilder(d)
            Select Case b.Measurement.Method
                Case DimensionMeasurementBuilder.MeasurementMethod.Horizontal
                    Return StackAxis.Y
                Case DimensionMeasurementBuilder.MeasurementMethod.Vertical
                    Return StackAxis.X
            End Select
        Catch ex As NXException
            ' 선형 치수가 아님(반경/각도 등) → 판정 불가
        Finally
            If b IsNot Nothing Then b.Destroy()
        End Try
        Return StackAxis.None
    End Function

    ' ---------------------------------------------------------------- 적용

    ''' <summary>True면 치수별 계산/이동 내역을 정보 창(Listing Window)에 출력 (문제 확인용)</summary>
    Private Const DebugLog As Boolean = True

    Private Sub Apply(refDim As Dimension, refAxis As StackAxis, targets As List(Of Dimension),
                      axis As StackAxis, settings As ArrangeSettings)
        Dim refCoord As Double = Coord(refDim.AnnotationOrigin, axis)

        Dim items As New List(Of LayoutItem)(targets.Count)
        For i = 0 To targets.Count - 1
            Dim o As Point3d = targets(i).AnnotationOrigin
            items.Add(New LayoutItem(i, Coord(o, axis) - refCoord, SafeSize(targets(i))))
        Next

        Dim newOffsets As IDictionary(Of Integer, Double) =
            DimensionLayout.Compute(items, settings.Mode, settings.Spacing, settings.Order, settings.RowTolerance)

        Dim log As ListingWindow = Nothing
        If DebugLog Then
            log = _session.ListingWindow
            log.Open()
            log.WriteLine("=== " & Title & " ===")
            log.WriteLine("형태=" & settings.Mode.ToString() & ", 간격=" & settings.Spacing &
                          ", 순서=" & settings.Order.ToString() & ", 축=" & axis.ToString())
            log.WriteLine("기준치수 Tag=" & CULng(refDim.Tag) & ", 유형=" & refDim.GetType().Name &
                          ", 판정=" & refAxis.ToString() & ", 기준좌표=" & refCoord)
            log.WriteLine("대상 " & targets.Count & "개 / 계산결과 " & newOffsets.Count & "개")
        End If

        Dim mark As Session.UndoMarkId = _session.SetUndoMark(Session.MarkVisibility.Visible, Title)

        Dim moved As Integer = 0
        For i = 0 To targets.Count - 1
            Dim d As Dimension = targets(i)
            Dim o As Point3d = d.AnnotationOrigin
            Dim prefix As String = "[" & i & "] Tag=" & CULng(d.Tag) & " 유형=" & d.GetType().Name &
                                   " 판정=" & If(log IsNot Nothing, GetStackAxis(d).ToString(), "") & " 현재=" & Coord(o, axis) &
                                   " 오프셋=" & items(i).Offset & " 크기=" & items(i).Size

            Dim newOffset As Double
            If Not newOffsets.TryGetValue(i, newOffset) Then
                ' 정상이라면 발생하지 않음. 발생 시 로그로 원인 확인
                If log IsNot Nothing Then log.WriteLine(prefix & " -> 계산결과 없음 (건너뜀)")
                Continue For
            End If

            Dim n As Point3d = WithCoord(o, axis, refCoord + newOffset)

            If Math.Abs(Coord(n, axis) - Coord(o, axis)) < 0.000000001 Then
                If log IsNot Nothing Then log.WriteLine(prefix & " -> 이미 위치에 있음 (" & Coord(n, axis) & ")")
                Continue For
            End If

            ' 치수선(텍스트) 위치만 쌓는 축 방향으로 이동. 다른 축 좌표는 유지.
            d.AnnotationOrigin = n
            moved += 1

            If log IsNot Nothing Then
                log.WriteLine(prefix & " -> 이동 " & Coord(n, axis) & " (설정 후 읽은 값=" & Coord(d.AnnotationOrigin, axis) & ")")
            End If
        Next

        If moved = 0 Then
            _session.DeleteUndoMark(mark, Nothing)
            ShowInfo("이미 정렬되어 있습니다.")
            Return
        End If

        _session.UpdateManager.DoUpdate(mark)
    End Sub

    Private Shared Function SafeSize(d As Dimension) As Double
        Try
            Return Math.Abs(d.ComputedSize)
        Catch ex As NXException
            Return 0.0
        End Try
    End Function

    Private Shared Function Coord(p As Point3d, axis As StackAxis) As Double
        Return If(axis = StackAxis.X, p.X, p.Y)
    End Function

    Private Shared Function WithCoord(p As Point3d, axis As StackAxis, value As Double) As Point3d
        If axis = StackAxis.X Then
            Return New Point3d(value, p.Y, p.Z)
        End If
        Return New Point3d(p.X, value, p.Z)
    End Function

    ' ---------------------------------------------------------------- 메시지

    Private Sub ShowError(msg As String)
        _ui.NXMessageBox.Show(Title, NXMessageBox.DialogType.Error, msg)
    End Sub

    Private Sub ShowInfo(msg As String)
        _ui.NXMessageBox.Show(Title, NXMessageBox.DialogType.Information, msg)
    End Sub

End Class
