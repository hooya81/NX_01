Imports NXOpen
Imports NXOpen.UF

''' <summary>
''' NX 세션 접근용 공용 진입점.
''' 외부 EXE 에서 처음 호출하면 NX 가 배치 모드로 초기화되고,
''' 내부 DLL 에서는 현재 실행 중인 NX 세션을 그대로 돌려줍니다.
''' </summary>
Public NotInheritable Class NxSession

    Private Shared _theSession As NXOpen.Session
    Private Shared _theUFSession As UFSession

    Private Sub New()
    End Sub

    ''' <summary>NXOpen Session</summary>
    Public Shared ReadOnly Property TheSession As NXOpen.Session
        Get
            If _theSession Is Nothing Then
                _theSession = NXOpen.Session.GetSession()
            End If
            Return _theSession
        End Get
    End Property

    ''' <summary>User Function(UF) Session</summary>
    Public Shared ReadOnly Property TheUFSession As UFSession
        Get
            If _theUFSession Is Nothing Then
                _theUFSession = UFSession.GetUFSession()
            End If
            Return _theUFSession
        End Get
    End Property

    ''' <summary>현재 Work 파트 (없으면 Nothing)</summary>
    Public Shared ReadOnly Property WorkPart As Part
        Get
            Return TheSession.Parts.Work
        End Get
    End Property

    ''' <summary>현재 Display 파트 (없으면 Nothing)</summary>
    Public Shared ReadOnly Property DisplayPart As Part
        Get
            Return TheSession.Parts.Display
        End Get
    End Property

    ''' <summary>Teamcenter(NX Manager) 모드 여부</summary>
    Public Shared ReadOnly Property IsTeamcenterMode As Boolean
        Get
            Dim active As Boolean = False
            TheUFSession.UF.IsUgmanagerActive(active)
            Return active
        End Get
    End Property

    ''' <summary>
    ''' 파트를 열고 Display 파트로 설정합니다.
    ''' 네이티브: "C:\work\a.prt" / Teamcenter: "@DB/000123/A"
    ''' </summary>
    Public Shared Function OpenPart(partPath As String) As BasePart
        Dim loadStatus As PartLoadStatus = Nothing
        Dim opened As BasePart = TheSession.Parts.OpenBaseDisplay(partPath, loadStatus)
        Try
            If loadStatus IsNot Nothing AndAlso loadStatus.NumberUnloadedParts > 0 Then
                Throw New InvalidOperationException(
                    $"'{partPath}' 를 여는 중 {loadStatus.NumberUnloadedParts} 개의 하위 파트를 로드하지 못했습니다.")
            End If
        Finally
            loadStatus?.Dispose()
        End Try
        Return opened
    End Function

    ''' <summary>열려 있는 모든 파트를 저장하지 않고 닫습니다. (주로 외부 EXE 종료 시)</summary>
    Public Shared Sub CloseAllParts()
        TheSession.Parts.CloseAll(BasePart.CloseModified.CloseModified, Nothing)
    End Sub

End Class
