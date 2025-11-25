# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## ⚠️ Critical Rules

### Git Commit Guidelines
- **ALL commit messages MUST be written in Korean (한국어)**
- Commit message format: `<타입>: <설명>`
  - 타입 예시: 기능, 수정, 개선, 문서, 리팩토링, 테스트, 빌드
- Examples:
  - ✅ `기능: 사용자 인증 API 추가`
  - ✅ `수정: WebGL 빌드 오류 해결`
  - ❌ `feat: Add user authentication API` (English - NOT ALLOWED)

### Documentation Generation Policy
- **NEVER create or modify *.md files without explicit user permission**
- This includes but not limited to:
  - README.md, CHANGELOG.md, CONTRIBUTING.md
  - PRD.md, USER_GUIDE.md, API.md
  - Any other markdown documentation files
- **Exception**: You may update CLAUDE.md when explicitly instructed
- **Rationale**: AI-generated documentation may appear unprofessional or inappropriate for corporate repositories

### Repository Ownership
- This is a **proprietary corporate repository** (not open source)
- Do not add LICENSE files or open source licensing information
- Do not add package.json "license" fields

### File Hygiene
- **Proactively add unnecessary files to .gitignore** when discovered
- Common files to ignore:
  - Unity: `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`
  - Build artifacts: `ait-build/`, `webgl/`, `dist/`, `node_modules/`
  - Logs: `*.log`, `*.log.meta`
  - IDE: `.idea/`, `.vscode/`, `*.swp`
- When you see untracked files that should not be committed, add them to the appropriate `.gitignore` file

## Overview

This is the **Apps in Toss Unity SDK** - a Unity package that enables Unity/Tuanjie engine game projects to be converted and deployed as mini-apps on the Apps in Toss platform. The SDK provides:

- WebGL build automation with Apps in Toss platform optimization
- Unity Editor integration with a custom Build & Deploy window
- Custom WebGL templates (AITTemplate2022) with platform-specific bridge code
- Vite-based build pipeline using granite build system
- Comprehensive C# API for platform features (payments, ads, user auth, sensors, etc.)

## Project Structure

```
apps-in-toss-unity-transform-sdk/
├── Runtime/                          # Runtime SDK code (used in Unity games)
│   ├── AIT.cs                       # Main API class with all platform features
│   └── AITBase.cs                   # Base implementation
├── Editor/                           # Unity Editor scripts
│   ├── AppsInTossBuildWindow.cs     # Main build & deploy UI window
│   ├── AITConvertCore.cs            # Core build pipeline logic
│   └── AITEditorScriptObject.cs     # Configuration ScriptableObject
├── WebGLTemplates/                   # Unity WebGL templates
│   └── AITTemplate2022/              # Main template for Unity 2022.3+
│       ├── index.html               # Template HTML with placeholders
│       ├── Runtime/                 # Platform bridge JavaScript
│       │   └── appsintoss-unity-bridge.js
│       └── BuildConfig/             # Vite build configuration
│           ├── package.json         # npm dependencies
│           ├── vite.config.ts       # Vite configuration
│           └── granite.config.ts    # Granite build config (with placeholders)
├── ViteTemplate/                     # Legacy Vite template (less used)
└── sdk-runtime-generator~/           # SDK code generator (excluded from UPM package)
```

**Note**: The `sdk-runtime-generator~` directory contains internal development tools for generating SDK runtime code. The tilde (~) suffix excludes it from Unity Package Manager distribution, following Unity's package exclusion conventions.

## Build Pipeline Architecture

The SDK uses a **two-phase build system**:

### Phase 1: Unity WebGL Build
1. `AITConvertCore.Init()` configures Unity PlayerSettings:
   - Sets WebGL template to `PROJECT:AITTemplate2022`
   - Disables compression (required by Apps in Toss)
   - Optimizes memory and threading for mobile browsers
2. `BuildWebGL()` executes Unity's BuildPipeline to `webgl/` folder
3. Produces standard Unity WebGL output with custom template

