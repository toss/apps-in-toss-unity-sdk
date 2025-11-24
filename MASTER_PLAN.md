# Unity SDK 완전 자동화 마스터 플랜

## 🎯 목표
`@apps-in-toss/web-framework` 모든 API를 Unity C#/jslib로 **100% 자동 생성**, 엄격한 검증으로 사용자 경험 보장

## 📋 전체 구조

```
[Enterprise Private]
apps-in-toss-unity-transform-sdk/
├── Runtime/Generated/       # ✅ 자동 생성 (공개)
│   ├── AIT.Generated.cs    # 전체 API
│   ├── Types.Generated.cs  # 타입 정의
│   └── *.jslib             # JavaScript 구현
├── tools/generate-unity-sdk/ # ❌ 생성 도구 (비공개)
│   ├── src/
│   │   ├── validators/     # 엄격한 검증 로직
│   │   └── ...
│   └── .generation-log.json # 생성 이력 추적
└── README.md                # ✅ 최소 문서 (공개)

[Public GitHub]
toss/apps-in-toss-unity-sdk/
└── (수동 배포 by maintainer)
```

## 🔧 Phase 1: 코드 자동 생성 도구 (3-4일)

### 도구 구조
```
tools/generate-unity-sdk/
├── package.json
├── src/
│   ├── index.ts           # CLI
│   ├── parser.ts          # ts-morph 파싱
│   ├── generators/
│   │   ├── csharp.ts
│   │   ├── jslib.ts
│   │   └── mapper.ts
│   ├── validators/        # 엄격한 검증
│   │   ├── completeness.ts  # 누락 API 검출
│   │   ├── types.ts         # 타입 매핑 검증
│   │   └── syntax.ts        # 생성 코드 문법 검증
│   └── templates/
```

### 엄격한 검증 체계

#### 1. API 완전성 검증
```typescript
// validators/completeness.ts
export function validateCompleteness(
  sourceAPIs: ParsedAPI[],
  generatedAPIs: GeneratedAPI[]
): ValidationResult {
  const missing = sourceAPIs.filter(
    api => !generatedAPIs.find(g => g.name === api.name)
  );

  if (missing.length > 0) {
    throw new Error(`
❌ 생성 실패: 누락된 API 발견

누락된 API (${missing.length}개):
${missing.map(api => `  - ${api.name} (${api.file})`).join('\n')}

🛠️  조치 필요:
1. tools/generate-unity-sdk/src/generators/ 업데이트
2. 복잡한 타입은 수동 템플릿 추가
3. 생성 후 다시 실행

생성 중단됨.
    `);
  }

  return { success: true, apiCount: generatedAPIs.length };
}
```

#### 2. 타입 매핑 검증
```typescript
// validators/types.ts
export function validateTypeMapping(api: ParsedAPI): void {
  for (const param of api.parameters) {
    if (!isSupported(param.type)) {
      throw new Error(`
❌ 지원되지 않는 타입: ${param.type}

API: ${api.name}
Parameter: ${param.name}
Type: ${param.type}

🛠️  조치 필요:
1. tools/generate-unity-sdk/src/generators/mapper.ts에 타입 매핑 추가
2. 또는 templates/에 수동 템플릿 작성

지원 가능한 타입:
- Primitives: string, number, boolean
- Objects: interface { ... }
- Arrays: T[]
- Promises: Promise<T>

생성 중단됨.
      `);
    }
  }
}
```

#### 3. 생성 코드 검증
```typescript
// validators/syntax.ts
export function validateGeneratedCode(
  csharpCode: string,
  jslibCode: string
): void {
  // C# 문법 검증
  if (!isCSharpValid(csharpCode)) {
    throw new Error(`
❌ 생성된 C# 코드에 문법 오류 발견

🛠️  조치 필요:
1. tools/generate-unity-sdk/src/templates/ 템플릿 수정
2. 또는 generators/csharp.ts 로직 수정

