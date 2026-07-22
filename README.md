# Pixel Companion

[한국어](README.md) | [English](README.en.md)

Pixel Companion은 Windows와 macOS를 위한 오프라인 우선, 비방해형 도트 데스크톱 펫입니다. 생성형 AI나 외부 AI 서비스 없이 상태 머신, 조건, 확률, 쿨다운과 현지화된 대사 데이터로 자연스러운 행동을 구현합니다.

> 현재 Windows 10/11 x64 설치 파일을 우선 제공합니다. macOS 배포 패키지는 후속 단계에서 지원할 예정입니다.

## 현재 구현된 기능

- 투명하고 항상 위에 표시되는 Avalonia 캐릭터 창
- 픽셀이 흐려지지 않는 최근접 이웃 렌더링
- 실제 경과 시간 기반 이동, 대기, 드래그, 낙하와 착지
- 클릭 대사, 먹이 주기와 놀아주기 반응
- 영어·한국어 리소스와 영어/번역 키 폴백
- 데스크톱 펫과 데이터를 공유하는 별도의 고급 설정 프로그램
- 우선순위 행동 결정, 쿨다운과 최근 대사 반복 방지
- 제한된 오프라인 경과 시간이 적용되는 다마고치 상태값
- 캐릭터 팩 유효성 검사와 플랫폼 기능 실패 시 안전한 대체 동작

## Windows 설치

[GitHub Releases](https://github.com/ByteLab-1520/PixelCompanion/releases)에서 최신 `PixelCompanion-Installer.exe`를 내려받아 실행합니다.

Windows가 게시자를 확인할 수 없다는 경고를 표시할 수 있습니다. 현재 설치 파일에는 상용 코드 서명이 적용되지 않았으므로, 이 저장소에서 받은 파일인지와 Release에 첨부된 SHA-256 값을 확인해 주세요.

## 소스에서 실행

.NET 10 SDK를 설치한 다음 다음 명령을 실행합니다.

```powershell
dotnet restore PixelCompanion.slnx
dotnet build PixelCompanion.slnx -c Release
dotnet run --project src/PixelCompanion.Desktop
```

고급 설정 프로그램은 별도로 실행할 수 있습니다.

```powershell
dotnet run --project src/PixelCompanion.Config
```

외부 테스트 프레임워크에 의존하지 않는 핵심 테스트를 실행합니다.

```powershell
dotnet run --project tests/PixelCompanion.Core.Tests
```

## Windows 인스톨러 빌드

Inno Setup 6을 설치한 다음 저장소 루트에서 실행합니다.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-WindowsInstaller.ps1
```

완성된 단일 설치 파일은 `artifacts/windows/installer/`에 생성됩니다. 자세한 내용은 [Windows 패키징 안내서](packaging/windows/README.md)를 참고하세요.

## 사용자 데이터

- Windows: `%LOCALAPPDATA%\PixelCompanion`
- macOS: `~/Library/Application Support/PixelCompanion`

설계 범위와 향후 계획은 [아키텍처 문서](docs/architecture.md)와 [로드맵](docs/roadmap.md)에서 확인할 수 있습니다.

## 라이선스

소스 코드는 [MIT License](LICENSE)로 배포됩니다. 캐릭터 자산은 각 캐릭터 매니페스트에 기록된 별도 라이선스를 따르며, 기본 오리지널 캐릭터는 CC0-1.0으로 표시되어 있습니다.
