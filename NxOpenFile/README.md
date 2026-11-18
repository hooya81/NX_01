# NxOpenFile: 실행 중인 NX에 파일 열기 (C# WinForms EXE)

`NxOpenFile.exe`에서 버튼을 눌러 파일을 고르면, **이미 실행 중인 NX**에서 그 파일이 열립니다.

## 왜 DLL이 하나 더 있나요?

NX Open은 외부 EXE가 **이미 떠 있는 NX 화면 세션에 직접 붙는 기능을 제공하지 않습니다.**
외부 EXE에서 `Session.GetSession()`을 호출하면 화면 없는 새 NX(batch) 세션이 시작될 뿐, 지금 보고 있는 NX에는 아무 변화가 없습니다.

그래서 아래처럼 두 부분으로 나눴습니다.

```
NxOpenFile.exe (WinForms)                     실행 중인 NX
 └─ 버튼 → 파일 선택 → Named Pipe ──────▶ NxOpenFile.NxBridge.dll (NX 안에서 대기)
                                               └─ Session.Parts.OpenBaseDisplay(경로)
```

- **NxOpenFile.App** → `NxOpenFile.exe`: 사용자가 실행하는 프로그램이고 NXOpen을 참조하지 않습니다.
- **NxOpenFile.NxBridge** → `NxOpenFile.NxBridge.dll`: NX 안에서 한 번 실행해 두면 NX가 종료될 때까지 EXE의 요청을 기다립니다.
- 통신: 로컬 Named Pipe입니다. 같은 Windows 사용자, 같은 로그온 세션에서만 접속됩니다. 네트워크 포트는 열지 않습니다.

## 프로젝트 구성

| 경로 | 설명 |
|---|---|
| `NxOpenFile.sln` | 솔루션 (플랫폼 `x64`) |
| `NxOpenFile.App/MainForm.cs` | 버튼, 파일 선택, NX에 열기 요청 |
| `NxOpenFile.App/NxBridgeClient.cs` | Named Pipe 클라이언트 |
| `NxOpenFile.NxBridge/BridgeEntry.cs` | NX 진입점 `Main` / `Startup` / `GetUnloadOption`, 파이프 서버, 파일 열기 |
| `NxOpenFile.NxBridge/BridgeProtocol.cs` | 파이프 이름과 명령 규약 (EXE 프로젝트에 링크로 공유) |

두 프로젝트 모두 `net48` / `x64`입니다. NX Open .NET이 .NET Framework 기반이고, NX가 설치된 PC라면 EXE를 추가 런타임 설치 없이 실행할 수 있습니다.

## 빌드 (Visual Studio 2026)

1. 환경변수 `UGII_BASE_DIR`을 NX 설치 경로로 설정합니다. 예: `C:\Program Files\Siemens\NX2606`
   - 브리지 프로젝트가 `$(UGII_BASE_DIR)\NXBIN\managed\NXOpen.dll`, `NXOpenUI.dll`을 참조합니다.
   - 설정 후 Visual Studio를 다시 시작하세요.
2. `NxOpenFile.sln`을 열고 구성 `Release | x64`로 빌드합니다.
3. 결과:
   - `NxOpenFile.App\bin\x64\Release\NxOpenFile.exe`
   - `NxOpenFile.NxBridge\bin\x64\Release\NxOpenFile.NxBridge.dll`

## 사용 방법

1. NX를 실행합니다 (이미 실행 중이면 그대로 둡니다).
2. NX에서 `파일 → 실행 → NX Open` (Ctrl+U)을 열고 `NxOpenFile.NxBridge.dll`을 선택합니다.
   - "NX 파일 열기 브리지가 시작되었습니다" 메시지가 뜨면 준비 완료입니다.
   - NX를 실행할 때마다 한 번씩만 하면 됩니다.
3. `NxOpenFile.exe`를 실행하고 **[NX에서 파일 열기...]** 버튼을 누른 뒤 `.prt` 파일을 선택합니다.
4. 실행 중인 NX에 해당 파일이 열립니다.
   - 이미 열려 있는 파일이면 다시 열지 않고 그 파일로 화면을 전환합니다.

### NX 시작 시 자동 로드 (선택)

매번 Ctrl+U를 하기 싫다면, DLL을 NX 사용자 정의 디렉터리의 `startup` 폴더에 두세요.
NX가 시작할 때 `Startup()` 진입점이 호출되어 브리지가 자동으로 시작됩니다.
예를 들어 `UGII_USER_DIR=C:\NXCustom`이면 `C:\NXCustom\startup\NxOpenFile.NxBridge.dll`에 둡니다.

## 참고 및 문제 해결

- **라이선스와 서명**: 서명되지 않은 NX Open DLL을 Ctrl+U로 실행하려면 NX Open 저자(author) 라이선스가 필요합니다.
  라이선스가 없는 PC에 배포하려면 NX의 `SignDotNet` 도구로 DLL에 서명해야 합니다. EXE는 서명할 필요가 없습니다.
- **"NX에 연결할 수 없습니다"**: NX에서 브리지 DLL을 실행했는지 확인하세요.
  EXE와 NX가 같은 Windows 사용자로 실행 중이어야 합니다.
- **로그**: 브리지 DLL은 `%TEMP%\NxOpenFile.NxBridge.log`에 동작 기록을 남깁니다.
- **DLL 교체**: 브리지 DLL은 NX가 종료될 때까지 로드된 상태로 남습니다 (`AtTermination`).
  DLL을 새로 빌드해 교체하려면 NX를 재시작하세요.