생성 중단됨.
    `);
  }

  // jslib 문법 검증
  if (!isJavaScriptValid(jslibCode)) {
    throw new Error(`
❌ 생성된 jslib 코드에 문법 오류 발견

🛠️  조치 필요:
1. tools/generate-unity-sdk/src/templates/ 템플릿 수정
2. 또는 generators/jslib.ts 로직 수정

생성 중단됨.
    `);
  }
}
```

### 실행 흐름
```bash
npm run generate

# 출력:
📊 web-framework 분석 중...
✓ 42개 API 발견

🔍 검증 중...
✓ 타입 매핑 완료
✓ API 완전성 확인

🔨 생성 중...
✓ AIT.Generated.cs (42 APIs)
✓ Types.Generated.cs (87 types)
✓ AppsInToss-*.jslib (10 files)

🧪 검증 중...
✓ C# 문법 검증
✓ jslib 문법 검증
✓ Unity 컴파일 테스트

✅ 생성 완료! (3.2s)

📋 요약:
- API: 42개 (100%)
- Types: 87개
- Files: 13개
```

### 에러 발생 시
```bash
npm run generate

❌ 생성 실패: 누락된 API 발견

누락된 API (3개):
  - startUpdateLocation (web-bridge/src/location.ts)
  - setDeviceOrientation (web-bridge/src/device.ts)
  - onVisibilityChanged (web-bridge/src/global.ts)

🛠️  조치 필요:
복잡한 이벤트 리스너 패턴이 감지되었습니다.
수동 템플릿 작성이 필요합니다:

1. tools/generate-unity-sdk/src/templates/event-listener.hbs 생성
2. 또는 generators/csharp.ts에 이벤트 패턴 추가

생성 중단됨.

# Exit code: 1 (CI/CD 빌드 실패)
```

## 📊 Phase 2: 전체 API 생성 (2-3일)

### 목표: 42개 전체 API 생성

#### 우선순위 1 (Day 1)
- appLogin, openCamera, getCurrentLocation
- fetchAlbumPhotos, checkoutPayment
- loadAppsInTossAdMob, showAppsInTossAdMob

#### 우선순위 2 (Day 2)
- 나머지 35개 API
- 복잡한 타입/패턴 수동 템플릿 작성

#### 우선순위 3 (Day 3)
- Unity 전체 빌드 테스트
- 주요 API E2E 테스트

## 🚀 Phase 3: Public 배포 (수동)

### 배포 프로세스
```bash
# 1. Private에서 생성 완료 확인
npm run generate
# ✅ 42개 API 모두 생성 확인

# 2. Git 커밋
git add Runtime/Generated
git commit -m "기능: 전체 API 자동 생성 (42개)"
git push origin dave

# 3. 태그 생성 (maintainer only)
git tag rc/v0.0.1
git push --tags

# 4. 수동 배포 준비
./tools/prepare-public-release.sh v0.0.1

# 5. Review 변경사항
git diff release/v0.0.1

# 6. Public repo에 수동 push
git remote add public git@github.com:toss/apps-in-toss-unity-sdk.git
git push public release/v0.0.1:main

# 7. GitHub Release 수동 생성
# Web UI에서 Release 작성
```

## ⏱️ 타임라인

- Day 1-2: 생성 도구 + 검증 시스템 구현
- Day 3: 우선 7개 API 생성 & 테스트
- Day 4-5: 전체 42개 API 생성 & 수동 템플릿 작성
- Day 6: Unity 전체 빌드 테스트
- Day 7: Public 수동 배포

## ✅ 완료 기준

- [ ] 42개 API 100% 자동 생성
- [ ] 엄격한 검증 시스템 완성 (누락 시 에러)
- [ ] Unity 2022.3 LTS 빌드 성공
- [ ] 주요 API E2E 테스트 통과
- [ ] v0.0.1 Public 수동 배포 완료
