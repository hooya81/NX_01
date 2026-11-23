''' <summary>
''' 사용 가능한 명령 목록. 새 명령을 만들면 All() 에 한 줄 추가하세요.
''' </summary>
Public NotInheritable Class NxCommandRegistry

    Private Sub New()
    End Sub

    Public Shared Function All() As IList(Of INxCommand)
        Return New List(Of INxCommand) From {
            New SessionInfoCommand()
        }
    End Function

    Public Shared Function Find(name As String) As INxCommand
        Return All().FirstOrDefault(
            Function(c) String.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
    End Function

End Class
