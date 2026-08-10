# 수동 연동

> **참고**: Apps in Toss Unity SDK 사용을 권장합니다. 수동 연동 방식은 JS Bridge, WebGL 빌드 설정, 패키징을 모두 직접 구현해야 합니다. 특별한 이유가 없다면 [SDK를 사용한 자동 연동](GettingStarted.md)을 사용하세요.

Apps in Toss에 Unity 게임을 배포하려면 Unity 프로젝트를 WebGL로 빌드해야 합니다. 이 문서는 SDK 없이 Unity에서 WebGL 빌드를 만드는 지점까지를 설명합니다. 웹 프로젝트로 감싸 배포 가능한 패키지로 만드는 작업(Vite 구성, `.ait` 패키징)은 다루지 않습니다 — 아래 "5. 결과물 확인" 절 참고.

SDK를 사용하면 이 과정 전체(WebGL 빌드부터 `.ait` 패키징까지)가 자동화됩니다. 자동화된 과정의 내부 동작은 [빌드 파이프라인](BuildProcess.md)에 정리되어 있습니다.

## 1. WebGL 모듈 설치

Unity Hub에서 WebGL 플랫폼이 설치되어 있어야 합니다.

1. Unity Hub 실행
2. Installs 탭 선택
3. 사용 중인 Unity 버전 옆의 점 세 개(···) 클릭 → Add Modules
4. WebGL Build Support 체크 → 설치
5. 설치가 완료되면 Unity에서 File > Build Settings(Unity 6 이상은 File > Build Profiles)로 진입했을 때 플랫폼 목록에 **WebGL**이 나타납니다.

## 2. 플랫폼 전환

1. Unity 프로젝트 열기
2. File > Build Settings(Unity 6 이상은 File > Build Profiles) 이동
3. WebGL 선택 → Switch Platform 클릭

## 3. Player 설정 조정

Edit > Project Settings > Player 메뉴에서 다음 항목을 설정합니다.

- Publishing Settings
  - Compression Format: `Brotli`로 설정

> **참고**: SDK를 사용한 자동 연동에서도 Production Server, Build & Package, Publish 프로필은 압축 포맷을 자동으로 Brotli로 설정합니다. Dev Server 프로필만 빌드 속도를 위해 압축을 비활성화합니다. 프로필별 매트릭스는 [빌드 프로필](BuildProfiles.md)을 참고하세요.

## 4. 빌드하기

1. File > Build Settings(Unity 6 이상은 File > Build Profiles)로 이동
2. WebGL 선택된 상태에서 → Build 클릭
3. 출력 폴더 지정 (예: `Build/`)

## 5. 결과물 확인

빌드가 완료되면 보통 `index.html`, `Build`, `TemplateData` 폴더가 생성됩니다. Unity 프로젝트 설정이나 버전에 따라 생성되는 폴더는 조금 다를 수 있습니다.

이 폴더들을 Vite 등으로 구성한 웹 프로젝트에 포함시키면 정적 웹페이지 형태로 띄울 수 있습니다. Vite 프로젝트를 구성하는 방법과, 그 결과물을 배포 가능한 `.ait` 패키지로 만드는 방법은 이 문서의 범위를 벗어납니다.

SDK를 사용한 자동 연동에서는 이 부분을 [빌드 파이프라인](BuildProcess.md)의 Phase 2 패키징 단계가 대신합니다 — WebGL 산출물을 웹 프로젝트 구조로 재배치하고, 플레이스홀더를 치환하고, `granite build`로 `.ait` 패키지를 생성합니다.

## 관련 문서

- [시작하기](GettingStarted.md) — SDK를 사용한 자동 연동
- [빌드 파이프라인](BuildProcess.md) — SDK가 WebGL 빌드부터 패키징까지 자동화하는 방식
- [빌드 프로필](BuildProfiles.md) — 프로필별 압축 포맷 등 설정 차이
