Imports NXOpen

''' <summary>NX Open 진입점 (File > Execute > NX Open 또는 메뉴/리본 버튼에서 호출)</summary>
Public Module Program

    Public Sub Main(args As String())
        ' VB는 대소문자를 구분하지 않으므로 변수명을 형식명(Session/UI)과 다르게 둠
        Dim theSession As Session = Session.GetSession()
        Dim theUI As UI = UI.GetUI()

        Try
            Dim cmd As New ArrangeDimensionsCommand(theSession, theUI)
            cmd.Run()
        Catch ex As NXException
            theUI.NXMessageBox.Show("Arrange Dimensions", NXMessageBox.DialogType.Error,
                "NX 오류: " & ex.Message & " (" & ex.ErrorCode & ")")
        Catch ex As Exception
            theUI.NXMessageBox.Show("Arrange Dimensions", NXMessageBox.DialogType.Error,
                "오류: " & ex.Message & vbLf & vbLf & ex.StackTrace)
        End Try
    End Sub

    ''' <summary>실행 후 DLL 즉시 언로드 (개발 중 재빌드 편의)</summary>
    Public Function GetUnloadOption(arg As String) As Integer
        Return CInt(Session.LibraryUnloadOption.Immediately)
    End Function

End Module
