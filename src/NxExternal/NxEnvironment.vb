Imports System.IO
Imports System.Reflection

''' <summary>
''' 외부 EXE 가 NX 설치 폴더의 NXOpen 어셈블리/네이티브 DLL 을 찾을 수 있도록 환경을 준비합니다.
''' 주의: 이 클래스는 NXOpen 타입을 절대 참조하면 안 됩니다. (어셈블리 해석기 등록 전이므로)
''' </summary>
Friend NotInheritable Class NxEnvironment

    Private Const DefaultBaseDir As String = "C:\Program Files\Siemens\NX2606"

    Private Shared _baseDir As String
    Private Shared _searchDirs As String()

    Private Sub New()
    End Sub

    Public Shared ReadOnly Property BaseDir As String
        Get
            Return _baseDir
        End Get
    End Property

    ''' <summary>
    ''' UGII_BASE_DIR 확인 → PATH 에 NXBIN 추가 → AssemblyResolve 등록.
    ''' NXOpen 을 사용하는 어떤 코드보다 먼저 호출해야 합니다.
    ''' </summary>
    Public Shared Sub Initialize()
        _baseDir = Environment.GetEnvironmentVariable("UGII_BASE_DIR")
        If String.IsNullOrWhiteSpace(_baseDir) Then
            _baseDir = DefaultBaseDir
            Environment.SetEnvironmentVariable("UGII_BASE_DIR", _baseDir)
        End If
        _baseDir = _baseDir.TrimEnd("\"c, "/"c)

        Dim nxBin = Path.Combine(_baseDir, "NXBIN")
        Dim managed = Path.Combine(nxBin, "managed")

        If Not File.Exists(Path.Combine(managed, "NXOpen.dll")) Then
            Throw New DirectoryNotFoundException(
                $"NXOpen.dll 을 찾을 수 없습니다. UGII_BASE_DIR 를 확인하세요: {_baseDir}")
        End If

        ' 네이티브 NX DLL 로드를 위해 NXBIN 을 PATH 앞에 추가
        Dim pathVar = If(Environment.GetEnvironmentVariable("PATH"), String.Empty)
        If pathVar.IndexOf(nxBin, StringComparison.OrdinalIgnoreCase) < 0 Then
            Environment.SetEnvironmentVariable("PATH", nxBin & ";" & pathVar)
        End If

        _searchDirs = {managed, nxBin}
        AddHandler AppDomain.CurrentDomain.AssemblyResolve, AddressOf OnAssemblyResolve
    End Sub

    Private Shared Function OnAssemblyResolve(sender As Object, e As ResolveEventArgs) As Assembly
        Dim simpleName = New AssemblyName(e.Name).Name
        For Each searchDir In _searchDirs
            Dim candidate = Path.Combine(searchDir, simpleName & ".dll")
            If File.Exists(candidate) Then
                Return Assembly.LoadFrom(candidate)
            End If
        Next
        Return Nothing
    End Function

End Class