### Phase 2: Granite Build (Packaging)
1. `PackageWebGLBuild()` creates `ait-build/` directory
2. **Copies BuildConfig** from `WebGLTemplates/AITTemplate2022/BuildConfig/`:
   - `package.json`, `vite.config.ts`, `tsconfig.json` (no substitution)
   - `granite.config.ts` (with placeholder substitution for app metadata)
3. **Copies WebGL build** to proper structure:
   - `index.html` → project root (with Unity placeholder substitution)
   - `Build/`, `TemplateData/`, `Runtime/` → `public/` folder
4. **Runs npm install** (only if `node_modules/` doesn't exist)
5. **Runs `npm run build`** which executes `granite build`:
   - Copies `public/` folder to `dist/` (static file serving)
   - Generates final deployable package in `ait-build/dist/`

### Placeholder Substitution

Two types of placeholders are replaced during the build:

**In index.html (Unity placeholders):**
- `%UNITY_WEB_NAME%`, `%UNITY_COMPANY_NAME%`, etc. → Unity PlayerSettings values
- `%UNITY_WEBGL_LOADER_FILENAME%`, etc. → Actual WebGL build filenames
- `%AIT_IS_PRODUCTION%` → "true" or "false" based on config

**In granite.config.ts (Apps in Toss placeholders):**
- `%AIT_APP_NAME%` → App ID from config
- `%AIT_DISPLAY_NAME%` → Display name from config
- `%AIT_PRIMARY_COLOR%` → Brand color from config
- `%AIT_ICON_URL%` → App icon URL (required field)
- `%AIT_LOCAL_PORT%` → Dev server port

## Key Commands

### Development
The SDK is a Unity package - there are no traditional build/test commands. Development happens within Unity Editor:

1. **Open in Unity**: Load the package in a Unity project via Package Manager
2. **Access Build Window**: Unity menu `Apps in Toss > Build & Deploy Window`
3. **Run Build**: Click "🚀 Build & Package" in the window

### Testing Build Pipeline
To test the build pipeline without Unity:

```bash
# Navigate to build output
cd ait-build/

# Install dependencies (first time only)
npm install

# Run dev server
npm run dev

# Build for production
npm run build

# Deploy to Apps in Toss
npm run deploy
```

### Unity Version Requirements
- **Minimum**: Unity 2021.3 LTS
- **Recommended**: Unity 2022.3 LTS or higher
- **E2E Testing**: Always use Unity 2021.3.45f2 to ensure backward compatibility
- Tuanjie Engine supported

**IMPORTANT**: When testing the SDK or running E2E tests, ALWAYS use Unity 2021.3 to verify that the minimum supported version works correctly. The SDK must support Unity 2021.3 and all higher versions.

## Important Implementation Details

### WebGL Template System
Unity WebGL templates are in `WebGLTemplates/AITTemplate2022/`. The template is automatically:
1. Copied to Unity project's `Assets/WebGLTemplates/` on first use
2. Selected in PlayerSettings as `PROJECT:AITTemplate2022`
3. Used during WebGL build to inject Apps in Toss bridge code

### Build Settings Auto-Configuration
`AITConvertCore.Init()` automatically configures optimal settings:
- **Compression**: Disabled (Apps in Toss requirement)
- **Threading**: Disabled (mobile browser compatibility)
- **Memory**: 512MB for Unity 2022.3, 1024MB for Unity 6
- **Data caching**: Disabled
- **Linker target**: Wasm

### Node.js/npm Detection
The SDK searches for npm in multiple paths:
```csharp
string[] possiblePaths = {
    "/usr/local/bin/npm",
    "/opt/homebrew/bin/npm",
    "/usr/bin/npm"
};
```
Falls back to `which npm` if not found in standard locations.

### Dev Server Implementation
- Uses `npx vite --port {localPort} --host` to serve the build
- Automatically kills existing processes on the same port using `lsof`
- Runs in background with Unity EditorWindow monitoring process state

### Configuration Storage
Settings are stored in `Assets/AppsInToss/Editor/AITConfig.asset` (ScriptableObject):
- App metadata (name, version, description, icon URL)
- Build settings (production mode, optimization flags)
- Branding (primary color, icon URL)
- Advertising IDs (optional)
- Deployment key (for `ait deploy`)

**Critical**: The icon URL (`config.iconUrl`) is mandatory for builds - the pipeline validates this and shows an error dialog if missing.

### Caching Strategy
The build pipeline optimizes subsequent builds by:
- Preserving `node_modules/` between builds (skips `npm install` if exists)
- Using local npm cache in `.npm-cache/`
- Only deleting `dist/`, `public/`, and temp files between builds

## API Structure

The `AIT` class in `Runtime/AIT.cs` provides the complete platform API surface:

### Core Features
- **Init & Auth**: `Init()`, `Login()`, `Logout()`, `CheckLoginStatus()`, `GetUserInfo()`
- **Payments**: `RequestPayment()` (Toss Pay integration)
- **Advertising**: `ShowInterstitialAd()`, `ShowRewardedAd()`
- **Storage**: `SetStorageData()`, `GetStorageData()`, `RemoveStorageData()`
- **Sharing**: `ShareText()`, `ShareLink()`, `ShareImage()`
- **UI**: `ShowToast()`, `ShowDialog()`, `Vibrate()`

### Advanced APIs
- **Audio System**: Background music, audio context management (10 methods)
- **File System**: File I/O operations (10 methods)
- **Sensors**: Accelerometer, gyroscope, compass, device motion (10 methods)
- **Performance**: Performance monitoring and metrics (5 methods)

All APIs use callback-based patterns with `Action<ResultType>` delegates.

## Common Development Workflows

### Adding a New Platform Feature
1. Add C# method to `Runtime/AIT.cs` and `Runtime/AITBase.cs`
2. Add corresponding data structures (e.g., `*Options`, `*Result` classes)
3. Update `Runtime/appsintoss-unity-bridge.js` to implement JavaScript bridge
4. Test in Unity Editor and WebGL build

### Modifying Build Configuration
1. Edit template files in `WebGLTemplates/AITTemplate2022/BuildConfig/`
2. Add new placeholders in `granite.config.ts` if needed
3. Update `AITConvertCore.CopyBuildConfigFromTemplate()` to perform substitution
4. Rebuild to test

### Updating WebGL Template
1. Modify `WebGLTemplates/AITTemplate2022/index.html`
2. Update `Runtime/appsintoss-unity-bridge.js` for bridge logic
3. The template is copied to Unity projects on first build
4. Existing projects may need manual `Assets/WebGLTemplates/` update

### Debugging Build Failures
- Check Unity Console for detailed logs (Editor Window shows abbreviated logs)
- Build artifacts are in: `webgl/` (Unity build), `ait-build/` (final package)
- Common issues:
  - Missing Node.js/npm → Install Node.js 18+
  - Missing icon URL → Set in Build & Deploy Window settings
  - npm install timeout → Check internet connection, npm registry
  - WebGL build failure → Check Unity build settings, platform support installed

## Apps in Toss Platform Integration

The SDK integrates with the Apps in Toss platform through:
1. **@apps-in-toss/web-framework** npm package (runtime framework)
2. **granite** build tool (converts Unity WebGL to mini-app format)
3. **ait deploy** CLI (uploads to platform)
4. **appsintoss-unity-bridge.js** (JavaScript bridge for Unity C# ↔ platform APIs)

The bridge enables bidirectional communication:
- Unity → Platform: `SendMessage()` calls invoke platform APIs via window object
- Platform → Unity: JavaScript calls Unity game object methods

## Version Compatibility Matrix

| Unity Version | WebGL Template | Memory | Compression |
|--------------|----------------|--------|-------------|
| 2019.4       | AITTemplate    | 256MB  | Gzip/Disabled |
| 2020.3       | AITTemplate2020| 256MB  | Gzip/Disabled |
| 2022.3 LTS   | AITTemplate2022| 512MB  | Brotli/Disabled |
| 2023.3+ (Unity 6) | AITTemplate2022 | 1024MB | Brotli/Disabled |

**Note**: Compression must be Disabled for Apps in Toss deployment (platform requirement).

## File Naming Patterns

- Editor scripts: `AIT*.cs` (e.g., `AITConvertCore.cs`, `AITEditorScriptObject.cs`)
- Runtime API: `AIT.cs`, `AITBase.cs`
- Templates: `AITTemplate{UnityVersion}/` (e.g., `AITTemplate2022`)
- Build output: `webgl/` (Unity), `ait-build/` (package), `ait-build/dist/` (deployable)
- Config files: `AITConfig.asset`, `granite.config.ts`, `vite.config.ts`

## Testing Strategy

### Overview
The SDK uses an **E2E-focused testing strategy** to maximize reliability while minimizing maintenance burden. This approach prioritizes testing user-facing functionality over implementation details.

### Test Structure

```
Tests/
├── E2E/                              # End-to-End benchmark tests
│   ├── SampleUnityProject/           # Minimal Unity project for testing
│   │   ├── Assets/Scripts/
│   │   │   ├── E2ETestRunner.cs      # Benchmark data collection
│   │   │   └── Editor/BuildScript.cs # CLI build automation
│   │   └── ait-build/dist/           # Built artifacts (gitignored)
│   ├── tests/
│   │   ├── build-and-benchmark.test.js  # Playwright E2E tests
│   │   ├── playwright.config.ts
│   │   └── benchmark-results.json    # Test results (gitignored)
│   └── README.md
└── JavaScript/                       # JavaScript bridge tests
    ├── bridge.test.js                # Vitest unit tests (22 tests)
    ├── vitest.config.ts
    └── README.md
```

### Test Coverage

#### ✅ JavaScript Bridge Tests (22 tests)
- **Purpose**: Validate Unity ↔ JavaScript communication layer
- **Framework**: Vitest + happy-dom
- **Run time**: ~2-3 seconds
- **Coverage**: Browser detection, OS detection, ReactNativeWebView, Unity messaging
- **CI/CD**: Runs automatically on every push/PR

```bash
cd Tests/JavaScript
npm test
# ✓ 22 tests passed in 3ms
```

#### ✅ E2E Benchmark Tests
- **Purpose**: Validate complete build → deploy → runtime workflow
- **Framework**: Playwright (headless Chrome with GPU acceleration)
- **Run time**: ~5-10 minutes (requires pre-built Unity project)
- **Validates**:
  - Unity WebGL build success
  - SDK packaging (ait-build/dist/)
  - Placeholder substitution (%AIT_*, %UNITY_*)
  - Build size < 100MB
  - Headless browser loading
  - Unity initialization < 120s
  - Runtime performance (FPS > 20, Memory < 1GB)

```bash
# 1. Build Unity project first (manual or CLI)
# 2. Run Playwright tests
cd Tests/E2E/tests
npm test
```

#### ❌ NOT Included (Refactoring Resistance)
To maintain refactoring resistance, the following tests are **intentionally excluded**:
- ❌ C# unit tests (Validator, History, Presets)
- ❌ C# integration tests (AITConvertCore internals)
- ❌ UI tests (BuildWindow state management)
- ❌ Mock/Fixture infrastructure

**Rationale**: These tests couple to implementation details and break during refactoring. E2E tests provide better ROI by validating user-facing behavior.

### Running Tests Locally

#### JavaScript Tests (Fast)
```bash
cd Tests/JavaScript
npm install
npm test              # Run once
npm run test:watch    # Watch mode
npm run coverage      # With coverage
```

#### E2E Tests (Slow - requires Unity build)
```bash
# Option 1: Unity Editor
# 1. Open Tests/E2E/SampleUnityProject in Unity
# 2. Create BenchmarkScene.unity with E2ETestRunner component
# 3. Apps in Toss > Build for E2E
# 4. Run tests:
cd Tests/E2E/tests
npm install
npm test

# Option 2: Unity CLI (automated but slow ~20-30 min)
/Applications/Unity/Hub/Editor/2022.3.*/Unity.app/Contents/MacOS/Unity \
  -quit -batchmode -nographics \
  -projectPath Tests/E2E/SampleUnityProject \
  -executeMethod BuildScript.BuildForE2EInternal \
  -logFile -
cd Tests/E2E/tests
npm test
```

### CI/CD Integration

**.github/workflows/tests.yml** runs three jobs:

1. **javascript-tests** (automatic)
   - Runs on every push/PR
   - ~2-3 minutes
   - 22 tests must pass

2. **e2e-validation** (automatic)
   - Validates E2E test file structure
   - Checks Playwright config
   - ~1-2 minutes

3. **unity-build-test** (manual trigger only)
   - Requires Unity license secrets
   - Full Unity build + E2E test
   - ~30-40 minutes
   - Triggered via workflow_dispatch

```yaml
# Automatic on push/PR:
- JavaScript tests
- E2E structure validation

# Manual trigger only:
- Unity WebGL build
- E2E benchmark execution
```

### Test Metrics

#### Performance Benchmarks
E2E tests measure and validate:
- **Page Load Time**: Initial HTML/CSS/JS loading
- **Unity Load Time**: WebAssembly compilation + initialization
- **FPS**: Average, min, max frame rates (GPU accelerated)
- **Memory Usage**: Heap size, WebAssembly memory
- **Build Size**: Total dist folder size

Example benchmark output:
```json
{
  "timestamp": "2024-01-15T12:34:56.789Z",
  "pageLoadTime": 1234,
  "unityLoadTime": 5678,
  "distSize": 47456789,
  "benchmarkData": {
    "avgFPS": 58.5,
    "minFPS": 42.1,
    "maxFPS": 60.0,
    "memoryUsageMB": 234.5
  },
  "gpuInfo": {
    "supported": true,
    "renderer": "ANGLE (Intel HD Graphics)",
    "isHardwareAccelerated": true
  }
}
```

### Adding New Tests

#### For JavaScript Bridge Features
1. Add test to `Tests/JavaScript/bridge.test.js`
2. Run `npm run test:watch`
3. Ensure all 22+ tests pass
4. Commit (CI will auto-verify)

#### For E2E Scenarios
1. Modify `Tests/E2E/tests/build-and-benchmark.test.js`
2. Test locally with pre-built Unity project
3. Update expected metrics if needed
4. Document in `Tests/E2E/README.md`

### Troubleshooting

#### JavaScript tests fail
```bash
cd Tests/JavaScript
rm -rf node_modules package-lock.json
npm install
npm test
```

#### E2E tests fail - Build artifacts not found
```
❌ Build artifacts not found. Please build the project first.
Expected path: Tests/E2E/SampleUnityProject/ait-build/dist
```
**Solution**: Run Unity build first (see Tests/E2E/README.md)

#### E2E tests fail - GPU not accelerated
```
🎨 GPU Info: { renderer: 'SwiftShader', isHardwareAccelerated: false }
```
**Solution**: Update Playwright config args (already configured for GPU acceleration)

### Test Philosophy

**Why E2E-focused?**
- ✅ High confidence in user-facing functionality
- ✅ Survives refactoring (tests "what", not "how")
- ✅ Catches real integration bugs
- ✅ Low maintenance burden

**Why no unit tests?**
- ❌ Brittle - break on refactoring
- ❌ Test implementation details
- ❌ High maintenance cost
- ❌ False sense of security (pass but app broken)

This strategy balances **confidence** with **maintainability**, ensuring tests remain valuable as the codebase evolves.

## Documentation Files

- `README.md` - User-facing documentation (Korean)
- `PLAN_TEST.md` - Test implementation plan
- `Tests/E2E/README.md` - E2E test guide
- `Tests/JavaScript/README.md` - JavaScript test guide

All other documentation has been removed as the project is not yet released.
