Imports NXOpen
Imports NXOpen.Annotations
Imports NXOpen.UF

''' <summary>사용자가 고르는 쌓는 방향 (기준치수 방향을 판정하지 못했을 때)</summary>
Public Enum StackAxis
    None
    X ' 좌/우로 쌓음 (수직 측정 치수)
    Y ' 위/아래로 쌓음 (수평 측정 치수)
End Enum

''' <summary>
''' 기준치수 선택 → 정렬할 치수 선택 → 형태(직렬/병렬/혼합)·간격 입력 → 기준치수 기준으로 배치.
''' 쌓는 방향 판정:
'''   수평/수직 치수 → 형식 또는 측정 방법으로 판정
'''   수직거리(Perpendicular) 치수 → 기준선의 시트상 방향 (기울어진 선이면 그 각도로 쌓음)
'''   판정 불가 → 사용자가 입력 창에서 방향 선택
''' </summary>
Friend NotInheritable Class ArrangeDimensionsCommand

    Private Const Title As String = "Arrange Dimensions"

    Private ReadOnly _session As Session
    Private ReadOnly _ui As UI
    Private ReadOnly _uf As UFSession
    Private _workPart As Part

    ''' <summary>치수별 방향 판정 결과 캐시 (빌더 생성 횟수 줄이기)</summary>
    Private ReadOnly _dirCache As New Dictionary(Of Tag, StackDirection)()

    Public Sub New(theSession As Session, theUI As UI)
        _session = theSession
        _ui = theUI
        _uf = UFSession.GetUFSession()
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

        ' 방향을 판정하지 못해도 진행: 입력 창에서 사용자가 방향을 고름
        Dim refDir As StackDirection = GetStackDirection(refDim)

        refDim.Highlight()
        Try
            ' 2) 정렬할 치수 선택
            Dim selected As List(Of Dimension) = SelectTargetDimensions(refDim)
            If selected Is Nothing OrElse selected.Count = 0 Then Return

            ' 3) 형태/간격/순서(+ 필요 시 방향) 입력
            Dim settings As ArrangeSettings = SettingsForm.Ask(ArrangeSettings.Load(), selected.Count, Not refDir.IsValid)
            If settings Is Nothing Then Return
            settings.Save()

            Dim dir As StackDirection = If(refDir.IsValid, refDir, StackDirection.FromAxis(settings.Axis))

            ' 4) 방향이 확실히 다른 치수만 제외. 판정 불가 치수는 사용자가 고른 것이므로 포함.
            Dim targets As New List(Of Dimension)()
            Dim skipped As Integer = 0
            For Each d In selected
                Dim td As StackDirection = GetStackDirection(d)
                If Not td.IsValid OrElse td.IsParallelTo(dir) Then
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
            Apply(refDim, refDir, targets, dir, settings)
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

    ' ---------------------------------------------------------------- 방향 판정

    ''' <summary>
    ''' 치수의 쌓는 방향 판정 (판정 불가 시 StackDirection.None).
    ''' 1) 형식: HorizontalDimension / VerticalDimension
    ''' 2) 형식: PerpendicularDimension → 기준선의 시트상 방향
    ''' 3) 선형 치수 빌더의 측정 방법(Measurement.Method): Horizontal / Vertical
    ''' 4) 형식 이름이 Horizontal*/Vertical* 로 시작 (Ordinate 제외)
    ''' </summary>
    Friend Function GetStackDirection(d As Dimension) As StackDirection
        Dim cached As StackDirection
        If _dirCache.TryGetValue(d.Tag, cached) Then Return cached

        Dim result As StackDirection = DetectStackDirection(d)
        _dirCache(d.Tag) = result
        Return result
    End Function

    Private Function DetectStackDirection(d As Dimension) As StackDirection
        If TypeOf d Is HorizontalDimension Then Return StackDirection.AlongY
        If TypeOf d Is VerticalDimension Then Return StackDirection.AlongX

        If TypeOf d Is PerpendicularDimension Then
            Dim perp As StackDirection = DirectionOfPerpendicular(d)
            If perp.IsValid Then Return perp
        End If

        Dim byMethod As StackDirection = DirectionFromMeasurementMethod(d)
        If byMethod.IsValid Then Return byMethod

        Dim name As String = d.GetType().Name
        If name.IndexOf("Ordinate", StringComparison.OrdinalIgnoreCase) < 0 Then
            If name.StartsWith("Horizontal", StringComparison.OrdinalIgnoreCase) Then Return StackDirection.AlongY
            If name.StartsWith("Vertical", StringComparison.OrdinalIgnoreCase) Then Return StackDirection.AlongX
        End If

        Return StackDirection.None
    End Function

    Private Function DirectionFromMeasurementMethod(d As Dimension) As StackDirection
        Dim b As LinearDimensionBuilder = Nothing
        Try
            ' 읽기만 하고 Commit 하지 않음
            b = _workPart.Dimensions.CreateLinearDimensionBuilder(d)
            Select Case b.Measurement.Method
                Case DimensionMeasurementBuilder.MeasurementMethod.Horizontal
                    Return StackDirection.AlongY
                Case DimensionMeasurementBuilder.MeasurementMethod.Vertical
                    Return StackDirection.AlongX
            End Select
        Catch ex As NXException
            ' 선형 치수가 아님(반경/각도 등) → 판정 불가
        Finally
            If b IsNot Nothing Then b.Destroy()
        End Try
        Return StackDirection.None
    End Function

    ''' <summary>
    ''' 수직거리(Perpendicular) 치수: 치수선은 기준선에 수직이므로, 치수를 쌓는 방향은 기준선과 나란한 방향.
    ''' 연관 객체(첫 번째/두 번째) 중 직선을 찾아 시트 좌표로 변환한 방향을 사용.
    ''' </summary>
    Private Function DirectionOfPerpendicular(d As Dimension) As StackDirection
        Dim b As LinearDimensionBuilder = Nothing
        Try
            ' 읽기만 하고 Commit 하지 않음
            b = _workPart.Dimensions.CreateLinearDimensionBuilder(d)
            For Each sel As SelectNXObject In New SelectNXObject() {b.FirstAssociativity, b.SecondAssociativity}
                If sel Is Nothing Then Continue For

                Dim obj As TaggedObject = Nothing
                Dim view As View = Nothing
                Dim pick As Point3d
                sel.GetValue(obj, view, pick)

                Dim p1 As Point3d, p2 As Point3d
                If Not TryGetLineEnds(obj, p1, p2) Then Continue For

                Dim s1 As Double() = ToSheet(p1, view)
                Dim s2 As Double() = ToSheet(p2, view)
                Dim dir As New StackDirection(s2(0) - s1(0), s2(1) - s1(1))
                If dir.IsValid Then Return dir
            Next
        Catch ex As NXException
            ' 판정 불가
        Finally
            If b IsNot Nothing Then b.Destroy()
        End Try
        Return StackDirection.None
    End Function

    ''' <summary>직선(곡선 Line 또는 직선 모서리 Edge)의 양 끝점</summary>
    Private Shared Function TryGetLineEnds(obj As TaggedObject, ByRef p1 As Point3d, ByRef p2 As Point3d) As Boolean
        Dim ln = TryCast(obj, Line)
        If ln IsNot Nothing Then
            p1 = ln.StartPoint
            p2 = ln.EndPoint
            Return True
        End If

        Dim e = TryCast(obj, Edge)
        If e IsNot Nothing AndAlso e.SolidEdgeType = Edge.EdgeType.Linear Then
            e.GetVertices(p1, p2)
            Return True
        End If

        Return False
    End Function

    ''' <summary>도면 뷰 안의 객체면 모델 좌표 → 시트 좌표로 변환, 시트 위 객체면 그대로</summary>
    Private Function ToSheet(p As Point3d, view As View) As Double()
        If TypeOf view Is Drawings.DraftingView Then
            Dim model As Double() = {p.X, p.Y, p.Z}
            Dim sheet(1) As Double
            _uf.View.MapModelToDrawing(view.Tag, model, sheet)
            Return sheet
        End If
        Return New Double() {p.X, p.Y}
    End Function

    ''' <summary>
    ''' 형상 바깥쪽 판정: 기준치수가 측정하는 형상(연관 객체)의 위치보다 치수가 어느 쪽에 있는지.
    ''' +1 = 쌓는 방향 + 쪽이 바깥, -1 = - 쪽이 바깥, 0 = 판정 불가.
    ''' </summary>
    Private Function DetectOutwardSign(refDim As Dimension, dir As StackDirection) As Integer
        Dim b As LinearDimensionBuilder = Nothing
        Try
            ' 읽기만 하고 Commit 하지 않음
            b = _workPart.Dimensions.CreateLinearDimensionBuilder(refDim)
            Dim sum As Double = 0
            Dim cnt As Integer = 0
            For Each sel As SelectNXObject In New SelectNXObject() {b.FirstAssociativity, b.SecondAssociativity}
                If sel Is Nothing Then Continue For

                Dim obj As TaggedObject = Nothing
                Dim view As View = Nothing
                Dim pick As Point3d
                sel.GetValue(obj, view, pick)

                For Each gp In GeometryPoints(obj)
                    Dim sp As Double() = ToSheet(gp, view)
                    sum += dir.CoordOf(sp(0), sp(1))
                    cnt += 1
                Next
            Next
            If cnt = 0 Then Return 0

            Dim diff As Double = Coord(refDim.AnnotationOrigin, dir) - sum / cnt
            If Math.Abs(diff) < 0.000001 Then Return 0
            Return Math.Sign(diff)
        Catch ex As NXException
            Return 0
        Finally
            If b IsNot Nothing Then b.Destroy()
        End Try
    End Function

    ''' <summary>연관 객체의 대표 점들 (모서리/선의 양 끝점, 점 좌표). 모르는 객체는 빈 목록.</summary>
    Private Shared Function GeometryPoints(obj As TaggedObject) As List(Of Point3d)
        Dim pts As New List(Of Point3d)()

        Dim ln = TryCast(obj, Line)
        If ln IsNot Nothing Then
            pts.Add(ln.StartPoint)
            pts.Add(ln.EndPoint)
            Return pts
        End If

        Dim e = TryCast(obj, Edge)
        If e IsNot Nothing Then
            Dim v1 As Point3d, v2 As Point3d
            e.GetVertices(v1, v2)
            pts.Add(v1)
            pts.Add(v2)
            Return pts
        End If

        Dim pt = TryCast(obj, Point)
        If pt IsNot Nothing Then
            pts.Add(pt.Coordinates)
        End If

        Return pts
    End Function

    ' ---------------------------------------------------------------- 적용

    ''' <summary>True면 치수별 계산/이동 내역을 정보 창(Listing Window)에 출력 (문제 확인용)</summary>
    Private Const DebugLog As Boolean = True

    Private Sub Apply(refDim As Dimension, refDir As StackDirection, targets As List(Of Dimension),
                      dir As StackDirection, settings As ArrangeSettings)
        Dim refCoord As Double = Coord(refDim.AnnotationOrigin, dir)

        Dim items As New List(Of LayoutItem)(targets.Count)
        For i = 0 To targets.Count - 1
            Dim o As Point3d = targets(i).AnnotationOrigin
            items.Add(New LayoutItem(i, Coord(o, dir) - refCoord, SafeSize(targets(i))))
        Next

        ' 병렬: 모든 치수를 한쪽(형상 바깥쪽)으로 쌓음
        Dim side As Integer = 0
        Dim sideReason As String = "현재 쪽 유지"
        If settings.Mode = ArrangeMode.Parallel Then
            If settings.Side <> 0 Then
                side = Math.Sign(settings.Side)
                sideReason = "사용자 지정"
            Else
                side = DetectOutwardSign(refDim, dir)
                sideReason = "형상 바깥쪽 자동"
                If side = 0 Then
                    ' 형상 위치를 못 읽으면 대상 치수가 더 많이 있는 쪽 (동수면 +)
                    side = If(items.Sum(Function(x) Math.Sign(x.Offset)) < 0, -1, 1)
                    sideReason = "형상 위치 판정 불가 → 대상 치수가 많은 쪽"
                End If
            End If
        End If

        Dim newOffsets As IDictionary(Of Integer, Double) =
            DimensionLayout.Compute(items, settings.Mode, settings.Spacing, settings.Order, settings.RowTolerance, side)

        Dim log As ListingWindow = Nothing
        If DebugLog Then
            log = _session.ListingWindow
            log.Open()
            log.WriteLine("=== " & Title & " ===")
            log.WriteLine("형태=" & settings.Mode.ToString() & ", 간격=" & settings.Spacing &
                          ", 순서=" & settings.Order.ToString() & ", 쌓는방향=" & dir.ToString())
            log.WriteLine("기준치수 Tag=" & CULng(refDim.Tag) & ", 유형=" & refDim.GetType().Name &
                          ", 판정=" & refDir.ToString() & ", 기준좌표=" & refCoord)
            log.WriteLine("쌓는 쪽=" & If(side > 0, "+", If(side < 0, "-", "각자")) & " (" & sideReason & ")")
            log.WriteLine("대상 " & targets.Count & "개 / 계산결과 " & newOffsets.Count & "개")
        End If

        Dim mark As Session.UndoMarkId = _session.SetUndoMark(Session.MarkVisibility.Visible, Title)

        Dim moved As Integer = 0
        For i = 0 To targets.Count - 1
            Dim d As Dimension = targets(i)
            Dim o As Point3d = d.AnnotationOrigin
            Dim prefix As String = ""
            If log IsNot Nothing Then
                prefix = "[" & i & "] Tag=" & CULng(d.Tag) & " 유형=" & d.GetType().Name &
                         " 판정=" & GetStackDirection(d).ToString() & " 현재=" & Coord(o, dir) &
                         " 오프셋=" & items(i).Offset & " 크기=" & items(i).Size
            End If

            Dim newOffset As Double
            If Not newOffsets.TryGetValue(i, newOffset) Then
                ' 정상이라면 발생하지 않음. 발생 시 로그로 원인 확인
                If log IsNot Nothing Then log.WriteLine(prefix & " -> 계산결과 없음 (건너뜀)")
                Continue For
            End If

            Dim n As Point3d = WithCoord(o, dir, refCoord + newOffset)

            If Math.Abs(Coord(n, dir) - Coord(o, dir)) < 0.000000001 Then
                If log IsNot Nothing Then log.WriteLine(prefix & " -> 이미 위치에 있음 (" & Coord(n, dir) & ")")
                Continue For
            End If

            ' 치수선(텍스트) 위치를 쌓는 방향으로만 이동. 치수선 방향 위치는 유지.
            d.AnnotationOrigin = n
            moved += 1

            If log IsNot Nothing Then
                log.WriteLine(prefix & " -> 이동 " & Coord(n, dir) & " (설정 후 읽은 값=" & Coord(d.AnnotationOrigin, dir) & ")")
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

    ''' <summary>점의 쌓는 방향 좌표</summary>
    Private Shared Function Coord(p As Point3d, dir As StackDirection) As Double
        Return dir.CoordOf(p.X, p.Y)
    End Function

    ''' <summary>쌓는 방향 좌표만 value로 바꾼 점 (치수선 방향 성분과 Z는 유지)</summary>
    Private Shared Function WithCoord(p As Point3d, dir As StackDirection, value As Double) As Point3d
        Dim delta As Double = value - Coord(p, dir)
        Return New Point3d(p.X + delta * dir.X, p.Y + delta * dir.Y, p.Z)
    End Function

    ' ---------------------------------------------------------------- 메시지

    Private Sub ShowError(msg As String)
        _ui.NXMessageBox.Show(Title, NXMessageBox.DialogType.Error, msg)
    End Sub

    Private Sub ShowInfo(msg As String)
        _ui.NXMessageBox.Show(Title, NXMessageBox.DialogType.Information, msg)
    End Sub

End Class
