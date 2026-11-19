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
| `NxOpenFile.App/MainForm.cs` | 버튼 2개(파일 열기, 컴포넌트 목록), 목록 표시 |
| `NxOpenFile.App/NxBridgeClient.cs` | `Call(명령이름, 인수)`: NX 안의 명령 함수를 호출하고 결과 문자열을 받음 |
| `NxOpenFile.NxBridge/NxCommands.cs` | **NX 작업 코드는 여기에 작성.** `[NxCommand]` 함수 (`OpenPart`, `GetComponents`) |
| `NxOpenFile.NxBridge/NxCommandAttribute.cs` | `[NxCommand("이름")]` 표시 |
| `NxOpenFile.NxBridge/BridgeEntry.cs` | 통신 처리 전용: NX 진입점, 파이프 서버, 명령 찾기, 메인 스레드 전환. 고칠 필요 없음 |
| `NxOpenFile.NxBridge/BridgeProtocol.cs` | 파이프 이름, 통신 규약, `NxCommandNames`, `ComponentRow` (EXE 프로젝트에 링크로 공유) |

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
   - "NX 브리지가 시작되었습니다" 메시지와 등록된 명령 목록이 뜨면 준비 완료입니다.
   - NX를 실행할 때마다 한 번씩만 하면 됩니다.
3. `NxOpenFile.exe`를 실행하고 **[NX에서 파일 열기...]** 버튼을 누른 뒤 `.prt` 파일을 선택합니다.
4. 실행 중인 NX에 해당 파일이 열립니다.
   - 이미 열려 있는 파일이면 다시 열지 않고 그 파일로 화면을 전환합니다.
5. **[컴포넌트 목록]** 버튼을 누르면 NX의 현재 디스플레이 파트 아래에 있는 컴포넌트가 모두 표시됩니다.
   - 레벨만큼 들여쓴 트리 형태로 보여 줍니다. 열은 컴포넌트, 레벨, 파트 파일, 상태입니다.
   - 첫 줄(굵은 글씨)은 디스플레이 파트 자신입니다.
   - 억제된 컴포넌트와 로드되지 않은 컴포넌트는 회색으로 표시됩니다.

## NX 기능 추가하기

NX 작업 코드는 `NxOpenFile.NxBridge/NxCommands.cs`에만 작성하면 됩니다.

1. `NxCommands.cs`에 아래 형식의 함수를 추가합니다.

   ```csharp
   [NxCommand("GetWorkPartName")]
   public static string GetWorkPartName(Session session, string arg)
   {
       Part work = session.Parts.Work;
       if (work == null) throw new InvalidOperationException("워크 파트가 없습니다.");
       return work.Leaf;
   }
   ```

   - `session`: 브리지가 넣어 주는 현재 NX 세션입니다.
   - `arg`: EXE가 보낸 인수입니다. 여러 줄이어도 됩니다.
   - 반환값: EXE로 돌려줄 문자열입니다. 여러 줄이어도 됩니다.
   - 실패를 알리려면 예외를 던집니다. 예외 메시지가 EXE에 그대로 전달됩니다.

2. EXE에서 호출합니다.

   ```csharp
   string name = await Task.Run(() => NxBridgeClient.Call("GetWorkPartName"));
   ```

   - NX 쪽 함수가 예외를 던지면 EXE에서는 `BridgeException`이 발생합니다.
   - NX에 연결하지 못하면 `TimeoutException`이 발생합니다.

3. 브리지 DLL을 다시 빌드하고 **NX를 재시작**한 뒤 Ctrl+U로 다시 실행합니다.
   - 등록된 명령 목록은 브리지 시작 메시지와 `%TEMP%\NxOpenFile.NxBridge.log`에서 확인할 수 있습니다.

### 알아 둘 점

- **함수는 NX 메인 스레드에서 실행됩니다.** NX Open API는 메인 스레드에서만 호출할 수 있어서, 브리지가 `Control.Invoke`로 메인 스레드에 넘겨 실행합니다.
- **NX 객체는 EXE로 넘길 수 없습니다.** `Session`, `Part`, `Component` 같은 객체는 NX 프로세스 안에서만 의미가 있습니다. 필요한 값은 문자열로 만들어 돌려주세요.
  여러 항목이면 `ComponentRow`처럼 직렬화 클래스를 `BridgeProtocol.cs`에 두면 EXE와 브리지가 함께 씁니다.
- **명령 이름 상수**: `BridgeProtocol.cs`의 `NxCommandNames`에 이름을 상수로 두면 오타를 막을 수 있습니다. 필수는 아닙니다.

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
