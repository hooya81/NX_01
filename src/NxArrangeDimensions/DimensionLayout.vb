' 네임스페이스는 프로젝트의 RootNamespace(NxArrangeDimensions)를 사용

''' <summary>배치 형태</summary>
Public Enum ArrangeMode
    ''' <summary>직렬: 모든 치수를 기준치수와 같은 줄에 정렬</summary>
    Serial = 0

    ''' <summary>병렬: 치수마다 한 줄씩, 기준치수에서 간격만큼 쌓음</summary>
    Parallel = 1

    ''' <summary>혼합: 이미 같은 줄에 있는 치수(체인)는 묶어서 쌓음</summary>
    Mixed = 2
End Enum

''' <summary>쌓는 순서</summary>
Public Enum ArrangeOrder
    ''' <summary>현재 위치 순서 유지 (기준치수에 가까운 것이 안쪽)</summary>
    KeepCurrent = 0

    ''' <summary>치수값이 작은 것이 안쪽, 큰 것이 바깥쪽</summary>
    BySizeAscending = 1
End Enum

''' <summary>배치 계산 입력 1건 (NX 객체와 무관한 순수 데이터)</summary>
Public NotInheritable Class LayoutItem
    Public Sub New(id As Integer, offset As Double, size As Double)
        Me.Id = id
        Me.Offset = offset
        Me.Size = size
    End Sub

    ''' <summary>호출측 식별자 (목록 인덱스 등)</summary>
    Public ReadOnly Property Id As Integer

    ''' <summary>기준치수 원점으로부터의 현재 거리 (쌓는 축 방향, 부호 포함)</summary>
    Public ReadOnly Property Offset As Double

    ''' <summary>치수값 (정렬 기준)</summary>
    Public ReadOnly Property Size As Double
End Class

''' <summary>
''' 기준치수를 기준으로 치수 위치를 계산.
''' NX API를 쓰지 않으므로 단독 테스트 가능.
''' </summary>
Public Module DimensionLayout

    ''' <summary>
    ''' 각 치수의 새 오프셋(기준치수 원점 기준)을 계산한다.
    ''' - 직렬: 모든 치수의 오프셋을 0(기준치수 줄)으로.
    ''' - 병렬: 현재 있는 쪽(+/-)을 유지한 채 치수마다 한 줄씩 spacing 간격으로 쌓음.
    '''         기준치수와 거의 같은 줄에 있던 치수는 치수가 더 많은 쪽으로 보냄.
    ''' - 혼합: 기준치수 rowTolerance 이내는 기준 줄에 맞추고,
    '''         서로 rowTolerance 이내인 치수는 같은 줄(체인)로 묶어 쌓음.
    ''' </summary>
    Public Function Compute(items As IEnumerable(Of LayoutItem), mode As ArrangeMode,
                            spacing As Double, order As ArrangeOrder,
                            rowTolerance As Double) As IDictionary(Of Integer, Double)
        If items Is Nothing Then Throw New ArgumentNullException(NameOf(items))
        If spacing <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(spacing), "간격은 0보다 커야 합니다.")
        If rowTolerance < 0 Then Throw New ArgumentOutOfRangeException(NameOf(rowTolerance))

        Dim list = items.ToList()
        Dim result As New Dictionary(Of Integer, Double)()

        If mode = ArrangeMode.Serial Then
            For Each it In list
                result(it.Id) = 0.0
            Next
            Return result
        End If

        Dim pos = list.Where(Function(i) i.Offset > rowTolerance).ToList()
        Dim neg = list.Where(Function(i) i.Offset < -rowTolerance).ToList()
        Dim near = list.Where(Function(i) Math.Abs(i.Offset) <= rowTolerance).ToList()

        If mode = ArrangeMode.Parallel Then
            ' 기준 줄 위의 치수도 한 줄씩 차지해야 하므로 치수가 많은 쪽(동수면 +)에 합침
            If neg.Count > pos.Count Then neg.AddRange(near) Else pos.AddRange(near)
            ArrangeSide(pos, +1, spacing, order, 0.0, result)
            ArrangeSide(neg, -1, spacing, order, 0.0, result)
        Else
            For Each it In near
                result(it.Id) = 0.0
            Next
            ArrangeSide(pos, +1, spacing, order, rowTolerance, result)
            ArrangeSide(neg, -1, spacing, order, rowTolerance, result)
        End If

        Return result
    End Function

    Private Sub ArrangeSide(side As IEnumerable(Of LayoutItem), sign As Integer, spacing As Double,
                            order As ArrangeOrder, rowTolerance As Double,
                            result As IDictionary(Of Integer, Double))
        ' 1) 현재 거리순으로 정렬 후, 가까이 붙어있는 것끼리 한 줄로 묶음 (rowTolerance = 0 이면 묶지 않음)
        Dim rows As New List(Of List(Of LayoutItem))()
        For Each it In side.OrderBy(Function(i) Math.Abs(i.Offset))
            Dim last As List(Of LayoutItem) = If(rows.Count > 0, rows(rows.Count - 1), Nothing)
            If last IsNot Nothing AndAlso rowTolerance > 0 AndAlso
               Math.Abs(it.Offset) - Math.Abs(last(0).Offset) <= rowTolerance Then
                last.Add(it)
            Else
                rows.Add(New List(Of LayoutItem) From {it})
            End If
        Next

        ' 2) 줄 순서 결정
        If order = ArrangeOrder.BySizeAscending Then
            rows = rows.OrderBy(Function(r) r.Max(Function(i) Math.Abs(i.Size))) _
                       .ThenBy(Function(r) Math.Abs(r(0).Offset)) _
                       .ToList()
        End If

        ' 3) 간격 적용
        For k = 0 To rows.Count - 1
            Dim newOffset As Double = sign * (k + 1) * spacing
            For Each it In rows(k)
                result(it.Id) = newOffset
            Next
        Next
    End Sub

End Module
