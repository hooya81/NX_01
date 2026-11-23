Imports System.Runtime.CompilerServices
Imports System.Text
Imports NxCommon

''' <summary>
''' 외부 실행 EXE 진입점.
'''
''' 사용법:
'''   NxExternal.exe [명령] [파트경로]
'''     명령    : 생략 시 "info"
'''     파트경로 : 지정 시 먼저 열고 명령 실행 (예: C:\work\a.prt, @DB/000123/A)
''' </summary>
Friend Module Program

    Public Function Main(args As String()) As Integer
        Console.OutputEncoding = Encoding.UTF8

        ' 1) NX 환경 준비 (NXOpen 타입 사용 전에 반드시 먼저)
        Try
            NxEnvironment.Initialize()
        Catch ex As Exception
            Console.Error.WriteLine("[환경 오류] " & ex.Message)
            Return 2
        End Try

        ' 2) 실제 작업 (NXOpen 참조는 별도 메서드에서만)
        Try
            Return Run(args)
        Catch ex As IO.FileNotFoundException
            Console.Error.WriteLine("[로드 오류] NX 어셈블리를 불러오지 못했습니다: " & ex.Message)
            Return 2
        End Try
    End Function

    <MethodImpl(MethodImplOptions.NoInlining)>
    Private Function Run(args As String()) As Integer
        Dim logger As INxLogger = New ConsoleLogger()
        Dim commandName = If(args.Length > 0, args(0), "info")
        Dim partPath = If(args.Length > 1, args(1), Nothing)

        Dim command = NxCommandRegistry.Find(commandName)
        If command Is Nothing Then
            logger.Error($"알 수 없는 명령: {commandName}")
            logger.Info("사용 가능한 명령: " & String.Join(", ", NxCommandRegistry.All().Select(Function(c) c.Name)))
            Return 1
        End If

        Try
            logger.Info($"NX 세션 연결 중... (UGII_BASE_DIR={NxEnvironment.BaseDir})")
            Dim s = NxSession.TheSession
            logger.Info("NX 세션 연결 완료")

            If Not String.IsNullOrEmpty(partPath) Then
                logger.Info($"파트 열기: {partPath}")
                NxSession.OpenPart(partPath)
            End If

            command.Execute(logger)
            Return 0

        Catch ex As NXOpen.NXException
            logger.Error($"NX 오류 ({ex.ErrorCode}): {ex.Message}")
            Return 1
        Catch ex As Exception
            logger.Error(ex.ToString())
            Return 1
        Finally
            Try
                NxSession.CloseAllParts()
            Catch
                ' 세션 초기화 실패 등으로 닫을 파트가 없을 수 있음
            End Try
        End Try
    End Function

End Module
