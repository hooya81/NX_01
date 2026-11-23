''' <summary>
''' 기능 단위 인터페이스. 새 기능은 이 인터페이스를 구현한 클래스로 추가하고
''' 외부 EXE / 내부 DLL 진입점에서 호출하면 됩니다.
''' </summary>
Public Interface INxCommand
    ''' <summary>명령 이름 (외부 EXE 인자 등에서 사용)</summary>
    ReadOnly Property Name As String

    ''' <summary>명령 실행</summary>
    Sub Execute(logger As INxLogger)
End Interface
