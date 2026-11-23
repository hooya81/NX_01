# NX Arrange Dimensions

Solid Edge의 *Arrange Dimensions*와 비슷하게, NX 도면 치수를 **기준치수에서 일정 간격으로 정렬**하는 NX Open(VB.NET) 도구입니다.

- 대상: NX 2606, VB.NET / .NET Framework 4.8 (x64)
- 지원 치수: 수평 또는 수직으로 측정하는 선형 치수
  - 방향 판정 순서: ① 치수 형식(`HorizontalDimension`/`VerticalDimension`) → ② 선형 치수 빌더의 측정 방법(`Measurement.Method` = Horizontal/Vertical) → ③ 형식 이름
  - 기준치수의 방향을 판정하지 못하면 입력 창에서 쌓는 방향(위/아래, 좌/우)을 고릅니다.
  - 정렬 대상 중 방향이 확실히 반대인 치수만 제외하고, 판정할 수 없는 치수는 기준치수 방향에 맞춰 정렬합니다.

## 사용 흐름

1. 도면 파트를 열고 실행합니다 (`File > Execute > NX Open...` → `NxArrangeDimensions.dll`).
2. **기준치수**를 선택합니다. 이 치수는 움직이지 않습니다.
3. **정렬할 치수**를 여러 개 선택하고 `OK`를 누릅니다. 기준치수와 방향이 다른 치수는 자동으로 제외됩니다.
4. 대화상자에서 **배치 형태**, **치수 간격**, **쌓는 순서**를 정합니다.
   - **직렬**: 선택한 치수를 모두 기준치수와 같은 줄에 맞춥니다. 간격과 순서는 쓰지 않습니다.
   - **병렬**: 치수마다 한 줄씩 차지하도록 기준치수에서 `간격 × 1, 2, 3 …` 위치에 쌓습니다.
   - **혼합**: 이미 같은 줄에 있는 치수(체인)는 묶어서 한 줄로 쌓습니다.
   - 쌓는 순서는 "치수값 작은 것 안쪽"(기본값)과 "현재 위치 순서 유지" 중에서 고릅니다.
5. 실행 결과는 `Undo` 한 번으로 되돌릴 수 있습니다.

## 배치 규칙

- 병렬과 혼합에서는 치수를 지금 놓인 쪽(기준치수 위/아래 또는 좌/우)에 그대로 둔 채 쌓습니다.
- 병렬에서 기준치수와 거의 같은 줄에 있던 치수는 치수가 더 많은 쪽으로 보냅니다.
- 혼합에서는 기준치수와 `간격 × 0.25` 이내로 가까운 치수를 기준치수 줄에 맞추고, 서로 `간격 × 0.25` 이내인 치수를 같은 줄로 묶습니다.
- 치수선에 수직인 방향 좌표만 바꿉니다. 수평 치수는 Y, 수직 치수는 X를 바꿉니다. 텍스트의 가로 위치는 그대로 둡니다.
- 간격은 파트 단위(mm 또는 inch)를 따릅니다. 마지막 입력값은 `%APPDATA%\NxArrangeDimensions\settings.txt`에 저장되고 다음 실행 때 기본값으로 쓰입니다.

## 빌드

Visual Studio 2019 이상(SDK 스타일 프로젝트)이 필요합니다.

```
cd src\NxArrangeDimensions
dotnet build -c Release
```

- 프로젝트 옵션은 `Option Strict On`, `Option Explicit On`, `Option Infer On`입니다.
- 네임스페이스는 프로젝트의 RootNamespace(`NxArrangeDimensions`)를 그대로 씁니다.

- NX DLL은 `%UGII_BASE_DIR%\NXBIN\managed`에서 참조합니다. 경로가 다르면 `/p:NxManagedDir=C:\...\NXBIN\managed` 옵션으로 지정하세요.
- 빌드 결과는 `bin\Release\NxArrangeDimensions.dll`에 생성됩니다.
- 서명하지 않은 DLL을 실행하려면 NX Open 개발자 라이선스(`dotnet_author`)가 필요합니다. 일반 사용자에게 배포하려면 DLL에 서명해야 합니다.

## 구조

| 파일 | 역할 |
|---|---|
| `Program.vb` | NX 진입점(`Main`, `GetUnloadOption`) |
| `ArrangeDimensionsCommand.vb` | 선택, NX 치수 읽기와 이동, Undo 처리 |
| `DimensionLayout.vb` | 배치 계산. NX와 무관하므로 단독으로 테스트할 수 있습니다 |
| `SettingsForm.vb` | 간격과 정렬 순서 입력 대화상자(WinForms) |
| `ArrangeSettings.vb` | 입력값 저장과 불러오기 |

## NX에서 꼭 확인할 점

이 코드는 NX가 없는 환경에서 작성했습니다. 배치 로직은 단위 테스트로 검증했지만, 아래 NX 동작은 실제 NX에서 확인해야 합니다.

- `Annotation.AnnotationOrigin`을 설정했을 때 치수선이 함께 이동하는지 확인합니다. 연관 원점(Associative Origin)이 걸린 치수는 움직이지 않을 수 있습니다.
- 선택 마스크 `UF_dimension_type` (subtype 0)으로 모든 치수 유형이 선택되는지 확인합니다.
- NX 2606에서 `Linear` 명령으로 만든 수평/수직 치수가 `HorizontalDimension`/`VerticalDimension` 객체로 인식되는지 확인합니다.

## 다음 단계 후보

- 평행(Parallel), 원통(Cylindrical), 각도 치수 지원
- 치수 측정 범위로 체인 치수를 판정하고, 텍스트 박스로 겹침을 검사
- 뷰나 시트 전체를 한 번에 자동 정렬
- Block UI Styler 대화상자와 리본 버튼 등록
