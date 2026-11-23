Imports NXOpen

''' <summary>
''' 현재 NX 세션의 기본 정보 스냅샷.
''' </summary>
Public Class NxSessionInfo

    Public Property UgiiBaseDir As String
    Public Property UgiiVersion As String
    Public Property UserName As String
    Public Property MachineName As String
    Public Property IsTeamcenterMode As Boolean
    Public Property LoadedPartCount As Integer
    Public Property WorkPart As String
    Public Property DisplayPart As String
    Public Property DisplayPartUnits As String
    Public Property LoadedParts As New List(Of String)

    ''' <summary>현재 세션에서 정보를 수집합니다.</summary>
    Public Shared Function Collect() As NxSessionInfo
        Dim s As NXOpen.Session = NxSession.TheSession
        Dim info As New NxSessionInfo With {
            .UgiiBaseDir = EnvOrEmpty("UGII_BASE_DIR"),
            .UgiiVersion = EnvOrEmpty("UGII_VERSION"),
            .UserName = Environment.UserName,
            .MachineName = Environment.MachineName,
            .IsTeamcenterMode = NxSession.IsTeamcenterMode,
            .LoadedPartCount = NxSession.TheUFSession.Part.AskNumParts()
        }

        Dim work As Part = s.Parts.Work
        Dim disp As Part = s.Parts.Display
        info.WorkPart = If(work Is Nothing, "(없음)", Describe(work))
        info.DisplayPart = If(disp Is Nothing, "(없음)", Describe(disp))
        info.DisplayPartUnits = If(disp Is Nothing, "-", disp.PartUnits.ToString())

        For Each p As BasePart In s.Parts
            info.LoadedParts.Add(Describe(p))
        Next

        Return info
    End Function

    ''' <summary>출력용 문자열 목록</summary>
    Public Function ToLines() As IList(Of String)
        Dim lines As New List(Of String) From {
            "========== NX Session Info ==========",
            $"UGII_BASE_DIR     : {UgiiBaseDir}",
            $"UGII_VERSION      : {UgiiVersion}",
            $"User / Machine    : {UserName} @ {MachineName}",
            $"Teamcenter Mode   : {IsTeamcenterMode}",
            $"Work Part         : {WorkPart}",
            $"Display Part      : {DisplayPart}",
            $"Display Units     : {DisplayPartUnits}",
            $"Loaded Parts (UF) : {LoadedPartCount}"
        }
        For Each p In LoadedParts
            lines.Add($"   - {p}")
        Next
        lines.Add("=====================================")
        Return lines
    End Function

    Private Shared Function Describe(p As BasePart) As String
        Return $"{p.Leaf}  [{p.FullPath}]"
    End Function

    Private Shared Function EnvOrEmpty(name As String) As String
        Dim v = Environment.GetEnvironmentVariable(name)
        Return If(String.IsNullOrEmpty(v), "(미설정)", v)
    End Function

End Class
