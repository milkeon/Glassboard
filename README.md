# Glassboard

Windows에서 동작하는 *플로팅 클립보드 보조 앱*입니다.

## 기획 의도

`Win + V`로 확인할 수 있는 클립보드는 유용하지만, 매번 열고 닫으면서 확인하는 흐름이 번거롭습니다.
또한 `Ctrl + C`를 반복해서 누르다 보면, 복사가 실제로 되었는지 붙여넣기로 다시 확인해야 하는 순간이 자주 생깁니다.
Glassboard는 이런 불편을 줄이기 위해, *옆에 떠 있는 상태로 바로 보고 바로 다시 쓸 수 있는* 클립보드를 목표로 만들었습니다.

## 최종 결과물

Glassboard는 Windows 바탕화면 한쪽에 조용히 붙어 있는 오버레이 창입니다.
- 최신 클립보드의 *텍스트 5개*와 *이미지 5개*를 각각 카드로 보여줍니다.
- 항목을 클릭해 상세를 보고, *Ctrl*을 누른 상태에서 다시 선택하면 클립보드로 다시 복사합니다.
- 창은 기본적으로 클릭을 흘려보내고, *Ctrl*을 누를 때만 상호작용이 켜집니다.
- 상단의 투명도 슬라이더로 오버레이 농도를 조절할 수 있습니다.
- 가장자리에 숨겨진 리사이즈 핸들로 크기를 조절할 수 있습니다.
- 시작 시 화면 오른쪽 상단 쪽으로 자동 도킹됩니다.

## 핵심 동작

- 새 클립보드가 들어오면 텍스트/이미지를 구분해 히스토리에 추가
- 중복된 항목은 다시 쌓지 않음
- 항목 선택 시 강조 상태로 크게 확인 가능
- `Ctrl`을 누르면 클릭 가능한 상태로 전환
- `Ctrl + Enter` 또는 `Ctrl + 클릭`으로 다시 복사
- `Ctrl`을 떼면 다시 click-through 상태로 돌아감

## 핵심 기술

### 1. WPF
- Windows 데스크톱 오버레이 UI를 만드는 핵심 프레임워크입니다.
- 반투명 창, 카드형 히스토리, 선택 강조, 크기 조절 같은 데스크톱 UI를 자연스럽게 구현합니다.

### 2. .NET 8 (`net8.0-windows`)
- 최신 .NET 기반 Windows 전용 앱입니다.
- 실행 파일 배포와 유지보수가 단순합니다.

### 3. `WinExe` + Windows 타깃
- 콘솔 창 없이 바로 실행되는 데스크톱 앱 형태입니다.
- `EnableWindowsTargeting`으로 Windows 환경 배포에 맞춰 구성합니다.

### 4. 클립보드 감시와 히스토리 관리
- 클립보드 업데이트를 감시해 최신 항목을 자동으로 갱신합니다.
- 텍스트와 이미지를 별도 목록으로 관리해, 필요한 항목을 빠르게 찾을 수 있습니다.

### 5. 인터랙션 토글 UX
- 평소에는 click-through로 방해를 줄이고, `Ctrl`을 누를 때만 편집/선택/리사이즈가 가능하게 만듭니다.
- 이 방식이 “옆에 떠 있지만 필요할 때만 만지는” Glassboard의 핵심 사용성입니다.

### 6. 릴리스/아이콘 관리
- 앱 아이콘을 포함해 실행 파일과 시각적 식별성을 맞춥니다.
- GitHub Release로 배포본을 관리합니다.

## 현재 버전

- `v0.2.44`
- 단일 exe 배포 기준입니다.

## 빌드

```bash
~/.dotnet/dotnet build Glassboard.csproj -c Release
```

## 실행

Windows에서 `bin/Release/net8.0-windows/Glassboard.exe`를 실행합니다.

## 배포 참고

- GitHub Release: https://github.com/milkeon/Glassboard/releases/tag/v0.2.43
- Asset ZIP: https://github.com/milkeon/Glassboard/releases/download/v0.2.43/Glassboard-v0.2.43-win-x64.zip
- Asset EXE: https://github.com/milkeon/Glassboard/releases/download/v0.2.43/Glassboard.exe
