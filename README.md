# Glassboard

Windows용 WPF 클립보드/캡처 오버레이 앱입니다.

## 기능
- 최근 복사한 텍스트/이미지를 반투명 카드로 계속 표시
- `Ctrl` + 클릭으로 항목을 선택해 다시 클립보드에 복사
- 창을 항상 위에 띄워 빠르게 참조 가능

## 버전
- `v0.1.1` 우측 고정/자동 노출/`Ctrl` 홀드 진해짐 개선 버전
- GitHub Release에 직접 실행 가능한 `Glassboard.exe`와 압축본 `Glassboard-v0.1.1-win-x64.zip`을 함께 올립니다.

## 빌드
```bash
~/.dotnet/dotnet build Glassboard.csproj -c Release
```

## 실행
Windows에서 `bin/Release/net8.0-windows/Glassboard.exe`를 실행합니다.
