Imports NXOpen
Imports NXOpen.Annotations
Imports NXOpen.UF

''' <summary>치수를 쌓는 축 (치수선에 수직인 방향)</summary>
Friend Enum StackAxis
    None
    X ' 수직 치수: 좌/우로 쌓음
    Y ' 수평 치수: 위/아래로 쌓음
End Enum

''' <summary>
''' 기준치수 선택 → 정렬할 치수 선택 → 형태(직렬/병렬/혼합)·간격 입력 → 기준치수 기준으로 배치.
''' 1차 버전 지원 범위: 수평(HorizontalDimension) / 수직(VerticalDimension) 치수.
''' </summary>
Friend NotInheritable Class ArrangeDimensionsCommand

    Private Const Title As String = "Arrange Dimensions"

    Private ReadOnly _session As Session
    Private ReadOnly _ui As UI

    Public Sub New(theSession As Session, theUI As UI)
        _session = theSession
        _ui = theUI
    End Sub

    Public Sub Run()
        Dim workPart As Part = _session.Parts.Work
        If workPart Is Nothing Then
            ShowError("작업 파트가 없습니다. 도면 파트를 연 뒤 실행하세요.")
            Return
        End If

        ' 1) 기준치수 선택
        Dim refDim As Dimension = SelectReferenceDimension()
        If refDim Is Nothing Then Return

        Dim axis As StackAxis = GetStackAxis(refDim)
        If axis = StackAxis.None Then
            ShowError("지원하지 않는 치수 유형입니다: " & refDim.GetType().Name & vbLf &
                      "현재 버전은 수평/수직 치수만 지원합니다.")
            Return
        End If

        refDim.Highlight()
        Try
            ' 2) 정렬할 치수 선택 (기준치수와 같은 유형만 사용)
            Dim targets As List(Of Dimension) = SelectTargetDimensions(refDim, axis)
            If targets Is Nothing OrElse targets.Count = 0 Then Return

            ' 3) 형태/간격/순서 입력
            Dim settings As ArrangeSettings = SettingsForm.Ask(ArrangeSettings.Load(), targets.Count)
            If settings Is Nothing Then Return
            settings.Save()

            ' 4) 배치 계산 및 적용
            Apply(refDim, targets, axis, settings)
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

    Private Function SelectTargetDimensions(refDim As Dimension, axis As StackAxis) As List(Of Dimension)
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

        Dim targets As New List(Of Dimension)()
        Dim skipped As Integer = 0
        For Each o In objs
            Dim d = TryCast(o, Dimension)
            If d Is Nothing OrElse d.Tag = refDim.Tag Then Continue For

            If GetStackAxis(d) = axis Then
                targets.Add(d)
            Else
                skipped += 1
            End If
        Next

        If targets.Count = 0 Then
            ShowError("기준치수와 같은 방향의 치수가 선택되지 않았습니다.")
            Return Nothing
        End If

        If skipped > 0 Then
            ShowInfo("방향이 다른 치수 " & skipped & "개는 제외됩니다.")
        End If

        Return targets
    End Function

    Friend Shared Function GetStackAxis(d As Dimension) As StackAxis
        If TypeOf d Is HorizontalDimension Then Return StackAxis.Y
        If TypeOf d Is VerticalDimension Then Return StackAxis.X
        Return StackAxis.None
    End Function

    ' ---------------------------------------------------------------- 적용

    Private Sub Apply(refDim As Dimension, targets As List(Of Dimension), axis As StackAxis, settings As ArrangeSettings)
        Dim refCoord As Double = Coord(refDim.AnnotationOrigin, axis)

        Dim items As New List(Of LayoutItem)(targets.Count)
        For i = 0 To targets.Count - 1
            Dim o As Point3d = targets(i).AnnotationOrigin
            items.Add(New LayoutItem(i, Coord(o, axis) - refCoord, SafeSize(targets(i))))
        Next

        Dim newOffsets As IDictionary(Of Integer, Double) =
            DimensionLayout.Compute(items, settings.Mode, settings.Spacing, settings.Order, settings.RowTolerance)

        Dim mark As Session.UndoMarkId = _session.SetUndoMark(Session.MarkVisibility.Visible, Title)

        Dim moved As Integer = 0
        For i = 0 To targets.Count - 1
            Dim newOffset As Double
            If Not newOffsets.TryGetValue(i, newOffset) Then Continue For

            Dim d As Dimension = targets(i)
            Dim o As Point3d = d.AnnotationOrigin
            Dim n As Point3d = WithCoord(o, axis, refCoord + newOffset)

            If Math.Abs(Coord(n, axis) - Coord(o, axis)) < 0.000000001 Then Continue For

            ' 치수선(텍스트) 위치만 쌓는 축 방향으로 이동. 다른 축 좌표는 유지.
            d.AnnotationOrigin = n
            moved += 1
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
