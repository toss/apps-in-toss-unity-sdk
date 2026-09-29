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

echo "=== Top 20 Processes by CPU ==="
ps aux --sort=-%cpu | head -21
echo ""

echo "=== Top 20 Processes by Memory ==="
ps aux --sort=-%mem | head -21
echo ""

echo "=== Unity-related Processes ==="
ps aux | grep -i -E "unity|Unity.Licensing|UnityHelper|UnityShaderCompiler|UnityPackageManager" | grep -v grep || echo "No Unity processes found"
echo ""

echo "=== Killing Unity-related Processes ==="
KILLED=0
for PROC_NAME in Unity Unity.Licensing.Client UnityHelper UnityShaderCompiler UnityPackageManager; do
  PIDS=$(pgrep -x "$PROC_NAME" 2>/dev/null || true)
  if [ -n "$PIDS" ]; then
    echo "Killing $PROC_NAME (PIDs: $PIDS)..."
    kill -9 $PIDS 2>/dev/null || true
    KILLED=$((KILLED + 1))
  fi
done
# 패턴으로도 추가 정리
UNITY_PIDS=$(pgrep -if "unity" 2>/dev/null || true)
if [ -n "$UNITY_PIDS" ]; then
  echo "Killing remaining Unity-related processes (PIDs: $UNITY_PIDS)..."
  kill -9 $UNITY_PIDS 2>/dev/null || true
  KILLED=$((KILLED + 1))
fi
if [ $KILLED -eq 0 ]; then
  echo "No Unity processes to kill"
fi
sleep 2
echo ""

echo "=== Post-cleanup Process Check ==="
ps aux | grep -i unity | grep -v grep || echo "No Unity processes remaining"
echo ""

echo "=== Memory After Cleanup ==="
vm_stat | head -10
