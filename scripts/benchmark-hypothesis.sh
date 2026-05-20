#!/usr/bin/env bash
# 두 가설 검증용 벤치마크 드라이버 — 3-필러 × 네트워크 3점 × CPU 3점.
#
# scripts/benchmark.sh(3-필러 단일 환경)와 별개로, 다음 두 가설을 데이터로
# 검증하기 위해 네트워크·CPU 축을 각각 3점으로 넓힌 측정을 수행한다:
#   가설 1 (네트워크 의존) — p1→p2 단계(Brotli 압축 적용)의 효과는 네트워크가
#                            느릴수록 크다.
#   가설 2 (CPU 의존)      — p2→p3 단계(JS decompressionFallback → 브라우저
#                            네이티브 Brotli 디코딩)의 효과는 CPU가 느린
#                            기기일수록 크다.
#
# 측정 매트릭스: unity{2021.3,6000.2} × pillar{1,2,3}
#               × network{kr-lte-slow,kr-lte-fast,kr-wifi}
#               × cpu{cpu-2x,cpu-4x,cpu-6x} × iter 5 = 270 셀.
#
# Phase 1: unity × {brotli, disabled} = 빌드 (mtime 기반 캐시, benchmark.sh와 공유)
# Phase 2: unity × pillar당 정적 서버 1회 기동 + network × cpu × ITER cell 측정
#          benchmark-loading.mjs에 BENCHMARK_NETWORKS/BENCHMARK_CPUS(comma-
#          separated)를 넘겨 다축을 한 번에 순회한다.
#
# 결과: Tests~/E2E/tests/benchmark-hypothesis-results.jsonl (3-필러 결과와 분리)
#
# 사용:
#   scripts/benchmark-hypothesis.sh                  # 전체 270셀
#   scripts/benchmark-hypothesis.sh --build-only     # Phase 1만
#   scripts/benchmark-hypothesis.sh --measure-only   # Phase 2만 (빌드 산출물 가정)
#   scripts/benchmark-hypothesis.sh --unity 6000.2   # 한 Unity 버전만
#   scripts/benchmark-hypothesis.sh --pillar pillar3 # 한 필러만
#   BENCHMARK_ITERATIONS=1 scripts/benchmark-hypothesis.sh --measure-only  # 스모크

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TESTS_DIR="$ROOT/Tests~/E2E/tests"
RESULTS_PATH="$TESTS_DIR/benchmark-hypothesis-results.jsonl"

UNITY_VERSIONS=(2021.3 6000.2)
PILLARS=(pillar1 pillar2 pillar3)

# 가설 측정 축 — 네트워크 3점 / CPU 3점. benchmark-loading.mjs가 복수형
# env var를 comma-separated로 받아 한 서버 기동당 전 축을 순회한다.
HYPOTHESIS_NETWORKS="kr-lte-slow,kr-lte-fast,kr-wifi"
HYPOTHESIS_CPUS="cpu-2x,cpu-4x,cpu-6x"

# 반복 횟수: --iterations 인자가 우선, 없으면 외부 BENCHMARK_ITERATIONS,
# 그것도 없으면 5. (스모크 시 BENCHMARK_ITERATIONS=1로 조절 가능)
ITERATIONS="${BENCHMARK_ITERATIONS:-5}"
ONLY_BUILD=0
ONLY_MEASURE=0
ONE_UNITY=""
ONE_PILLAR=""
FORCE_REBUILD=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --build-only)    ONLY_BUILD=1; shift ;;
    --measure-only)  ONLY_MEASURE=1; shift ;;
    --unity)         ONE_UNITY="$2"; shift 2 ;;
    --pillar)        ONE_PILLAR="$2"; shift 2 ;;
    --iterations)    ITERATIONS="$2"; shift 2 ;;
    --force-rebuild) FORCE_REBUILD=1; shift ;;
    --results)       RESULTS_PATH="$2"; shift 2 ;;
    -h|--help)
      grep '^#' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *)
      echo "[benchmark-hypothesis] unknown arg: $1" >&2
      exit 2
      ;;
  esac
done

if [[ -n "$ONE_UNITY" ]]; then
  UNITY_VERSIONS=("$ONE_UNITY")
fi
if [[ -n "$ONE_PILLAR" ]]; then
  PILLARS=("$ONE_PILLAR")
fi

log() {
  echo "[benchmark-hypothesis $(date +%H:%M:%S)] $*"
}

