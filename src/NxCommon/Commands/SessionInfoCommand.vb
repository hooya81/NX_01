''' <summary>세션 기본 정보 출력 (샘플 명령)</summary>
Public Class SessionInfoCommand
    Implements INxCommand

    Public ReadOnly Property Name As String Implements INxCommand.Name
        Get
            Return "info"
        End Get
    End Property

    Public Sub Execute(logger As INxLogger) Implements INxCommand.Execute
        Dim info = NxSessionInfo.Collect()
        For Each line In info.ToLines()
            logger.Info(line)
        Next
    End Sub
End Class
