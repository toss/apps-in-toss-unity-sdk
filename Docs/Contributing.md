# Contributing

이 문서는 Apps in Toss Unity SDK에 기여하기 위한 개발 환경 설정과 규칙을 안내합니다.

## 목차

- [개발 환경 설정](#개발-환경-설정)
- [Unity .meta 파일 규칙](#unity-meta-파일-규칙)
- [커밋 메시지 규칙](#커밋-메시지-규칙)
- [PR 가이드라인](#pr-가이드라인)

---

## 개발 환경 설정

### Git hooks 설정

저장소를 클론한 후, Git hooks를 활성화하세요:

```bash
./.githooks/setup.sh
```

이 스크립트는 다음 hook을 설정합니다:
- **pre-commit**: `.meta` 파일 누락 검사

### 필요 도구

| 도구 | 용도 | 필수 여부 |
|------|------|----------|
| Unity 2021.3+ | SDK 개발 및 테스트 | 필수 |
| Node.js 18+ | SDK Generator | 선택 (내장 Node.js 사용 가능) |
| pnpm | SDK Generator 패키지 관리 | 선택 (Generator 작업 시) |

### SDK Generator 개발

`sdk-runtime-generator~/` 디렉토리에서 SDK 코드를 자동 생성할 수 있습니다:

```bash
cd sdk-runtime-generator~
pnpm install
pnpm generate   # TypeScript → C# + JavaScript 브릿지 생성
pnpm format     # CSharpier 포맷팅 (dotnet 필요)
pnpm validate   # Mono mcs로 컴파일 검사
pnpm test       # 유닛 테스트 실행
```

> **중요**: `Runtime/SDK/` 디렉토리의 파일을 직접 수정하지 마세요. 모든 변경은 Generator를 통해 이루어져야 합니다.

---

## Unity .meta 파일 규칙

Unity 패키지의 `Editor/`, `Runtime/`, `WebGLTemplates/` 디렉토리 내 모든 파일은 반드시 `.meta` 파일이 함께 있어야 합니다.

### 검사 방법

1. **pre-commit hook**: 커밋 시 자동으로 `.meta` 파일 누락 검사

### .meta 파일 생성 방법

`.meta` 파일이 누락된 경우:

1. **Unity Editor 자동 생성 (권장)**
   - Unity Editor에서 프로젝트를 열면 자동으로 생성됩니다.

2. **수동 생성**
   - 기존 `.meta` 파일을 참고하여 생성
   - GUID는 반드시 고유해야 합니다.

### .meta 파일 예시

```yaml
fileFormatVersion: 2
guid: a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

---

## 커밋 메시지 규칙

모든 커밋 메시지는 **한국어**로 작성합니다.

### 형식

```
<타입>: <설명>
```

### 타입

| 타입 | 설명 |
|------|------|
| `기능` | 새로운 기능 추가 |
| `수정` | 버그 수정 |
| `개선` | 기존 기능 개선 |
| `리팩토링` | 코드 구조 개선 (기능 변경 없음) |
| `문서` | 문서 변경 |
| `테스트` | 테스트 추가/수정 |
| `빌드` | 빌드 설정 변경 |

### 예시

```
기능: 사용자 인증 API 추가
수정: WebGL 빌드 오류 해결
개선: 빌드 성능 최적화
리팩토링: API 호출 코드 정리
문서: 시작하기 가이드 추가
테스트: 유닛 테스트 케이스 추가
빌드: Unity 6 지원 추가
```

### 잘못된 예시

```
feat: Add user authentication API    ❌ 영어 사용
사용자 인증 API 추가                   ❌ 타입 누락
기능 사용자 인증 API 추가              ❌ 콜론 누락
```

---

## PR 가이드라인

### PR 작성 시 체크리스트

- [ ] 커밋 메시지가 한국어로 작성되었는가?
- [ ] `.meta` 파일이 모두 포함되었는가?
- [ ] 자동 생성 코드를 직접 수정하지 않았는가?
- [ ] 테스트가 통과하는가?

---

## 관련 문서

- [시작하기](GettingStarted.md) - SDK 설치 및 기본 설정
- [문제 해결](Troubleshooting.md) - FAQ 및 트러블슈팅
