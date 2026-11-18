# VS2019 VB.NET + NX2606 파일 열고/닫기 (DLL / net48)

NX 내부 실행 DLL 방식: NX에서 `파일 -> 실행 -> NX Open`으로 실행.
`NX2206` 설치 환경에서 `Release|x64` DLL 빌드 검증 완료. `NX2606`도 API 동일.

## 프로젝트 구성

- `NX_FileOpenClose.sln` : VS2019용 솔루션 (x64)
- `NX_FileOpenClose/NX_FileOpenClose.vbproj` : `net48` / `x64` / `OutputType=Library` / `UseWindowsForms`
- `NX_FileOpenClose/NxMain.vb` : DLL 진입점 `Public Shared Sub Main()`
- `NX_FileOpenClose/Unload.vb` : `GetUnloadOption` (Immediately)
- `NX_FileOpenClose/FormMain.vb` : 열기/닫기 WinForm

핵심 API (`FormMain.vb`):

```vb
' 열기 (.prt)
Dim loadStatus As NXOpen.PartLoadStatus = Nothing
opened = theSession.Parts.OpenDisplay(filePath, loadStatus)
loadStatus.Dispose()

' 열기 (STEP 등 .prt 외)
opened = theSession.Parts.OpenBaseDisplay(filePath, loadStatus)

' 현재 Display Part 닫기
theSession.Parts.Display.Close(
    NXOpen.BasePart.CloseWholeTree.True,
    NXOpen.BasePart.CloseModified.CloseModified,
    Nothing)

' 모두 닫기
theSession.Parts.CloseAll(NXOpen.BasePart.CloseModified.CloseModified, Nothing)
```

`CloseModified` = 저장 없이 닫기. 저장 후 닫으려면 먼저 `Save` 호출.

## VS2019 설정 (NX2606)

1. 환경변수 `UGII_BASE_DIR=C:\Program Files\Siemens\NX2606`
   (`.vbproj`가 `$(UGII_BASE_DIR)\NXBIN\managed\NXOpen.dll` 등 참조)
2. `NX_FileOpenClose.sln` 열기 → 플랫폼 `x64` 선택 (AnyCPU 금지)
3. 대상 프레임워크 `net48` 확인 (이미 `net48`로 고정됨)
4. 참조 3개 로컬복사=False 확인: `NXOpen`, `NXOpen.Utilities`, `NXOpenUI`
5. `Release|x64` 빌드 → `bin\x64\Release\net48\NX_FileOpenClose.dll`

검증 명령:

```powershell
dotnet restore NX_FileOpenClose\NX_FileOpenClose.vbproj
dotnet build NX_FileOpenClose\NX_FileOpenClose.vbproj -c Release -p:Platform=x64 -p:UGII_BASE_DIR="C:\Program Files\Siemens\NX2606" --no-restore
```

## NX에서 실행

1. NX2606 실행
2. `파일 -> 실행 -> NX Open` (또는 Ctrl+U)
3. `NX_FileOpenClose.dll` 선택 → 폼 표시
4. `찾아보기...`로 `.prt` 선택 → `[파일 열기]` → `[현재 파일 닫기]` / `[모두 닫기]`
