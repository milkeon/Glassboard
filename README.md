# Glassboard

Windows에서 동작하는 *플로팅 클립보드 보조 앱*입니다.

## 기획 의도

`Win + V`로 할 수 있는 기능은 편하지만, 매번 눌러서 확인하고 다시 닫는 과정이 번거롭습니다.
Glassboard는 이런 불편을 줄이기 위해, *옆에 떠 있는 상태로 바로 확인하고 바로 다시 쓸 수 있는* 클립보드를 목표로 만들었습니다.

## 핵심 기능

- 최근 복사한 텍스트와 이미지를 카드 형태로 계속 표시
- 필요한 항목을 빠르게 다시 클립보드에 복사
- 짧은 메모를 한 줄로 저장
- 항상 위에 떠 있는 플로팅 창으로 빠른 참조 제공
- Windows 작업 흐름을 방해하지 않는 보조 UI

## 핵심 기술

### 1. WPF
- Windows 데스크톱 UI를 만드는 핵심 프레임워크입니다.
- 카드형 화면, 반투명 배경, 항상 위 창 같은 보조 UI를 자연스럽게 표현하는 데 적합합니다.

### 2. .NET 8 (`net8.0-windows`)
- 최신 .NET 기반으로 빌드되는 Windows 전용 앱입니다.
- 배포와 실행이 단순하고, 성능과 유지보수성도 좋습니다.

### 3. Windows 전용 실행 파일 구성
- `WinExe` 방식으로 빌드되어 콘솔 창 없이 바로 실행됩니다.
- `EnableWindowsTargeting`을 사용해 Windows 환경에 맞는 배포 구성을 유지합니다.

### 4. 클립보드 중심 UX
- 핵심 목적이 “기록”이 아니라 “즉시 재사용”이기 때문에, 복사된 내용을 빠르게 다시 꺼내 쓰는 흐름에 초점을 둡니다.
- 사용자는 앱을 열어 확인만 하고, 필요한 항목을 다시 선택해 바로 붙여넣을 수 있습니다.

### 5. 아이콘/릴리스 자산 관리
- 앱 아이콘과 릴리스를 함께 관리해, 실행 파일과 배포본의 식별성을 맞춥니다.
- GitHub Release로 배포본을 함께 관리합니다.

## 현재 버전

- `v0.2.35`
- 새 아이콘을 적용한 Windows x64 self-contained 릴리스 기준입니다.

## 빌드

```bash
~/.dotnet/dotnet build Glassboard.csproj -c Release
```

## 실행

Windows에서 `bin/Release/net8.0-windows/Glassboard.exe`를 실행합니다.

## 배포 참고

- GitHub Release: https://github.com/milkeon/Glassboard/releases/tag/v0.2.35
- Asset ZIP: https://github.com/milkeon/Glassboard/releases/download/v0.2.35/Glassboard-v0.2.35-win-x64.zip
- Asset EXE: https://github.com/milkeon/Glassboard/releases/download/v0.2.35/Glassboard.exe
