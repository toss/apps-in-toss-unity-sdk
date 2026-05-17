#!/usr/bin/env bash
# 샘플 앱 로딩 시간 벤치마크 드라이버.
#
# Phase 1: 5 unity × 2 compression = 10개 빌드 (mtime 기반 캐시)
# Phase 2: 빌드 페어당 vite preview 1회 기동 + 4 network × 4 cpu × ITER iter cell 측정
#
# 결과: Tests~/E2E/tests/benchmark-loading-results.jsonl
#
# 사용:
#   scripts/benchmark.sh                 # 전체 매트릭스 (1600 측정)
#   scripts/benchmark.sh --build-only    # Phase 1만
#   scripts/benchmark.sh --measure-only  # Phase 2만 (Phase 1 산출물 가정)
#   scripts/benchmark.sh --unity 6000.2 --compression brotli   # 한 페어만
#   scripts/benchmark.sh --iterations 1 --network wifi --cpu cpu-1x  # 빠른 smoke

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TESTS_DIR="$ROOT/Tests~/E2E/tests"
RESULTS_PATH="$TESTS_DIR/benchmark-loading-results.jsonl"

UNITY_VERSIONS=(2021.3 2022.3 6000.0 6000.2 6000.3)
COMPRESSIONS=(disabled brotli)

ITERATIONS=5
ONLY_BUILD=0
ONLY_MEASURE=0
ONE_UNITY=""
ONE_COMPRESSION=""
ONE_NETWORK=""
ONE_CPU=""
FORCE_REBUILD=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --build-only)    ONLY_BUILD=1; shift ;;
    --measure-only)  ONLY_MEASURE=1; shift ;;
    --unity)         ONE_UNITY="$2"; shift 2 ;;
    --compression)   ONE_COMPRESSION="$2"; shift 2 ;;
    --iterations)    ITERATIONS="$2"; shift 2 ;;
    --network)       ONE_NETWORK="$2"; shift 2 ;;
    --cpu)           ONE_CPU="$2"; shift 2 ;;
    --force-rebuild) FORCE_REBUILD=1; shift ;;
    --results)       RESULTS_PATH="$2"; shift 2 ;;
    -h|--help)
      grep '^#' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *)
      echo "[benchmark] unknown arg: $1" >&2
      exit 2
      ;;
  esac
done

if [[ -n "$ONE_UNITY" ]]; then
  UNITY_VERSIONS=("$ONE_UNITY")
fi
if [[ -n "$ONE_COMPRESSION" ]]; then
  COMPRESSIONS=("$ONE_COMPRESSION")
fi

log() {
  echo "[benchmark $(date +%H:%M:%S)] $*"
}

build_pair() {
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
  local compression="$2"
  local dist_pair="$ROOT/Tests~/E2E/SampleUnityProject-${unity}/ait-build/dist/web-${compression}"

  if [[ ! -d "$dist_pair" ]]; then
    log "[$unity/$compression] dist missing ($dist_pair) — skip"
    return 0
  fi

  log "[$unity/$compression] measuring (iterations=$ITERATIONS)"

  local env_args=(
    BENCHMARK_UNITY="$unity"
    BENCHMARK_COMPRESSION="$compression"
    BENCHMARK_ITERATIONS="$ITERATIONS"
    BENCHMARK_RESULTS_PATH="$RESULTS_PATH"
  )
  [[ -n "$ONE_NETWORK" ]] && env_args+=(BENCHMARK_NETWORK="$ONE_NETWORK")
  [[ -n "$ONE_CPU" ]] && env_args+=(BENCHMARK_CPU="$ONE_CPU")
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
  for unity in "${UNITY_VERSIONS[@]}"; do
    for compression in "${COMPRESSIONS[@]}"; do
      build_pair "$unity" "$compression"
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
  for compression in "${COMPRESSIONS[@]}"; do
    measure_pair "$unity" "$compression"
  done
done

log "=== Phase 2 done ==="
log "to aggregate: node $ROOT/scripts/benchmark-report.mjs"
