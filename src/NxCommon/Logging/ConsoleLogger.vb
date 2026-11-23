''' <summary>외부 EXE 용: 콘솔에 출력</summary>
Public Class ConsoleLogger
    Implements INxLogger

    Public Sub Info(message As String) Implements INxLogger.Info
        Write("INFO ", message, Console.Out)
    End Sub

    Public Sub Warn(message As String) Implements INxLogger.Warn
        Write("WARN ", message, Console.Out)
    End Sub

    Public Sub [Error](message As String) Implements INxLogger.Error
        Write("ERROR", message, Console.Error)
    End Sub

    Private Shared Sub Write(level As String, message As String, writer As IO.TextWriter)
        writer.WriteLine($"[{DateTime.Now:HH:mm:ss}] {level} {message}")
    End Sub
End Class
