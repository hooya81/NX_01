Imports NXOpen
Imports NxCommon

''' <summary>
''' 내부 DLL 진입점. NX 안에서 실행됩니다.
'''   - File > Execute > NX Open... 에서 NxInternal.dll 선택
'''   - 또는 메뉴/리본(.men/.rtb)의 ACTIONS 에 DLL 경로 지정
''' </summary>
Public Module EntryPoint

    ''' <summary>NX 가 호출하는 메인 함수</summary>
    Public Sub Main(args As String())
        Dim logger As INxLogger = Nothing
        Try
            logger = New ListingWindowLogger()

            ' 인자로 명령 이름이 오면 해당 명령, 없으면 "info"
            Dim commandName = If(args IsNot Nothing AndAlso args.Length > 0, args(0), "info")
            Dim command = NxCommandRegistry.Find(commandName)
            If command Is Nothing Then
                ShowError($"알 수 없는 명령: {commandName}")
                Return
            End If

            command.Execute(logger)

        Catch ex As NXException
            ShowError($"NX 오류 ({ex.ErrorCode}){Environment.NewLine}{ex.Message}")
        Catch ex As Exception
            ShowError(ex.ToString())
        End Try
    End Sub

    ''' <summary>
    ''' DLL 언로드 시점. 개발 중에는 Immediately 로 두면 DLL 재빌드 후 NX 재시작 없이 다시 실행 가능.
    ''' 메뉴/콜백을 등록해 상주시키려면 AtTermination 으로 변경하세요.
    ''' </summary>
    Public Function GetUnloadOption(arg As String) As Integer
        Return CInt(Session.LibraryUnloadOption.Immediately)
    End Function

    Private Sub ShowError(message As String)
        UI.GetUI().NXMessageBox.Show("NxInternal", NXMessageBox.DialogType.Error, message)
    End Sub

End Module
