Imports NXOpen

''' <summary>내부 DLL 용: NX Listing Window + syslog 에 출력</summary>
Public Class ListingWindowLogger
    Implements INxLogger

    Private ReadOnly _lw As ListingWindow
    Private ReadOnly _writeSyslog As Boolean

    Public Sub New(Optional writeSyslog As Boolean = True)
        _lw = NxSession.TheSession.ListingWindow
        _writeSyslog = writeSyslog
        If Not _lw.IsOpen Then _lw.Open()
    End Sub

    Public Sub Info(message As String) Implements INxLogger.Info
        Write(message)
    End Sub

    Public Sub Warn(message As String) Implements INxLogger.Warn
        Write("[WARN] " & message)
    End Sub

    Public Sub [Error](message As String) Implements INxLogger.Error
        Write("[ERROR] " & message)
    End Sub

    Private Sub Write(message As String)
        _lw.WriteLine(message)
        If _writeSyslog Then NxSession.TheSession.LogFile.WriteLine(message)
    End Sub
End Class
