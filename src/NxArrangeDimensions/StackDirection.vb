''' <summary>
''' 치수를 쌓는 방향 (시트 평면의 단위 벡터, 치수선에 수직인 방향).
''' 수평 치수 = (0,1), 수직 치수 = (1,0), 수직거리(Perpendicular) 치수 = 기준선 방향.
''' NX API를 쓰지 않으므로 단독 테스트 가능.
''' </summary>
Public Structure StackDirection

    Public ReadOnly X As Double
    Public ReadOnly Y As Double

    ''' <summary>두 방향을 같은 방향으로 보는 허용 각도(도)</summary>
    Public Const ParallelToleranceDeg As Double = 1.0

    Public Sub New(x As Double, y As Double)
        Dim len As Double = Math.Sqrt(x * x + y * y)
        If len < 0.000000001 OrElse Double.IsNaN(len) OrElse Double.IsInfinity(len) Then
            Me.X = 0
            Me.Y = 0
        Else
            ' 부호 정규화: 같은 방향의 두 벡터가 반대 부호로 나와도 결과가 같도록 (Y>0, Y=0이면 X>0)
            Dim nx As Double = x / len
            Dim ny As Double = y / len
            If ny < -0.000000001 OrElse (Math.Abs(ny) <= 0.000000001 AndAlso nx < 0) Then
                nx = -nx
                ny = -ny
            End If
            Me.X = nx
            Me.Y = ny
        End If
    End Sub

    Public Shared ReadOnly Property None As StackDirection
        Get
            Return New StackDirection(0, 0)
        End Get
    End Property

    ''' <summary>좌/우로 쌓기 (수직 측정 치수)</summary>
    Public Shared ReadOnly Property AlongX As StackDirection
        Get
            Return New StackDirection(1, 0)
        End Get
    End Property

    ''' <summary>위/아래로 쌓기 (수평 측정 치수)</summary>
    Public Shared ReadOnly Property AlongY As StackDirection
        Get
            Return New StackDirection(0, 1)
        End Get
    End Property

    Public Shared Function FromAxis(axis As StackAxis) As StackDirection
        Select Case axis
            Case StackAxis.X : Return AlongX
            Case StackAxis.Y : Return AlongY
            Case Else : Return None
        End Select
    End Function

    Public ReadOnly Property IsValid As Boolean
        Get
            Return X <> 0 OrElse Y <> 0
        End Get
    End Property

    ''' <summary>같은 방향(또는 정반대) 인지</summary>
    Public Function IsParallelTo(other As StackDirection) As Boolean
        If Not IsValid OrElse Not other.IsValid Then Return False
        Dim cross As Double = Math.Abs(X * other.Y - Y * other.X)
        Return cross <= Math.Sin(ParallelToleranceDeg * Math.PI / 180.0)
    End Function

    ''' <summary>점의 이 방향 성분 (쌓는 방향 좌표)</summary>
    Public Function CoordOf(px As Double, py As Double) As Double
        Return px * X + py * Y
    End Function

    ''' <summary>쌓는 방향과 이루는 각도(도), 로그용</summary>
    Public Function AngleDeg() As Double
        Return Math.Atan2(Y, X) * 180.0 / Math.PI
    End Function

    Public Overrides Function ToString() As String
        If Not IsValid Then Return "None"
        If IsParallelTo(AlongY) Then Return "Y(위/아래)"
        If IsParallelTo(AlongX) Then Return "X(좌/우)"
        Return "각도 " & Math.Round(AngleDeg(), 2) & "°"
    End Function

End Structure
