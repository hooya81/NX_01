''' <summary>
''' 실행 환경(외부 EXE = 콘솔, 내부 DLL = Listing Window)에 맞게 출력하기 위한 로거.
''' 기능 코드는 이 인터페이스만 사용하면 양쪽에서 그대로 재사용됩니다.
''' </summary>
Public Interface INxLogger
    Sub Info(message As String)
    Sub Warn(message As String)
    Sub [Error](message As String)
End Interface
