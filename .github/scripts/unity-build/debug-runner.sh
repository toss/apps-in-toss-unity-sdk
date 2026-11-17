#!/usr/bin/env bash
set -e

echo "=== Runner Diagnostics ==="
echo "Hostname: $(hostname)"
echo "Date: $(date)"
echo ""

echo "=== CPU Info ==="
sysctl -n machdep.cpu.brand_string
echo "CPU cores: $(sysctl -n hw.ncpu)"
echo ""

echo "=== Memory Usage ==="
vm_stat | head -10
echo "Total RAM: $(sysctl -n hw.memsize | awk '{printf "%.1f GB", $1/1073741824}')"
echo ""

echo "=== Disk Usage ==="
df -h / | tail -1
echo ""

# macOS BSD ps에는 GNU의 --sort 옵션이 없다. -r(CPU 내림차순) / -m(메모리
# 내림차순)이 BSD 등가물.
echo "=== Top 20 Processes by CPU ==="
ps aux -r | head -21
echo ""

echo "=== Top 20 Processes by Memory ==="
ps aux -m | head -21
echo ""

echo "=== Unity-related Processes ==="
ps aux | grep -i -E "unity|Unity.Licensing|UnityHelper|UnityShaderCompiler|UnityPackageManager" | grep -v grep || echo "No Unity processes found"
echo ""

# 이 머신은 self-hosted 러너 여러 개(macos-1-1..1-5 등)가 물리 머신 한 대를
# 공유한다 — 러너 라벨/작업 디렉토리(actions-runner-N)만 다를 뿐 같은 호스트다.
# 그래서 프로세스 "이름"만 보고 kill하면 같은 머신에서 동시에 도는 다른 잡의
# Unity 에디터까지 함께 죽인다. 반드시 이 잡 자신의 작업 디렉토리를 커맨드라인에
# 포함한 프로세스만 골라 죽인다.
#
# Unity.Licensing.Client는 여러 잡이 공유하는 라이선싱 IPC 채널의 유일한
# 프로세스라서 워크스페이스 매칭 여부와 무관하게 절대 죽이지 않는다 — 죽이면
# 그 채널에 붙어 있던 다른 잡의 라이선스 핸드셰이크가 깨지고, 다음 잡은 채널이
# 없어 구버전 번들 클라이언트를 새로 띄우다 라이선스 인증에 실패한다
# (fix-licensing-client.sh가 다루는 문제와 같은 뿌리).
echo "=== Killing Unity-related Processes (scoped to this job's workspace) ==="
echo "Always skipped regardless of match: Unity.Licensing.Client (shared licensing IPC client)"

WORKSPACE_PATTERN="${GITHUB_WORKSPACE:-}"
if [ -n "${RUNNER_WORKSPACE:-}" ]; then
  # RUNNER_WORKSPACE는 보통 ".../actions-runner-N/_work/<repo>/<repo>" 형태다.
  # 이 잡 고유의 러너 디렉토리(actions-runner-N)까지 패턴에 더해 매칭 범위를 넓힌다.
  RUNNER_DIR=$(printf '%s' "$RUNNER_WORKSPACE" | sed -E 's#(.*/actions-runner-[0-9]+)/.*#\1#')
  if [ -n "$RUNNER_DIR" ] && [ "$RUNNER_DIR" != "$RUNNER_WORKSPACE" ]; then
    if [ -n "$WORKSPACE_PATTERN" ]; then
      WORKSPACE_PATTERN="${WORKSPACE_PATTERN}|${RUNNER_DIR}"
    else
      WORKSPACE_PATTERN="$RUNNER_DIR"
    fi
  fi
fi

KILLED=0
if [ -z "$WORKSPACE_PATTERN" ]; then
  echo "::warning::GITHUB_WORKSPACE/RUNNER_WORKSPACE not set — refusing to kill by name to avoid hitting other concurrent jobs on this shared host"
else
  echo "Workspace match pattern: ${WORKSPACE_PATTERN}"
  for PROC_NAME in Unity UnityHelper UnityShaderCompiler UnityPackageManager; do
    PIDS=$(pgrep -x "$PROC_NAME" 2>/dev/null || true)
    for PID in $PIDS; do
      CMD=$(ps -ww -o command= -p "$PID" 2>/dev/null || true)
      [ -n "$CMD" ] || continue
      if printf '%s' "$CMD" | grep -qE "$WORKSPACE_PATTERN"; then
        echo "Killing $PROC_NAME (PID: $PID) — matches this job's workspace"
        kill -9 "$PID" 2>/dev/null || true
        KILLED=$((KILLED + 1))
      else
        echo "Skipping $PROC_NAME (PID: $PID) — does not match this job's workspace (likely another concurrent job on the shared host)"
      fi
    done
  done

  # 패턴으로도 추가 정리 (Unity Helper (Renderer) 등 이름이 조금씩 다른 하위
  # 프로세스). Unity.Licensing.Client는 위 설명대로 이름에 "unity"가 들어가도
  # 명시적으로 제외한다.
  UNITY_PIDS=$(pgrep -if "unity" 2>/dev/null || true)
  for PID in $UNITY_PIDS; do
    CMD=$(ps -ww -o command= -p "$PID" 2>/dev/null || true)
    [ -n "$CMD" ] || continue
    case "$CMD" in
      *Unity.Licensing.Client*)
        echo "Skipping PID $PID — Unity.Licensing.Client is never killed (shared licensing IPC client)"
        continue
        ;;
    esac
    if printf '%s' "$CMD" | grep -qE "$WORKSPACE_PATTERN"; then
      echo "Killing remaining Unity-related process (PID: $PID) — matches this job's workspace"
      kill -9 "$PID" 2>/dev/null || true
      KILLED=$((KILLED + 1))
    else
      echo "Skipping PID $PID — does not match this job's workspace (likely another concurrent job on the shared host)"
    fi
  done
fi

if [ "$KILLED" -eq 0 ]; then
  echo "No Unity processes in this job's workspace were killed"
fi
sleep 2
echo ""

echo "=== Post-cleanup Process Check ==="
ps aux | grep -i unity | grep -v grep || echo "No Unity processes remaining"
echo ""

echo "=== Memory After Cleanup ==="
vm_stat | head -10
