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
''' 치수 여러 개 선택(1번째 = 기준치수) → 형태 자동 판정 → 간격 등 입력 → 기준치수 기준으로 배치.
''' 형태 자동 판정: 1번째와 2번째 치수의 측정 시작점이 같으면 병렬, 다르면 직렬.
''' 쌓는 방향 판정:
'''   수평/수직 치수 → 형식 또는 측정 방법으로 판정
'''   수직거리(Perpendicular) 치수 → 기준선의 시트상 방향 (기울어진 선이면 그 각도로 쌓음)
'''   판정 불가 → 사용자가 입력 창에서 방향 선택
''' </summary>
Friend NotInheritable Class ArrangeDimensionsCommand

    Private Const Title As String = "Arrange Dimensions"

    ''' <summary>시작점이 "같다"고 보는 허용 거리 (시트 좌표)</summary>
    Private Const StartTolerance As Double = 0.05

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

        ' 1) 치수 여러 개 선택 (선택 순서 유지, 1번째 = 기준치수)
        Dim selected As List(Of Dimension) = SelectDimensions()
        If selected Is Nothing Then Return
        If selected.Count < 2 Then
            ShowError("치수를 2개 이상 선택하세요. (1번째 선택한 치수가 기준치수)")
            Return
        End If

        Dim refDim As Dimension = selected(0)
        Dim others As List(Of Dimension) = selected.Skip(1).ToList()

        ' 방향을 판정하지 못해도 진행: 입력 창에서 사용자가 방향을 고름
        Dim refDir As StackDirection = GetStackDirection(refDim)

        refDim.Highlight()
        Try
            ' 2) 방향을 알면 미리 대상 치수를 거르고 형태를 자동 판정해 입력 창에 표시
            Dim targets As List(Of Dimension) = Nothing
            Dim skipped As Integer = 0
            Dim autoMode As ArrangeMode = ArrangeMode.Auto ' Auto = 판정 불가
            Dim autoReason As String = "방향 선택 후 판정"
            If refDir.IsValid Then
                targets = FilterTargets(others, refDir, skipped)
                If targets.Count > 0 Then autoMode = DetectAutoMode(refDim, targets(0), refDir, autoReason)
            End If

            ' 3) 형태/간격/쌓는 쪽/순서(+ 필요 시 방향) 입력
            Dim settings As ArrangeSettings = SettingsForm.Ask(ArrangeSettings.Load(), selected.Count,
                                                               Not refDir.IsValid, AutoText(autoMode, autoReason))
            If settings Is Nothing Then Return
            settings.Save()

            Dim dir As StackDirection = If(refDir.IsValid, refDir, StackDirection.FromAxis(settings.Axis))

            ' 4) 방향을 입력 창에서 고른 경우 이제 거르고 판정
            If targets Is Nothing Then
                targets = FilterTargets(others, dir, skipped)
                If targets.Count > 0 Then autoMode = DetectAutoMode(refDim, targets(0), dir, autoReason)
            End If

            If targets.Count = 0 Then
                ShowError("기준치수(1번째)와 같은 방향의 치수가 선택되지 않았습니다.")
                Return
            End If
            If skipped > 0 Then
                ShowInfo("방향이 다른 치수 " & skipped & "개는 제외됩니다.")
            End If

            ' 5) 형태 결정
            Dim mode As ArrangeMode = settings.Mode
            Dim modeReason As String = "사용자 지정"
            If mode = ArrangeMode.Auto Then
                If autoMode = ArrangeMode.Auto Then
                    mode = ArrangeMode.Parallel
                    modeReason = "자동 판정 불가(" & autoReason & ") → 병렬"
                    ShowInfo("1·2번째 치수의 시작점을 읽지 못해 병렬로 정렬합니다." & vbLf & autoReason)
                Else
                    mode = autoMode
                    modeReason = "자동: " & autoReason
                End If
            End If

            ' 6) 배치 계산 및 적용
            Apply(refDim, refDir, targets, dir, settings, mode, modeReason)
        Finally
            refDim.Unhighlight()
        End Try
    End Sub

    ''' <summary>방향이 확실히 다른 치수만 제외. 판정 불가 치수는 사용자가 고른 것이므로 포함.</summary>
    Private Function FilterTargets(others As List(Of Dimension), dir As StackDirection, ByRef skipped As Integer) As List(Of Dimension)
        Dim targets As New List(Of Dimension)()
        skipped = 0
        For Each d In others
            Dim td As StackDirection = GetStackDirection(d)
            If Not td.IsValid OrElse td.IsParallelTo(dir) Then
                targets.Add(d)
            Else
                skipped += 1
            End If
        Next
        Return targets
    End Function

    Private Shared Function AutoText(autoMode As ArrangeMode, reason As String) As String
        Select Case autoMode
            Case ArrangeMode.Parallel : Return "병렬 (" & reason & ")"
            Case ArrangeMode.Serial : Return "직렬 (" & reason & ")"
            Case Else : Return "판정 불가 → 병렬 (" & reason & ")"
        End Select
    End Function

    ' ---------------------------------------------------------------- 선택

    Private Shared Function DimensionMask() As Selection.MaskTriple()
        Return New Selection.MaskTriple() {New Selection.MaskTriple(UFConstants.UF_dimension_type, 0, 0)}
    End Function

    ''' <summary>치수 여러 개 선택. 선택 순서를 유지하고 중복/치수가 아닌 객체는 제외.</summary>
    Private Function SelectDimensions() As List(Of Dimension)
        Dim objs As TaggedObject() = Nothing
        Dim resp As Selection.Response = _ui.SelectionManager.SelectTaggedObjects(
            "정렬할 치수를 순서대로 선택하세요 (1번째 = 기준치수)",
            Title,
            Selection.SelectionScope.WorkPart,
            Selection.SelectionAction.ClearAndEnableSpecific,
            False, False,
            DimensionMask(),
            objs)

        If resp <> Selection.Response.Ok OrElse objs Is Nothing OrElse objs.Length = 0 Then
            Return Nothing
        End If

        Dim result As New List(Of Dimension)()
        Dim seen As New HashSet(Of Tag)()
        For Each o In objs
            Dim d = TryCast(o, Dimension)
            If d Is Nothing OrElse Not seen.Add(d.Tag) Then Continue For
            result.Add(d)
        Next
        Return result
    End Function

    ' ---------------------------------------------------------------- 형태 자동 판정

    ''' <summary>
    ''' 1번째(기준)와 2번째 치수의 측정 구간을 비교해 병렬/직렬 판정.
    ''' 판정 불가 시 ArrangeMode.Auto 반환.
    ''' </summary>
    Private Function DetectAutoMode(first As Dimension, second As Dimension, dir As StackDirection,
                                    ByRef reason As String) As ArrangeMode
        Dim a As Double() = MeasuredInterval(first, dir)
        Dim b As Double() = MeasuredInterval(second, dir)
        If a Is Nothing OrElse b Is Nothing Then
            reason = If(a Is Nothing, "1번째", "2번째") & " 치수의 측정점을 읽지 못함"
            Diag("  형태 판정: " & reason)
            Return ArrangeMode.Auto
        End If

        Dim mode As ArrangeMode = DimensionLayout.DetectMode(a(0), a(1), b(0), b(1), StartTolerance)
        reason = If(mode = ArrangeMode.Parallel, "1·2번째 시작점 같음", "1·2번째 시작점 다름")
        Diag("  형태 판정: 1번째 구간=[" & Math.Round(Math.Min(a(0), a(1)), 3) & ", " & Math.Round(Math.Max(a(0), a(1)), 3) &
             "], 2번째 구간=[" & Math.Round(Math.Min(b(0), b(1)), 3) & ", " & Math.Round(Math.Max(b(0), b(1)), 3) &
             "] → " & mode.ToString())
        Return mode
    End Function

    ''' <summary>
    ''' 치수가 측정하는 두 점의 치수선 방향 좌표 (시트 기준). 읽지 못하면 Nothing.
    ''' 치수선 방향 = 쌓는 방향에 수직 (수평 치수면 X, 수직 치수면 Y).
    ''' </summary>
    Private Function MeasuredInterval(d As Dimension, dir As StackDirection) As Double()
        Dim b As LinearDimensionBuilder = Nothing
        Try
            ' 읽기만 하고 Commit 하지 않음
            b = _workPart.Dimensions.CreateLinearDimensionBuilder(d)
            Dim coords As New List(Of Double)()
            For Each sel As SelectNXObject In New SelectNXObject() {b.FirstAssociativity, b.SecondAssociativity}
                If sel Is Nothing Then Continue For

                Dim obj As TaggedObject = Nothing
                Dim view As View = Nothing
                Dim pick As Point3d
                sel.GetValue(obj, view, pick)

                Dim p As Double() = MeasuredPoint(obj, view, pick, d.AnnotationOrigin)
                If p IsNot Nothing Then coords.Add(p(0) * dir.Y - p(1) * dir.X)
            Next
            If coords.Count < 2 Then Return Nothing
            Return New Double() {coords(0), coords(1)}
        Catch ex As Exception
            Diag("  치수 Tag=" & CULng(d.Tag) & " 측정점 읽기 오류: " & ex.Message)
            Return Nothing
        Finally
            If b IsNot Nothing Then b.Destroy()
        End Try
    End Function

    ''' <summary>
    ''' 연관 객체 하나에서 치수가 측정하는 점 (시트 좌표).
    ''' 점이면 그 좌표, 곡선/모서리면 선택 위치(pick)에 가장 가까운 끝점, 그 밖에는 선택 위치.
    ''' </summary>
    Private Function MeasuredPoint(obj As TaggedObject, view As View, pick As Point3d, near As Point3d) As Double()
        If obj Is Nothing Then Return Nothing

        Dim pt = TryCast(obj, Point)
        If pt IsNot Nothing Then
            Return ToSheetPoints(obj, New List(Of Point3d) From {pt.Coordinates}, view, near)(0)
        End If

        ' 선택 위치는 모델/시트 어느 좌표인지 확실하지 않으므로 치수에 가까운 해석 사용
        Dim pickSheet As Double() = ToSheetPoints(obj, New List(Of Point3d) From {pick}, view, near, False)(0)

        Dim ends As List(Of Point3d) = CurveEndPoints(obj, False)
        If ends.Count = 0 Then Return pickSheet

        Dim best As Double() = Nothing
        Dim bestDist As Double = Double.MaxValue
        For Each e In ToSheetPoints(obj, ends, view, near)
            Dim dist As Double = Math.Sqrt((e(0) - pickSheet(0)) ^ 2 + (e(1) - pickSheet(1)) ^ 2)
            If dist < bestDist Then
                bestDist = dist
                best = e
            End If
        Next
        Return best
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
        Catch ex As Exception
            ' 선형 치수가 아님(반경/각도 등) → 판정 불가
        Finally
            If b IsNot Nothing Then b.Destroy()
        End Try
        Return StackDirection.None
    End Function

    ''' <summary>
    ''' 수직거리(Perpendicular) 치수: 치수선은 기준선에 수직이므로, 치수를 쌓는 방향은 기준선과 나란한 방향.
    ''' 연관 객체(첫 번째/두 번째) 중 직선(Line / 직선 Edge / 직선 DraftingCurve 등)을 찾아 시트상 방향을 사용.
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

                Dim ends As List(Of Point3d) = CurveEndPoints(obj, True)
                If ends.Count < 2 Then Continue For

                Dim sheet As List(Of Double()) = ToSheetPoints(obj, ends, view, d.AnnotationOrigin)
                Dim dir As New StackDirection(sheet(1)(0) - sheet(0)(0), sheet(1)(1) - sheet(0)(1))
                Diag("  수직거리 치수 Tag=" & CULng(d.Tag) & " 기준선=" & obj.GetType().Name &
                     " 시트 끝점=(" & Fmt(sheet(0)) & ")~(" & Fmt(sheet(1)) & ") → 방향 " & dir.ToString())
                If dir.IsValid Then Return dir
            Next
            Diag("  수직거리 치수 Tag=" & CULng(d.Tag) & ": 연관 객체에서 직선을 찾지 못함")
        Catch ex As Exception
            Diag("  수직거리 치수 Tag=" & CULng(d.Tag) & " 판정 오류: " & ex.Message)
        Finally
            If b IsNot Nothing Then b.Destroy()
        End Try
        Return StackDirection.None
    End Function

    ''' <summary>
    ''' 곡선형 객체의 양 끝점.
    ''' Line / Edge 는 NXOpen 속성으로, 그 밖의 곡선(DraftingCurve 등)은 부모 모서리 → UF_EVAL → 곡선 속성 → 경계 상자 순으로 시도.
    ''' lineOnly = True 이면 직선일 때만 반환 (직선이 아니면 빈 목록).
    ''' </summary>
    Private Function CurveEndPoints(obj As TaggedObject, lineOnly As Boolean) As List(Of Point3d)
        Dim pts As New List(Of Point3d)()
        If obj Is Nothing Then Return pts

        Dim ln = TryCast(obj, Line)
        If ln IsNot Nothing Then
            pts.Add(ln.StartPoint)
            pts.Add(ln.EndPoint)
            Return pts
        End If

        Dim e = TryCast(obj, Edge)
        If e IsNot Nothing Then
            If lineOnly AndAlso e.SolidEdgeType <> Edge.EdgeType.Linear Then Return pts
            Dim v1 As Point3d, v2 As Point3d
            e.GetVertices(v1, v2)
            pts.Add(v1)
            pts.Add(v2)
            Return pts
        End If

        ' DraftingCurve 등: 아래 방법을 차례로 시도 (앞 방법이 실패하면 다음 방법)
        '   0) 원본 모서리       : DraftingCurve 의 부모(모델 Edge/Line)의 끝점 (모델 좌표)
        '      DraftingCurve 는 모델 모서리를 뷰에 투영한 객체라 UF_EVAL 등이
        '      "Cannot extract the curve from edge" 오류를 내므로 가장 먼저 시도
        '   1) UF_EVAL           : 곡선 계산기 (직선 여부 + 양 끝점)
        '   2) UF_MODL 곡선 속성  : 매개변수 0 / 0.5 / 1 위치 (가운데 점으로 직선 여부 확인)
        '   3) UF_MODL 경계 상자  : 상자 대각선 끝점 (수평/수직 직선만 직선으로 인정)
        Dim errors As New List(Of String)()

        If TryParentEnds(obj, lineOnly, pts, errors) Then Return pts
        If TryEvalEnds(obj, lineOnly, pts, errors) Then Return pts
        If TryCurvePropsEnds(obj, lineOnly, pts, errors) Then Return pts
        If TryBoundingBoxEnds(obj, lineOnly, pts, errors) Then Return pts

        Diag("  " & obj.GetType().Name & " 끝점 읽기 실패: " & String.Join(" / ", errors))
        pts.Clear()
        Return pts
    End Function

    ''' <summary>
    ''' 0) DraftingCurve 의 부모(원본 모델 모서리/곡선)에서 끝점.
    ''' UF_DRAW_ask_drafting_curve_parents 로 부모를 찾고, 부모가 Edge/Line 이면 그 끝점(모델 좌표) 사용.
    ''' </summary>
    Private Function TryParentEnds(obj As TaggedObject, lineOnly As Boolean, pts As List(Of Point3d),
                                   errors As List(Of String)) As Boolean
        If Not TypeOf obj Is Drawings.DraftingCurve Then Return False
        Try
            Dim count As Integer = 0
            Dim parents As Tag() = Nothing
            _uf.Draw.AskDraftingCurveParents(obj.Tag, count, parents)
            If parents Is Nothing OrElse count = 0 Then
                errors.Add("parents: 부모 없음")
                Return False
            End If

            For i = 0 To count - 1
                Dim parent As TaggedObject = NXOpen.Utilities.NXObjectManager.Get(parents(i))
                Dim ln = TryCast(parent, Line)
                If ln IsNot Nothing Then
                    pts.Add(ln.StartPoint)
                    pts.Add(ln.EndPoint)
                    Diag("  DraftingCurve 끝점: 부모 Line")
                    Return True
                End If

                Dim e = TryCast(parent, Edge)
                If e IsNot Nothing Then
                    If lineOnly AndAlso e.SolidEdgeType <> Edge.EdgeType.Linear Then Return True ' 직선 아님
                    Dim v1 As Point3d, v2 As Point3d
                    e.GetVertices(v1, v2)
                    pts.Add(v1)
                    pts.Add(v2)
                    Diag("  DraftingCurve 끝점: 부모 Edge (" & e.SolidEdgeType.ToString() & ")")
                    Return True
                End If
            Next

            errors.Add("parents: 부모가 Edge/Line 이 아님 (" &
                       String.Join(",", parents.Take(count).Select(Function(t) NXOpen.Utilities.NXObjectManager.Get(t).GetType().Name)) & ")")
            Return False
        Catch ex As Exception
            errors.Add("parents: " & ex.Message)
            pts.Clear()
            Return False
        End Try
    End Function

    ''' <summary>1) UF_EVAL 로 끝점. 성공 시 True (lineOnly 인데 직선이 아니면 빈 목록으로 True).</summary>
    Private Function TryEvalEnds(obj As TaggedObject, lineOnly As Boolean, pts As List(Of Point3d),
                                 errors As List(Of String)) As Boolean
        Dim evaluator As IntPtr = IntPtr.Zero
        Try
            _uf.Eval.Initialize(obj.Tag, evaluator)

            Dim isLine As Boolean = False
            _uf.Eval.IsLine(evaluator, isLine)
            If lineOnly AndAlso Not isLine Then Return True

            Dim limits(1) As Double
            _uf.Eval.AskLimits(evaluator, limits)
            For Each t In limits
                Dim p(2) As Double
                Dim deriv(2) As Double
                _uf.Eval.Evaluate(evaluator, 0, t, p, deriv)
                pts.Add(New Point3d(p(0), p(1), p(2)))
            Next
            Diag("  " & obj.GetType().Name & " 끝점: UF_EVAL")
            Return True
        Catch ex As Exception
            errors.Add("UF_EVAL: " & ex.Message)
            pts.Clear()
            Return False
        Finally
            Try
                If evaluator <> IntPtr.Zero Then _uf.Eval.Free(evaluator)
            Catch
            End Try
        End Try
    End Function

    ''' <summary>2) UF_MODL_ask_curve_props (매개변수 0~1) 로 끝점.</summary>
    Private Function TryCurvePropsEnds(obj As TaggedObject, lineOnly As Boolean, pts As List(Of Point3d),
                                       errors As List(Of String)) As Boolean
        Try
            Dim sample As New List(Of Point3d)()
            For Each parm In New Double() {0.0, 0.5, 1.0}
                Dim p(2) As Double, tangent(2) As Double, pNorm(2) As Double, bNorm(2) As Double
                Dim torsion As Double, radius As Double
                _uf.Modl.AskCurveProps(obj.Tag, parm, p, tangent, pNorm, bNorm, torsion, radius)
                sample.Add(New Point3d(p(0), p(1), p(2)))
            Next

            If lineOnly AndAlso Not IsCollinear(sample(0), sample(1), sample(2)) Then Return True
            pts.Add(sample(0))
            pts.Add(sample(2))
            Diag("  " & obj.GetType().Name & " 끝점: UF_MODL_ask_curve_props")
            Return True
        Catch ex As Exception
            errors.Add("curve_props: " & ex.Message)
            pts.Clear()
            Return False
        End Try
    End Function

    ''' <summary>3) UF_MODL_ask_bounding_box 로 끝점 (대각선).</summary>
    Private Function TryBoundingBoxEnds(obj As TaggedObject, lineOnly As Boolean, pts As List(Of Point3d),
                                        errors As List(Of String)) As Boolean
        Try
            Dim box(5) As Double
            _uf.Modl.AskBoundingBox(obj.Tag, box)

            If lineOnly Then
                ' 직선인지 알 수 없으므로, 두 축 폭이 0에 가까운(축에 나란한) 경우만 직선으로 인정
                Dim zeroAxes As Integer = 0
                For k = 0 To 2
                    If Math.Abs(box(k + 3) - box(k)) < 0.000001 Then zeroAxes += 1
                Next
                If zeroAxes < 2 Then
                    errors.Add("bounding_box: 축에 나란하지 않아 직선 방향을 알 수 없음")
                    Return False
                End If
            End If

            pts.Add(New Point3d(box(0), box(1), box(2)))
            pts.Add(New Point3d(box(3), box(4), box(5)))
            Diag("  " & obj.GetType().Name & " 끝점: UF_MODL_ask_bounding_box")
            Return True
        Catch ex As Exception
            errors.Add("bounding_box: " & ex.Message)
            pts.Clear()
            Return False
        End Try
    End Function

    ''' <summary>세 점이 한 직선 위에 있는지 (가운데 점의 벗어난 정도로 판단)</summary>
    Private Shared Function IsCollinear(a As Point3d, m As Point3d, b As Point3d) As Boolean
        Dim abx As Double = b.X - a.X, aby As Double = b.Y - a.Y, abz As Double = b.Z - a.Z
        Dim amx As Double = m.X - a.X, amy As Double = m.Y - a.Y, amz As Double = m.Z - a.Z
        Dim cx As Double = aby * amz - abz * amy
        Dim cy As Double = abz * amx - abx * amz
        Dim cz As Double = abx * amy - aby * amx
        Dim lenAb As Double = Math.Sqrt(abx * abx + aby * aby + abz * abz)
        If lenAb < 0.000001 Then Return False
        ' 가운데 점이 직선에서 벗어난 거리 / 길이 가 0.01% 미만이면 직선
        Return Math.Sqrt(cx * cx + cy * cy + cz * cz) / lenAb / lenAb < 0.0001
    End Function

    ''' <summary>
    ''' 객체 좌표 → 시트 좌표.
    ''' 도면 뷰(DraftingView) 안의 객체는 모델 좌표일 수도, 이미 시트 좌표일 수도 있음 (DraftingCurve 등).
    ''' Edge 는 항상 모델 좌표 → 변환. 그 밖의 객체는 두 해석 중 치수(near)에 더 가까운 쪽을 사용.
    ''' </summary>
    Private Function ToSheetPoints(obj As TaggedObject, pts As List(Of Point3d), view As View,
                                   near As Point3d,
                                   Optional edgeIsModel As Boolean = True) As List(Of Double())
        Dim raw As List(Of Double()) = pts.Select(Function(p) New Double() {p.X, p.Y}).ToList()
        If Not TypeOf view Is Drawings.DraftingView Then Return raw

        Dim mapped As New List(Of Double())()
        Try
            For Each p In pts
                Dim model As Double() = {p.X, p.Y, p.Z}
                Dim sheet(1) As Double
                _uf.View.MapModelToDrawing(view.Tag, model, sheet)
                mapped.Add(sheet)
            Next
        Catch ex As Exception
            Return raw
        End Try

        If edgeIsModel AndAlso TypeOf obj Is Edge Then Return mapped

        Dim dRaw As Double = DistToCentroid(raw, near)
        Dim dMapped As Double = DistToCentroid(mapped, near)
        Diag("  " & obj.GetType().Name & " 좌표 해석: 그대로=" & Math.Round(dRaw, 2) &
             ", 모델→시트 변환=" & Math.Round(dMapped, 2) & " (치수까지 거리, 가까운 쪽 사용)")
        Return If(dMapped <= dRaw, mapped, raw)
    End Function

    Private Shared Function DistToCentroid(pts As List(Of Double()), p As Point3d) As Double
        Dim cx As Double = pts.Average(Function(q) q(0))
        Dim cy As Double = pts.Average(Function(q) q(1))
        Return Math.Sqrt((cx - p.X) * (cx - p.X) + (cy - p.Y) * (cy - p.Y))
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
            Dim origin As Point3d = refDim.AnnotationOrigin
            Dim sum As Double = 0
            Dim cnt As Integer = 0
            For Each sel As SelectNXObject In New SelectNXObject() {b.FirstAssociativity, b.SecondAssociativity}
                If sel Is Nothing Then Continue For

                Dim obj As TaggedObject = Nothing
                Dim view As View = Nothing
                Dim pick As Point3d
                sel.GetValue(obj, view, pick)

                Dim pts As List(Of Point3d) = GeometryPoints(obj)
                If pts.Count = 0 Then Continue For

                For Each sp In ToSheetPoints(obj, pts, view, origin)
                    sum += dir.CoordOf(sp(0), sp(1))
                    cnt += 1
                Next
            Next
            If cnt = 0 Then Return 0

            Dim diff As Double = Coord(origin, dir) - sum / cnt
            Diag("  바깥쪽 판정: 치수 좌표=" & Math.Round(Coord(origin, dir), 3) &
                 ", 형상 평균 좌표=" & Math.Round(sum / cnt, 3))
            If Math.Abs(diff) < 0.000001 Then Return 0
            Return Math.Sign(diff)
        Catch ex As Exception
            Diag("  바깥쪽 판정 오류: " & ex.Message)
            Return 0
        Finally
            If b IsNot Nothing Then b.Destroy()
        End Try
    End Function

    ''' <summary>연관 객체의 대표 점들 (곡선/모서리의 양 끝점, 점 좌표). 모르는 객체는 빈 목록.</summary>
    Private Function GeometryPoints(obj As TaggedObject) As List(Of Point3d)
        Dim pt = TryCast(obj, Point)
        If pt IsNot Nothing Then
            Return New List(Of Point3d) From {pt.Coordinates}
        End If
        Return CurveEndPoints(obj, False)
    End Function

    ' ---------------------------------------------------------------- 진단 로그

    Private _diagOpened As Boolean = False

    ''' <summary>DebugLog 가 켜져 있으면 정보 창에 진단 메시지 출력</summary>
    Private Sub Diag(msg As String)
        If Not DebugLog Then Return
        Dim lw As ListingWindow = _session.ListingWindow
        If Not _diagOpened Then
            lw.Open()
            lw.WriteLine("--- 방향/바깥쪽 판정 진단 ---")
            _diagOpened = True
        End If
        lw.WriteLine(msg)
    End Sub

    Private Shared Function Fmt(p As Double()) As String
        Return Math.Round(p(0), 3) & ", " & Math.Round(p(1), 3)
    End Function

    ' ---------------------------------------------------------------- 적용

    ''' <summary>True면 치수별 계산/이동 내역을 정보 창(Listing Window)에 출력 (문제 확인용)</summary>
    Private Const DebugLog As Boolean = True

    Private Sub Apply(refDim As Dimension, refDir As StackDirection, targets As List(Of Dimension),
                      dir As StackDirection, settings As ArrangeSettings,
                      mode As ArrangeMode, modeReason As String)
        Dim refCoord As Double = Coord(refDim.AnnotationOrigin, dir)

        Dim items As New List(Of LayoutItem)(targets.Count)
        For i = 0 To targets.Count - 1
            Dim o As Point3d = targets(i).AnnotationOrigin
            items.Add(New LayoutItem(i, Coord(o, dir) - refCoord, SafeSize(targets(i))))
        Next

        ' 병렬: 모든 치수를 한쪽(형상 바깥쪽)으로 쌓음
        Dim side As Integer = 0
        Dim sideReason As String = "현재 쪽 유지"
        If mode = ArrangeMode.Parallel Then
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
            DimensionLayout.Compute(items, mode, settings.Spacing, settings.Order, settings.RowTolerance, side)

        Dim log As ListingWindow = Nothing
        If DebugLog Then
            log = _session.ListingWindow
            log.Open()
            log.WriteLine("=== " & Title & " ===")
            log.WriteLine("형태=" & mode.ToString() & " (" & modeReason & "), 간격=" & settings.Spacing &
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
        Catch ex As Exception
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
