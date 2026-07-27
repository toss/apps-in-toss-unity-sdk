# 베타 채널 (파일럿 전용)

이 문서는 **선택된 파일럿 제휴사**가 `@apps-in-toss/web-framework` 3.0.0 기반 SDK(메이저 업그레이드)를 옵트인으로 미리 테스트하는 **베타 채널** 사용법을 안내합니다.

> ⚠️ **주의 — 베타 채널은 production-ready가 아닙니다.**
> 3.0.0은 빌드 시스템이 바뀌는 메이저 변경(Vite + Rolldown)입니다. 일반 서비스 배포에는 stable(`#release/vX.Y.Z`)을 사용하세요. 베타 채널은 사전 협의된 파일럿 대상에게만 안내됩니다.

## 목차

- [stable과의 차이](#stable과의-차이)
- [옵트인 (설치)](#옵트인-설치)
- [새 베타로 업데이트](#새-베타로-업데이트)
- [stable로 복귀 (repin)](#stable로-복귀-repin)
- [알아둘 점](#알아둘-점)

---

## stable과의 차이

| 항목 | stable | 베타 채널 |
|------|--------|-----------|
| 핀 대상 | `#release/v2.6.1` (불변 태그) | `#beta` (이동 브랜치, 항상 최신 베타) |
| web-framework | 2.6.1 | 3.0.0-beta.x |
| 자동 업데이트 프롬프트 | 표시됨 | **표시 안 됨** (수동 관리) |
| GitHub Release 표시 | Latest | prerelease (`--latest=false`) |
| 권장 용도 | 서비스 배포 | 파일럿 테스트 |

베타 채널은 `beta` 브랜치 하나가 항상 최신 베타를 가리키는 **이동 ref**입니다. 자동 업데이터(`AITAutoUpdater`)는 prerelease 채널에 자동 업데이트 프롬프트를 띄우지 않으므로, 새 베타가 나오면 별도(슬랙/이슈)로 안내받고 **직접 갱신**합니다.

---

## 옵트인 (설치)

### 방법 1: Package Manager

1. Unity Editor에서 `Window` > `Package Manager` 열기
2. 왼쪽 상단 `+` 버튼 클릭
3. `Add package from git URL...` 선택
4. Git URL 입력:

```
https://github.com/toss/apps-in-toss-unity-sdk.git#beta
```

### 방법 2: manifest.json 직접 수정

프로젝트의 `Packages/manifest.json`에서 의존성을 `#beta`로 지정:

```json
{
  "dependencies": {
    "im.toss.apps-in-toss-unity-sdk": "https://github.com/toss/apps-in-toss-unity-sdk.git#beta"
  }
}
```

기존에 stable로 핀되어 있었다면 fragment만 `#release/v2.6.1` → `#beta`로 바꾸면 됩니다.

> 특정 베타 스냅샷에 고정하고 싶다면 이동 브랜치 `#beta` 대신 스냅샷 태그 `#release/v3.0.0-beta.9d42c0b`를 사용하세요. 베타 릴리즈 목록은 [GitHub Releases](https://github.com/toss/apps-in-toss-unity-sdk/releases)에서 `prerelease` 표시로 확인할 수 있습니다.

---

## 새 베타로 업데이트

베타 채널은 자동 업데이트 프롬프트가 뜨지 않습니다(아래 [알아둘 점](#알아둘-점) 참조). 새 베타 배포 안내를 받으면 **수동으로** 최신 `beta` HEAD를 다시 당겨옵니다.

UPM은 git 의존성을 `Packages/packages-lock.json`에 커밋 해시로 잠그므로, 단순히 Unity를 다시 열어도 갱신되지 않습니다. 다음 중 하나로 갱신하세요:

- **Package Manager에서 제거 후 재추가** — 패키지를 remove → 같은 `#beta` URL로 다시 add (HEAD가 재해석됨). 가장 간단합니다.
- **lock 해제** — `Packages/packages-lock.json`에서 `im.toss.apps-in-toss-unity-sdk` 항목의 `"hash"`를 지우고 저장 → Unity가 `#beta` HEAD로 재해석.

---

## stable로 복귀 (repin)

파일럿이 끝났거나 안정 버전으로 돌아가려면 fragment를 **불변 stable 태그**로 바꿉니다:

```json
"im.toss.apps-in-toss-unity-sdk": "https://github.com/toss/apps-in-toss-unity-sdk.git#release/v2.6.1"
```

stable 태그로 핀하면 자동 업데이터가 다시 정상적으로 해당 stable ref를 추적합니다. (3.0.0이 정식 stable로 graduation되면 `#release/v3.0.0` 태그로 핀하세요.)

---

## 알아둘 점

- **자동 업데이트 없음**: 자동 업데이터는 prerelease 채널(`#beta` 등)에 프롬프트를 띄우지 않습니다. `AIT` > `Check for Updates...`를 수동 실행하면 "베타 채널 — 수동 관리" 안내만 표시됩니다.
- **이동 브랜치**: `beta`는 새 베타 배포 시 force-push로 갱신됩니다. 재현 가능한 빌드가 필요하면 스냅샷 태그(`#release/v3.0.0-beta.x`)로 핀하세요.
- **Latest 아님**: 베타 GitHub Release는 항상 `prerelease`이며 Latest로 표시되지 않습니다. stable `latest` 릴리즈는 영향받지 않습니다.
- **메이저 변경**: 3.0.0은 빌드 시스템이 바뀌는 메이저 업그레이드입니다. 빌드/배포 동작이 stable과 다를 수 있으니 파일럿 중 이슈는 즉시 공유해 주세요.