# 필러 → 의존하는 빌드 압축 포맷. pillar1은 비압축(disabled), pillar2/3은 brotli.
pillar_compression() {
  case "$1" in
    pillar1) echo "disabled" ;;
    pillar2|pillar3) echo "brotli" ;;
    *) echo "[benchmark-hypothesis] unknown pillar: $1" >&2; exit 2 ;;
  esac
}

# Phase 1: 한 (unity, compression) 빌드를 생성한다.
# compression=brotli → dist/web-brotli, disabled → dist/web-disabled.
# 같은 압축 포맷을 공유하는 필러들은 빌드를 재사용한다. (benchmark.sh와 동일 산출물)
build_one() {
  local unity="$1"
  local compression="$2"
  local project_path="$ROOT/Tests~/E2E/SampleUnityProject-${unity}"
  local dist_pair="$project_path/ait-build/dist/web-${compression}"
  local dist_default="$project_path/ait-build/dist/web"
  local meta="$dist_pair/.benchmark-build-meta.json"

  if [[ ! -d "$project_path" ]]; then
    log "ERROR: missing project $project_path"
    return 1
  fi

  # 캐시 확인 - 산출물이 있고 force가 아니면 skip
  if [[ "$FORCE_REBUILD" -eq 0 && -d "$dist_pair" && -f "$meta" ]]; then
    log "[$unity/$compression] cached → skip build"
    return 0
  fi

  log "[$unity/$compression] building..."
  rm -rf "$dist_default" "$dist_pair"

  "$ROOT/run-local-tests.sh" --unity-build --unity-version "$unity" --compression "$compression"

  if [[ ! -d "$dist_default" ]]; then
    log "ERROR: build did not produce $dist_default"
    return 1
  fi

  mv "$dist_default" "$dist_pair"
  cat > "$meta" <<EOF
{
  "unity_version": "$unity",
  "compression": "$compression",
  "built_at": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
}
EOF
  log "[$unity/$compression] build done → $dist_pair"
}

measure_pair() {
  local unity="$1"
  local pillar="$2"
  local compression
  compression="$(pillar_compression "$pillar")"
  local dist_pair="$ROOT/Tests~/E2E/SampleUnityProject-${unity}/ait-build/dist/web-${compression}"

  if [[ ! -d "$dist_pair" ]]; then
    log "[$unity/$pillar] build dist missing ($dist_pair) — skip"
    return 0
  fi

  log "[$unity/$pillar] measuring (networks=$HYPOTHESIS_NETWORKS cpus=$HYPOTHESIS_CPUS iter=$ITERATIONS)"

  local env_args=(
    BENCHMARK_UNITY="$unity"
    BENCHMARK_PILLAR="$pillar"
    BENCHMARK_ITERATIONS="$ITERATIONS"
    BENCHMARK_RESULTS_PATH="$RESULTS_PATH"
    BENCHMARK_NETWORKS="$HYPOTHESIS_NETWORKS"
    BENCHMARK_CPUS="$HYPOTHESIS_CPUS"
  )
  [[ -n "${BENCHMARK_TIMEOUT_MS:-}" ]] && env_args+=(BENCHMARK_TIMEOUT_MS="$BENCHMARK_TIMEOUT_MS")
  [[ -n "${BENCHMARK_RETRY_ERRORS:-}" ]] && env_args+=(BENCHMARK_RETRY_ERRORS="$BENCHMARK_RETRY_ERRORS")

  (
    cd "$TESTS_DIR"
    env "${env_args[@]}" node benchmark-loading.mjs
  )
}

mkdir -p "$(dirname "$RESULTS_PATH")"

if [[ "$ONLY_MEASURE" -eq 0 ]]; then
  log "=== Phase 1: build matrix ==="
  # 측정 대상 필러들이 필요로 하는 압축 포맷 집합만 빌드한다.
  declare -A NEED_COMPRESSION=()
  for pillar in "${PILLARS[@]}"; do
    NEED_COMPRESSION["$(pillar_compression "$pillar")"]=1
  done
  for unity in "${UNITY_VERSIONS[@]}"; do
    for compression in "${!NEED_COMPRESSION[@]}"; do
      build_one "$unity" "$compression"
    done
  done
fi

if [[ "$ONLY_BUILD" -eq 1 ]]; then
  log "build-only mode → exit"
  exit 0
fi

log "=== Phase 2: measurement matrix ==="
log "results → $RESULTS_PATH"

for unity in "${UNITY_VERSIONS[@]}"; do
  for pillar in "${PILLARS[@]}"; do
    measure_pair "$unity" "$pillar"
  done
done

log "=== Phase 2 done ==="
log "to aggregate: node $ROOT/scripts/benchmark-report.mjs --input $RESULTS_PATH"
