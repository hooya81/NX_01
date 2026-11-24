Imports System.Globalization
Imports System.IO

''' <summary>사용자 입력값. 마지막 값을 %APPDATA%에 저장해 다음 실행 때 기본값으로 사용.</summary>
Public NotInheritable Class ArrangeSettings

    Public Property Spacing As Double = 10.0
    Public Property Mode As ArrangeMode = ArrangeMode.Auto
    Public Property Order As ArrangeOrder = ArrangeOrder.BySizeAscending

    ''' <summary>병렬일 때 쌓는 쪽: 0 = 자동(형상 바깥쪽), +1 = 위/오른쪽, -1 = 아래/왼쪽</summary>
    Public Property Side As Integer = 0

    ''' <summary>기준치수 방향을 판정하지 못했을 때 사용자가 고른 방향 (저장 안 함)</summary>
    Public Property Axis As StackAxis = StackAxis.Y

    ''' <summary>같은 줄로 간주하는 허용 거리 = Spacing × 이 비율</summary>
    Public Const RowToleranceRatio As Double = 0.25

    Public ReadOnly Property RowTolerance As Double
        Get
            Return Spacing * RowToleranceRatio
        End Get
    End Property

    Private Shared ReadOnly Property FilePath As String
        Get
            Return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NxArrangeDimensions", "settings.txt")
        End Get
    End Property

    Public Shared Function Load() As ArrangeSettings
        Dim s As New ArrangeSettings()
        Try
            If Not File.Exists(FilePath) Then Return s
            For Each line In File.ReadAllLines(FilePath)
                Dim p = line.Split(New Char() {"="c}, 2)
                If p.Length <> 2 Then Continue For
                Dim key = p(0).Trim()
                Dim val = p(1).Trim()

                Dim d As Double
                Dim n As Integer
                If key = "Spacing" AndAlso Double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, d) AndAlso d > 0 Then
                    s.Spacing = d
                ElseIf key = "Mode" AndAlso Integer.TryParse(val, n) AndAlso [Enum].IsDefined(GetType(ArrangeMode), n) Then
                    s.Mode = CType(n, ArrangeMode)
                ElseIf key = "Side" AndAlso Integer.TryParse(val, n) AndAlso n >= -1 AndAlso n <= 1 Then
                    s.Side = n
                ElseIf key = "Order" AndAlso Integer.TryParse(val, n) AndAlso [Enum].IsDefined(GetType(ArrangeOrder), n) Then
                    s.Order = CType(n, ArrangeOrder)
                End If
            Next
        Catch
            ' 설정 파일 문제는 무시하고 기본값 사용
        End Try
        Return s
    End Function

    Public Sub Save()
        Try
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath))
            File.WriteAllLines(FilePath, New String() {
                "Spacing=" & Spacing.ToString(CultureInfo.InvariantCulture),
                "Mode=" & CInt(Mode).ToString(CultureInfo.InvariantCulture),
                "Order=" & CInt(Order).ToString(CultureInfo.InvariantCulture),
                "Side=" & Side.ToString(CultureInfo.InvariantCulture)
            })
        Catch
            ' 저장 실패는 기능에 영향 없음
        End Try
    End Sub

End Class
