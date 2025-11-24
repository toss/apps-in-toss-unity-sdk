# Unity WebGL Benchmark

Unity 2022.3 기반 WebGL 자동 벤치마크 프로젝트
Headless Chrome에서 완전 자동으로 실행되며 결과를 JSON으로 출력합니다.

## 빠른 시작

```bash
./benchmark.sh
```

**실행 과정:**
1. Unity WebGL 빌드 (5-10분)
2. HTTP 서버 시작
3. Headless Chrome에서 벤치마크 실행 (70초)
4. JSON 결과를 stdout으로 출력

## 요구사항

- Unity 2022.3.62f3
- Python 3.x
- Google Chrome
- macOS (Apple Silicon 네이티브 지원)

## 벤치마크 테스트

4가지 테스트가 순차 실행됩니다:

1. **Baseline** (10초) - 빈 씬 성능
2. **Physics Stress** (15초) - 50개 물리 오브젝트
3. **Rendering** (15초) - 100개 오브젝트 (10x10 그리드)
4. **Combined** (20초) - 물리 + 렌더링

## 결과 분석

```bash
# 전체 JSON 보기
./benchmark.sh | jq .

# 특정 필드 추출
./benchmark.sh | jq '.baselineTest.avgFps'

# 모든 평균 FPS
./benchmark.sh | jq '{
  baseline: .baselineTest.avgFps,
  physics: .physicsTest.avgFps,
  rendering: .renderingTest.avgFps,
  combined: .combinedTest.avgFps
}'

# 파일로 저장
./benchmark.sh > results.json
```

## 출력 형식

**stdout (JSON):**
```json
{
  "testStartTime": "2025-11-19 05:39:13",
  "unityVersion": "2022.3.62f3",
  "platform": "WebGLPlayer",
  "baselineTest": {
    "avgFps": 26.56,
    "minFps": 1.56,
    "maxFps": 30.30,
    "memoryUsedMB": 0.16,
    "sampleCount": 83
  },
  ...
}
```

**stderr (사람이 읽기 쉬운 요약):**
```
==================================================
BENCHMARK RESULTS
==================================================

Baseline Test:
  Avg FPS: 26.56
  Min FPS: 1.56
  Max FPS: 30.30
  Memory: 0.16 MB
  Samples: 83
...
```

## 문제 해결

### NODE_OPTIONS 에러
```bash
unset NODE_OPTIONS
./benchmark.sh
```

### Chrome 없음
```bash
# Chrome 설치 확인
ls "/Applications/Google Chrome.app"
```

### 포트 8000 사용 중
```bash
pkill -f "python3.*server.py"
./benchmark.sh
```

## CI/CD 통합

```yaml
# GitHub Actions 예시
- name: Run Benchmark
  run: |
    cd apps-in-toss-unity-sdk-sample
    ./benchmark.sh > results.json

- name: Parse Results
  run: |
    BASELINE=$(cat results.json | jq '.baselineTest.avgFps')
    echo "Baseline FPS: $BASELINE"
```

## 프로젝트 구조

```
├── benchmark.sh              # 메인 실행 스크립트
├── server.py                 # HTTP 서버 (결과 수신)
├── README.md                 # 사용자 문서
├── CLAUDE.md                 # 개발자 문서
├── Assets/
│   ├── Editor/
│   │   └── BuildAndRunBenchmark.cs
│   ├── Plugins/
│   │   └── WebGLBenchmark.jslib
│   ├── Scenes/
│   │   └── BenchmarkScene.unity
│   ├── Scripts/
│   │   ├── AutoBenchmarkRunner.cs
│   │   ├── PerformanceBenchmark.cs
│   │   ├── PhysicsStressTest.cs
│   │   └── RenderingBenchmark.cs
│   └── WebGLTemplates/
│       └── BenchmarkTemplate/
│           └── index.html
├── ProjectSettings/
└── Packages/
```

## 라이선스

MIT License
