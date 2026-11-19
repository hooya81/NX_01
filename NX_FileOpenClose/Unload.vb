' Siemens 공식 패턴 (UGOPEN\NXOpenExamples\VB\...\Unload.vb)
Public Class Unload

    Public Shared Function GetUnloadOption(ByVal dummy As String) As Integer
        Dim unloadOption As Integer
        unloadOption = NXOpen.Session.LibraryUnloadOption.Immediately ' 실행 후 즉시 언로드
        'unloadOption = NXOpen.Session.LibraryUnloadOption.AtTermination ' NX 종료 시 언로드
        'unloadOption = NXOpen.Session.LibraryUnloadOption.Explicitly ' 파일 -> 언로드 시에만
        Return unloadOption
    End Function

End Class
