# 빌드 파이프라인 아키텍처

SDK는 **2단계 빌드 시스템**을 사용합니다:

## 1단계: Unity WebGL 빌드

1. `AITConvertCore.Init()`이 Unity PlayerSettings 구성 (내부적으로 AITBuildInitializer에 위임):
   - WebGL 템플릿을 `PROJECT:AITTemplate`으로 설정
   - 압축 비활성화 (Apps in Toss 요구사항)
   - 모바일 브라우저용 메모리 및 스레딩 최적화
2. `AITConvertCore.DoExport()`가 내부적으로 Unity의 BuildPipeline을 `webgl/` 폴더로 실행
3. 커스텀 템플릿이 적용된 표준 Unity WebGL 출력 생성

## 2단계: Granite 빌드 (패키징)

1. `AITPackageBuilder.PackageWebGLBuild()` (internal)가 `ait-build/` 디렉토리 생성
2. `WebGLTemplates/AITTemplate/BuildConfig~/`에서 **BuildConfig 복사**:
   - `package.json`, `vite.config.ts`, `tsconfig.json` (치환 없음)
   - `granite.config.ts` (앱 메타데이터 플레이스홀더 치환 적용)
3. **WebGL 빌드를 적절한 구조로 복사**:
   - `index.html` → 프로젝트 루트 (Unity 플레이스홀더 치환 적용)
   - `Build/`, `TemplateData/`, `Runtime/` → `public/` 폴더
4. **npm install 실행** (`node_modules/`가 없는 경우에만)
5. **`npm run build` 실행** (`granite build` 실행):
   - `ait-build/dist/`에 최종 배포 가능 패키지 생성

## 플레이스홀더 치환

빌드 중 두 가지 유형의 플레이스홀더가 치환됩니다:

**index.html (Unity 플레이스홀더):**
- `%UNITY_WEB_NAME%`, `%UNITY_COMPANY_NAME%` 등 → Unity PlayerSettings 값
- `%UNITY_WEBGL_LOADER_FILENAME%` 등 → 실제 WebGL 빌드 파일명
- `%AIT_IS_PRODUCTION%` → 설정에 따라 "true" 또는 "false"

**granite.config.ts (Apps in Toss 플레이스홀더):**
- `%AIT_APP_NAME%` → 설정의 앱 ID
- `%AIT_DISPLAY_NAME%` → 설정의 표시 이름
- `%AIT_PRIMARY_COLOR%` → 설정의 브랜드 색상
- `%AIT_ICON_URL%` → 앱 아이콘 URL (필수 필드)
- `%AIT_LOCAL_PORT%` → 개발 서버 포트
