# NX_01

Siemens **NX 2606** / **VB.NET** / **.NET Framework 4.8** NX Open 개발용 샘플 솔루션.

- **NxExternal.exe** : 외부 실행 프로그램 (NX 를 배치 모드로 기동)
- **NxInternal.dll** : NX 내부에서 실행되는 DLL (File > Execute > NX Open...)
- **NxCommon.dll**   : 두 프로젝트가 같이 쓰는 공용 기능 (세션 접근, 로거, 명령)

## 폴더 구조

```
NX_01.sln
Directory.Build.props      공통 설정 (NX 경로, net48, x64, Option Strict 등)
Directory.Build.targets    NXOpen 참조 / 선택적 서명(SignDotNet)
src/
  NxCommon/
    NxSession.vb           Session / UFSession / WorkPart / DisplayPart / OpenPart / CloseAllParts
    NxSessionInfo.vb       세션 기본 정보 수집
    Logging/               INxLogger, ConsoleLogger(외부), ListingWindowLogger(내부)
    Commands/              INxCommand, SessionInfoCommand, NxCommandRegistry
  NxExternal/
    Program.vb             EXE 진입점
    NxEnvironment.vb       UGII_BASE_DIR / PATH / NXOpen 어셈블리 해석
  NxInternal/
    Main.vb                Main / GetUnloadOption
```

## 빌드

1. Visual Studio 2022 로 `NX_01.sln` 열기
2. NX 경로는 아래 순서로 찾습니다.
   1. MSBuild 속성 `NXBaseDir` (`msbuild /p:NXBaseDir="D:\Siemens\NX2606"`)
   2. 환경변수 `UGII_BASE_DIR`
   3. 기본값 `C:\Program Files\Siemens\NX2606`
3. 참조: `NXOpen.dll`, `NXOpen.UF.dll`, `NXOpen.Utilities.dll`, (내부만) `NXOpenUI.dll`
   — 모두 `NXBIN\managed` 에서 참조하고 **Copy Local = False** 입니다.
4. 플랫폼은 **x64** 고정입니다.

> NX 2606 의 .NET Framework 버전이 다르다면 `Directory.Build.props` 의 `TargetFramework` 만 바꾸면 됩니다.

### 서명

`dotnet_author` 라이선스가 없는 PC 에서 실행하려면 NX 서명이 필요합니다.

```
msbuild NX_01.sln /p:Configuration=Release /p:NXSignOnBuild=true
```

`NXBIN\SignDotNet.exe` 로 빌드 결과물을 서명합니다. (NxInternal.dll 과 함께 NxCommon.dll 도 서명됨)

## 실행

### 외부 EXE (NxExternal.exe)

```
NxExternal.exe                       → 세션 정보 출력 (info)
NxExternal.exe info C:\work\a.prt    → 파트를 열고 세션 정보 출력
NxExternal.exe info @DB/000123/A     → Teamcenter 파트
```

- 실행 시 `UGII_BASE_DIR` 이 없으면 기본 경로를 사용하고, `NXBIN` 을 `PATH` 에 추가한 뒤
  `NXBIN\managed` 에서 NXOpen 어셈블리를 찾도록 `AssemblyResolve` 를 등록합니다.
  → EXE 를 NX 폴더에 복사할 필요가 없습니다.
- NXOpen 타입은 `NxEnvironment.Initialize()` 이후 호출되는 `Run()` 에서만 사용해야 합니다.
  (`Main` 에 NXOpen 타입을 직접 쓰면 해석기 등록 전에 로드되어 실패합니다.)
- NX 라이선스가 필요하며, Teamcenter 환경이면 NX 명령창 환경(`UGII_UGMGR_*` 등)에서 실행하세요.
- 종료 시 열린 파트는 저장 없이 모두 닫습니다.

### 내부 DLL (NxInternal.dll)

- NX 에서 **File > Execute > NX Open...** (Ctrl+U) → `NxInternal.dll` 선택
- 결과는 Listing Window 와 syslog 에 출력됩니다.
- `GetUnloadOption` 이 `Immediately` 이므로 실행 후 바로 언로드됩니다.
  → DLL 을 다시 빌드한 뒤 NX 재시작 없이 다시 실행할 수 있습니다.
- 디버깅: Visual Studio **Debug > Attach to Process** → `ugraf.exe` (Managed 코드)
- `NxCommon.dll` 은 반드시 `NxInternal.dll` 과 같은 폴더에 있어야 합니다.

## 기능 추가 방법

1. `src/NxCommon/Commands/` 에 `INxCommand` 구현 클래스를 추가

   ```vb
   Public Class MyFeatureCommand
       Implements INxCommand

       Public ReadOnly Property Name As String Implements INxCommand.Name
           Get
               Return "myfeature"
           End Get
       End Property

       Public Sub Execute(logger As INxLogger) Implements INxCommand.Execute
           Dim workPart = NxSession.WorkPart
           ' ... NX 작업 ...
           logger.Info("완료")
       End Sub
   End Class
   ```

2. `NxCommandRegistry.All()` 에 `New MyFeatureCommand()` 한 줄 추가
3. 외부: `NxExternal.exe myfeature C:\work\a.prt`
   내부: `NxInternal` 에서 `NxCommandRegistry.Find("myfeature")` 로 호출 (메뉴/블록스타일러 등)

출력은 `INxLogger` 로만 하면 외부(콘솔) / 내부(Listing Window) 어디서든 그대로 동작합니다.
UI(블록스타일러, 선택 다이얼로그 등)가 필요한 기능은 `NxInternal` 프로젝트에 두세요.
