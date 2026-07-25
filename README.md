<div align="center">

```text
┌──────────────────────────────────────┐
│        PIXEL COMPANION  v0.2         │
│       작은 친구, 조용한 동행         │
└──────────────────────────────────────┘
```

**작업을 방해하지 않고, 바탕화면 한쪽에서 함께 지내는 도트 데스크톱 펫**

[![Version](https://img.shields.io/badge/version-0.2.0-6f7cff?style=flat-square)](Directory.Build.props)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/Avalonia-12.1-8B44AC?style=flat-square)](https://avaloniaui.net/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-lightgrey?style=flat-square)](.)
[![Offline](https://img.shields.io/badge/Core-Offline-00a86b?style=flat-square)](.)
[![License](https://img.shields.io/badge/License-MIT-00a86b?style=flat-square)](LICENSE)

[한국어](README.md) | [English](README.en.md)

<br>

<img src="docs/media/pixel-companion-hero.png" alt="Pixel Companion v0.2.0 대표 이미지" width="100%">

</div>

---

## ░░ Pixel Companion은 어떤 프로그램인가요?

Pixel Companion은 바탕화면 위를 천천히 돌아다니는 작은 도트 캐릭터입니다.

마우스를 빼앗거나 창을 강제로 움직이지 않습니다. 화면 한가운데를 오래 가리지도 않습니다. 사용자가 집중하고 있을 때는 조용히 머물고, 잠깐 쉬는 순간에는 가벼운 반응으로 존재감을 보여 주는 방향을 목표로 합니다.

생성형 AI나 온라인 AI 서비스는 사용하지 않습니다. 캐릭터의 행동은 상태, 조건, 확률, 쿨다운과 대사 데이터를 조합해 만듭니다. 핵심 기능은 인터넷 연결 없이 작동하며, 사용자 데이터도 컴퓨터 안에 보관합니다.

<p align="center">
  <img src="docs/media/desktop-pet-walk.gif" alt="바탕화면을 걷는 Pixel Companion" width="720">
</p>

| | |
|---|---|
| 🐾 | 걷기, 기다리기, 드래그, 낙하와 착지 |
| 🖼️ | 내 이미지로 캐릭터 모습 설정 |
| 💬 | 상황과 친밀도에 따른 짧은 대사 |
| 🍙 | 배고픔, 행복도, 피로도 같은 다마고치 상태값 |
| 🌐 | 한국어·영어 지원 |
| 🔒 | 온라인 계정과 원격 분석 없이 로컬 저장 |
| 🪟 | Windows 10·11 x64 우선 지원 |

---

## ░░ 지금 할 수 있는 것

- 투명한 캐릭터 창을 바탕화면 위에 표시합니다.
- 픽셀이 흐려지지 않도록 최근접 이웃 방식으로 이미지를 확대합니다.
- 실제 경과 시간을 기준으로 천천히 걷고, 걷는 사이에는 충분히 쉬도록 움직입니다.
- 캐릭터를 마우스로 잡아 옮기면 놓인 자리로 떨어진 뒤 다시 균형을 잡습니다.
- 클릭, 먹이 주기, 놀아주기 같은 간단한 상호작용을 지원합니다.
- 트레이 아이콘과 우클릭 메뉴에서 표시, 숨기기, 일시정지와 설정을 바꿀 수 있습니다.
- 실행 프로그램과 분리된 고급 설정 프로그램이 같은 사용자 데이터를 공유합니다.
- 설정과 다마고치 상태를 안전하게 저장하고, 손상된 파일은 백업에서 복구합니다.
- 한국어 번역이 없으면 영어를, 영어도 없으면 번역 키를 표시해 프로그램이 멈추지 않게 합니다.

아직 모든 계획이 완성된 것은 아닙니다. 타이머, 미디어 반응, 배터리 반응과 세밀한 캐릭터 제작 도구는 아래 로드맵에 따라 차례로 추가할 예정입니다.

---

## ░░ v0.2.0에서 달라지는 점

`v0.2.0`은 “내 캐릭터를 쉽게 넣는 방법”과 “안전하게 다음 버전으로 넘어가는 방법”에 초점을 맞췄습니다.

### 이미지로 캐릭터 만들기

고급 설정의 캐릭터 화면에서 이미지를 칸 위로 끌어 놓거나 직접 선택할 수 있습니다.

![다섯 개의 이미지 슬롯이 있는 Pixel Companion 캐릭터 설정 화면](docs/media/character-settings.png)

| 이미지 칸 | 쓰임새 |
|---|---|
| `기본` | 서 있거나 쉬고 있을 때 |
| `뒷모습` | 뒤를 바라보는 동작을 위한 준비 이미지 |
| `걷기 1 · 왼발` | 첫 번째 걷기 프레임 |
| `걷기 2 · 오른발` | 두 번째 걷기 프레임 |
| `걷기 3 · 중간` | 양쪽 걸음 사이를 잇는 프레임 |

- `PNG`, `JPG`, `JPEG`, `GIF`를 지원합니다.
- 확장자만 확인하지 않고 실제 이미지 형식도 함께 검사합니다.
- 가져온 이미지는 사용자 데이터 폴더로 복사되므로 원본을 옮겨도 계속 사용할 수 있습니다.
- 빠진 걷기 이미지는 기본 이미지로 자연스럽게 대신합니다.
- GIF는 현재 첫 번째 프레임을 사용합니다.

### 안전한 자동 업데이트 기반

- 하루에 한 번 GitHub Release에서 새 버전을 확인합니다.
- 고급 설정에서 자동 확인을 끌 수 있습니다.
- 코드 서명이 확인된 Release만 업데이트 버튼으로 자동 설치할 수 있습니다.
- 서명된 업데이트는 별도의 `PixelCompanion.Updater.exe`가 SHA-256과 Windows 코드 서명을 다시 확인한 뒤 설치합니다.
- 서명되지 않은 Release는 자동 설치하지 않고 공식 GitHub Release 페이지를 엽니다.
- 서명이 없거나 체크섬이 다르면 기존 설치를 건드리지 않습니다.

> 공개된 `v0.1.0`에는 업데이트 확인 기능이 들어 있지 않습니다. `v0.2.0`은 아직 코드 서명이 없어 직접 설치해야 하며, 이후에도 서명된 Release에 한해서만 자동 설치를 제공합니다.

### 배포 과정도 함께 정리했습니다

- 프로젝트와 인스톨러 버전을 `0.2.0`으로 맞췄습니다.
- Windows 인스톨러를 실제로 임시 설치하고 세 실행 파일의 버전을 확인한 뒤 제거하는 검사를 추가했습니다.
- 빌드 중간 체크섬과 실제 공개 파일의 최종 체크섬을 구분했습니다.
- Release 태그가 `main`에 포함된 커밋을 가리킬 때만 배포를 진행합니다.
- 설치·버전 확인·제거 검사를 통과한 파일만 GitHub Release에 올립니다.
- 현재는 미서명 안내문과 SHA-256을 함께 제공하고, 신뢰할 수 있는 코드 서명을 확보하면 자동 설치를 활성화합니다.

---

## ░░ 설치하기

현재 공개 버전은 [GitHub Releases](https://github.com/ByteLab-1520/PixelCompanion/releases)에서 받을 수 있습니다.

1. 최신 Release의 `PixelCompanion-Installer.exe`를 내려받습니다.
2. 설치 파일을 실행합니다.
3. 설치가 끝나면 시작 메뉴에서 Pixel Companion을 실행합니다.

현재 공개 설치 파일은 코드 서명 준비 단계에 있어 Windows가 게시자를 확인할 수 없다는 경고를 보여 줄 수 있습니다. 반드시 이 저장소의 Release에서 받은 파일인지 확인하고, 함께 제공되는 SHA-256 값과 비교해 주세요.

---

## ░░ 내 이미지로 캐릭터 바꾸기

```text
Pixel Companion 우클릭
        ↓
고급 설정 열기
        ↓
Character / 캐릭터 탭
        ↓
이미지를 원하는 칸에 드래그 앤 드롭
        ↓
저장
```

이미지는 다음 위치에 복사됩니다.

```text
%LOCALAPPDATA%\PixelCompanion\characters\UserCharacter\
```

설정 프로그램을 닫아 두어도 캐릭터는 정상적으로 움직입니다. 실행 중 이미지를 바꾸면 잠시 뒤 새 이미지를 다시 불러옵니다.

---

## ░░ 소스에서 실행하기

필요한 것:

```text
.NET SDK      10.0
Avalonia      12.1
Windows       10 / 11
```

저장소를 받은 뒤 다음 명령을 실행합니다.

```powershell
dotnet restore PixelCompanion.slnx
dotnet build PixelCompanion.slnx -c Release
dotnet run --project src/PixelCompanion.Desktop
```

고급 설정만 따로 실행하려면:

```powershell
dotnet run --project src/PixelCompanion.Config
```

핵심 검사를 실행하려면:

```powershell
dotnet run --project tests/PixelCompanion.Core.Tests
```

---

## ░░ Windows 인스톨러 만들기

Inno Setup을 준비한 뒤 저장소 루트에서 실행합니다.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-WindowsInstaller.ps1 -Version 0.2.0
```

서명 전 설치 파일은 다음 폴더에 만들어집니다.

```text
artifacts/windows/installer/
```

빌드 체크섬은 `.unsigned.sha256`으로 끝납니다. 공개 Release를 준비할 때 실제 배포할 설치 파일을 기준으로 `PixelCompanion-Installer.exe.sha256`을 다시 생성합니다.

자세한 과정은 [Windows 패키징 안내서](packaging/windows/README.md)와 [릴리스 절차](docs/releasing.md)를 참고해 주세요.

---

## ░░ 앞으로의 로드맵

기능을 한꺼번에 늘리기보다, 먼저 안정적으로 함께 지낼 수 있는 캐릭터를 만드는 데 집중합니다.

| 버전 | 목표 | 예정된 작업 |
|---|---|---|
| `v0.2.0` | 캐릭터 이미지와 업데이트 기반 | 이미지 슬롯, 걷기 프레임, Release 확인, 미서명 설치 파일의 안전한 수동 업데이트 |
| `v0.3.0` | 바탕화면 사용성 | 이동 영역 편집기, 다중 모니터·DPI 복구, 클릭 통과, 자동 시작과 트레이 메뉴 개선 |
| `v0.4.0` | 함께 돌보는 재미 | 상태값 변화, 먹이·청소·쓰다듬기·수면, 타이머와 집중 타이머 |
| `v0.5.0` | Windows 상황 인식 | 자리 비움·복귀, 전체 화면, 시스템 부하, 배터리·충전, 음악·영상 반응 |
| `v0.6.0` | 캐릭터 제작 도구 | 스프라이트 시트, 프레임 순서와 FPS, 기준점, 히트박스, 소품과 조건부 대사 |
| `v0.7.0` | macOS 시험 지원 | Apple Silicon·x64 빌드, 메뉴 막대, 서명·공증과 배포 패키지 |
| `v1.0.0` | 안정화 | 장시간 실행, 성능 기준, 설정 복구, 접근성, 보안과 제작 문서 정리 |

세부 계획은 [로드맵 문서](docs/roadmap.md)에서 계속 다듬습니다.

---

## ░░ 프로젝트 구성

```text
PixelCompanion/
├── src/
│   ├── PixelCompanion.Desktop/   ← 캐릭터 실행 프로그램
│   ├── PixelCompanion.Config/    ← 고급 설정 프로그램
│   ├── PixelCompanion.Core/      ← 상태, 저장, 행동과 업데이트 로직
│   └── PixelCompanion.Updater/   ← Windows 자동 업데이트 실행기
├── assets/
│   ├── characters/               ← 기본 캐릭터 팩
│   └── locales/                  ← 한국어·영어 리소스
├── scripts/                      ← 빌드, 설치 검사와 Release 스크립트
├── packaging/windows/            ← Inno Setup 설정
├── tests/                        ← 핵심 동작 검사
└── docs/                         ← 구조, 로드맵과 배포 문서
```

---

## ░░ 사용자 데이터와 개인정보

Windows에서는 다음 위치에 설정과 캐릭터 상태를 저장합니다.

```text
%LOCALAPPDATA%\PixelCompanion
```

Pixel Companion은 키보드 입력 내용, 비밀번호, 화면 이미지, 마이크 음성과 개인 파일 내용을 수집하지 않습니다. 현재 재생 정보나 배터리 상태 같은 기능을 추가하더라도 필요한 정보만 컴퓨터 안에서 처리하는 것을 원칙으로 합니다.

---

## ░░ 라이선스

프로그램 소스 코드는 [MIT License](LICENSE)로 배포합니다.

캐릭터 이미지는 캐릭터마다 별도의 라이선스를 가질 수 있습니다. 기본 오리지널 캐릭터는 자유롭게 재배포할 수 있도록 CC0-1.0으로 표시되어 있습니다.

---

<div align="center">

**Pixel Companion** — _화면 한쪽에서, 조용히 함께._

</div>
