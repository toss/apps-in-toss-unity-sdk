# Unity SDK 배포 가이드

이 문서는 생성된 C# 코드를 Unity SDK 사용자에게 제공하는 방법을 설명합니다.

## 📁 파일 배치 구조

생성된 파일들은 다음 위치에 배치됩니다:

```
apps-in-toss-unity-transform-sdk/
└── Runtime/
    ├── AIT.cs                    # 수동 작성 (기존)
    ├── AITBase.cs                # 수동 작성 (기존)
    ├── Plugins/                  # 수동 작성 jslib (기존)
    │   ├── AppsInToss-Core.jslib
    │   ├── AppsInToss-Payment.jslib
    │   └── ...
    └── Generated/                # 🆕 자동 생성 파일들
        ├── AIT.Generated.cs      # 33개 API 메서드
        ├── Types.Generated.cs    # 50개 타입 정의 (enum 3개, class 47개)
        └── Plugins/              # 20개 jslib 파일 (카테고리별)
            ├── AppsInToss-로그인.jslib
            ├── AppsInToss-토스페이.jslib
            └── ...
```

## 🔄 배포 워크플로우

### 1. 코드 생성

web-framework의 최신 버전을 기반으로 SDK를 생성합니다:

```bash
cd tools/generate-unity-sdk

# 방법 A: GitHub에서 최신 버전 가져오기 (권장)
npm run generate -- generate --tag next

# 방법 B: 로컬 web-framework 사용 (개발용)
npm run generate -- generate \
  --skip-clone \
  --source-path /path/to/web-framework
```

생성 결과: `Runtime/Generated/` 폴더에 파일 생성

### 2. Unity에서 확인

1. Unity Editor에서 프로젝트 열기
2. `Runtime/Generated/` 폴더 확인
3. Console에서 컴파일 오류 확인
4. 생성된 enum/class 사용 가능한지 테스트

### 3. Git에 커밋

생성된 파일들은 **자동 생성되지만 Git에 포함**됩니다:

```bash
git add Runtime/Generated/
git commit -m "기능: web-framework vX.X.X 기반 SDK 자동 생성"
git push
```

**중요**:
- `Generated/` 폴더는 `.gitignore`에 포함되지 **않습니다**
- 사용자가 Unity Package Manager로 설치할 때 바로 사용할 수 있도록 커밋합니다

### 4. Unity Package 배포

#### 방법 A: Git URL을 통한 배포 (권장)

사용자는 Unity Package Manager에서 다음과 같이 설치:

```json
{
  "dependencies": {
    "com.toss.apps-in-toss-sdk": "https://github.com/toss/apps-in-toss-unity-sdk.git#v1.2.3"
  }
}
```

#### 방법 B: npm/OpenUPM을 통한 배포

`package.json` 업데이트 후:

```bash
npm publish
```

## 🔧 유지보수

### web-framework 버전 업데이트 시

1. web-framework가 업데이트되면
2. 생성기를 다시 실행
3. 변경된 파일 확인 (diff)
4. 테스트 후 커밋

```bash
# 1. 최신 web-framework로 생성
npm run generate -- generate --tag next

# 2. 변경사항 확인
git diff Runtime/Generated/

# 3. 확인 후 커밋
git add Runtime/Generated/
git commit -m "기능: web-framework v2.0.0 API 업데이트"
```

### 타입 정의 추가/수정

**자동 생성되는 타입** (수동 수정 금지):
- `Types.Generated.cs` - API 파라미터/반환 타입
- `AIT.Generated.cs` - API 메서드
- `Plugins/*.jslib` - JavaScript bridge

**수동 작성 타입** (필요시 추가):
- `Runtime/AIT.cs` - 추가 유틸리티 메서드
- `Runtime/AITBase.cs` - 기본 클래스

## 📦 사용자 관점

Unity 개발자가 SDK를 사용하는 방법:

### 설치

```json
// manifest.json
{
  "dependencies": {
    "com.toss.apps-in-toss-sdk": "1.2.3"
  }
}
```

### 사용

```csharp
using AppsInToss.Generated;

public class MyGame : MonoBehaviour
{
    void Start()
    {
        // 자동 생성된 enum 사용
        AIT.GenerateHapticFeedback(new HapticFeedbackOptions
        {
            Type = HapticFeedbackType.TickWeak  // ✅ enum으로 자동 완성
        });

        // 자동 생성된 class 사용
        AIT.StartUpdateLocation(new StartUpdateLocationOptions
        {
            Accuracy = Accuracy.High,  // ✅ 자동 생성된 타입
            TimeInterval = 1000,
            DistanceInterval = 10
        });
    }
}
```

## 🎯 핵심 장점

### 개발자 경험 (DX)
- ✅ **타입 안전성**: enum/class로 타입 체크
- ✅ **자동 완성**: IDE에서 타입 자동 완성
- ✅ **컴파일 타임 검증**: 잘못된 값 사용 시 컴파일 오류

### 유지보수성
- ✅ **자동화**: web-framework 업데이트 시 자동 반영
- ✅ **일관성**: 수동 실수 방지
- ✅ **버전 관리**: web-framework 버전과 동기화

## 🚨 주의사항

### 1. Generated 파일 직접 수정 금지

```csharp
// ❌ 잘못된 예시
// Types.Generated.cs를 직접 수정
public enum HapticFeedbackType
{
    TickWeak,
    MyCustomType  // ❌ 추가하지 마세요! 재생성 시 손실됩니다
}

// ✅ 올바른 예시
// 별도 파일에 확장
namespace AppsInToss.Extensions
{
    public enum CustomHapticType
    {
        MyCustomType
    }
}
```

### 2. .meta 파일 관리

Unity는 각 파일에 `.meta` 파일을 자동 생성합니다:

```
Runtime/Generated/
├── AIT.Generated.cs
├── AIT.Generated.cs.meta       # Unity가 자동 생성
├── Types.Generated.cs
└── Types.Generated.cs.meta     # Unity가 자동 생성
```

**중요**: `.meta` 파일도 반드시 Git에 커밋해야 합니다. 그렇지 않으면:
- Unity에서 GUID 충돌 발생
- 참조가 깨질 수 있음

### 3. CI/CD 자동화

GitHub Actions로 자동 생성 워크플로우 구성 가능:

```yaml
name: Update Generated SDK

on:
  schedule:
    - cron: '0 0 * * 1'  # 매주 월요일
  workflow_dispatch:

jobs:
  update-sdk:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup Node.js
        uses: actions/setup-node@v3
        with:
          node-version: '20'

      - name: Generate SDK
        run: |
          cd tools/generate-unity-sdk
          npm install
          npm run build
          npm run generate -- generate --tag next

      - name: Create Pull Request
        uses: peter-evans/create-pull-request@v5
        with:
          commit-message: "기능: web-framework 최신 버전 반영"
          title: "SDK 자동 생성 업데이트"
          body: "web-framework의 최신 API를 반영한 SDK 자동 생성"
```

## 📚 참고 자료

- [Unity Package Manager 문서](https://docs.unity3d.com/Manual/upm-ui.html)
- [Git Dependencies](https://docs.unity3d.com/Manual/upm-git.html)
- [Unity Scripting API](https://docs.unity3d.com/ScriptReference/)

## 🔗 관련 문서

- [README.md](./README.md) - 생성기 사용법
- [../../Runtime/README.md](../../Runtime/README.md) - SDK 사용 가이드
- [../../CHANGELOG.md](../../CHANGELOG.md) - 변경 이력
