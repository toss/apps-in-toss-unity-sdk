#!/usr/bin/env node
/**
 * 로딩 성능 실측(perf) 리포트 생성 스크립트
 *
 * perf.yml의 measure-ttff 잡들이 업로드한 `perf-results-{version}.json`(스키마는
 * Tests~/E2E/tests/perf-ttff.test.js 참조)을 버전별로 읽어, TTFF·on-wire 바이트
 * (wasm/data/total) 표와 차트를 만든다. baseline(직전 main 측정값)이 있으면 Δ를
 * 인라인 계산해 단일 레버 PR의 효과를 한눈에 보여준다.
 *
 * 입력 레이아웃 (perf.yml report 잡에서 구성):
 *   artifacts/perf-results-macos-<version>/perf-results-<version>.json   ← 이번 실행(PR/branch)
 *   baseline/baseline-<version>.json                                     ← 직전 main 측정(옵션, 캐시 복원)
 *
 * 출력: perf-report.md (PR 코멘트 + job summary로 사용)
 *
 * **기록 전용**: 하드 게이트 없음. self-hosted 부하 변동 때문에 Δ는 참고용 신호다.
 */

import fs from "fs";
import path from "path";

// perf 매트릭스: 2021.3(레거시) · 6000.0(LTS) · 6000.3(최신). perf.yml matrix와 동기화.
const UNITY_VERSIONS = (process.env.PERF_UNITY_VERSIONS || "2021.3,6000.0,6000.3")
  .split(",")
  .map((s) => s.trim())
  .filter(Boolean);
const OS = process.env.PERF_OS || "macos";

const MB = 1048576;

/**
 * 이번 실행의 perf 결과 로드 (artifacts/perf-results-<os>-<version>/perf-results-<version>.json)
 */
function loadPerfResult(version) {
  const candidates = [
    path.join("artifacts", `perf-results-${OS}-${version}`, `perf-results-${version}.json`),
    // 단일 OS가 아닌 경우를 위한 폴백 (이름에 os 없이 업로드된 경우)
    path.join("artifacts", `perf-results-${version}`, `perf-results-${version}.json`),
  ];
  for (const fp of candidates) {
    if (fs.existsSync(fp)) {
      try {
        return JSON.parse(fs.readFileSync(fp, "utf8"));
      } catch (e) {
        console.error(`Failed to parse ${fp}: ${e.message}`);
      }
    }
  }
  return null;
}

/**
 * baseline(직전 main) 결과 로드 (baseline/baseline-<version>.json). 없으면 null.
 */
function loadBaseline(version) {
  const fp = path.join("baseline", `baseline-${version}.json`);
  if (fs.existsSync(fp)) {
    try {
      return JSON.parse(fs.readFileSync(fp, "utf8"));
    } catch (e) {
      console.error(`Failed to parse baseline ${fp}: ${e.message}`);
    }
  }
  return null;
}

function fmtMs(v) {
  if (v === null || v === undefined || isNaN(v)) return "-";
  return Math.round(v).toLocaleString("en-US") + " ms";
}
function fmtMB(bytes) {
  if (bytes === null || bytes === undefined || isNaN(bytes)) return "-";
  return (bytes / MB).toFixed(2) + " MB";
}

/**
 * Δ(현재 − baseline) 표기. lowerIsBetter 기준으로 개선/악화 화살표.
 * 반환: "−12.3% 🟢" 형태(절대차 + 상대%). baseline 없으면 "—".
 */
function delta(current, base, unit) {
  if (current === null || current === undefined || base === null || base === undefined || base === 0) {
    return "—";
  }
  const absDiff = current - base;
  const pct = (absDiff / base) * 100;
  const sign = absDiff > 0 ? "+" : absDiff < 0 ? "−" : "±";
  const arrow = pct <= -1 ? "🟢" : pct >= 1 ? "🔴" : "⚪";
  const absStr = unit === "ms"
    ? `${sign}${Math.abs(Math.round(absDiff)).toLocaleString("en-US")} ms`
    : `${sign}${(Math.abs(absDiff) / MB).toFixed(2)} MB`;
  return `${absStr} (${sign}${Math.abs(pct).toFixed(1)}%) ${arrow}`;
}

/**
 * QuickChart.io URL 생성 (generate-benchmark-report.js 패턴 재사용)
 */
function quickChartUrl(config) {
  const encoded = encodeURIComponent(JSON.stringify(config));
  return `https://quickchart.io/chart?c=${encoded}&w=600&h=300&bkg=white`;
}

function ttffChart(data) {
  const current = UNITY_VERSIONS.map((v) => {
    const m = data[v]?.current?.ttffMs?.median;
    return m != null ? Math.round(m) : 0;
  });
  const baseline = UNITY_VERSIONS.map((v) => {
    const m = data[v]?.baseline?.ttffMs?.median;
    return m != null ? Math.round(m) : null;
  });
  const hasBaseline = baseline.some((b) => b != null);
  const datasets = [
    {
      label: "TTFF (this PR)",
      data: current,
      backgroundColor: "rgba(59, 130, 246, 0.8)",
    },
  ];
  if (hasBaseline) {
    datasets.unshift({
      label: "TTFF (main baseline)",
      data: baseline.map((b) => (b == null ? 0 : b)),
      backgroundColor: "rgba(148, 163, 184, 0.7)",
    });
  }
  return quickChartUrl({
    type: "bar",
    data: { labels: UNITY_VERSIONS, datasets },
    options: {
      title: { display: true, text: "TTFF (Time To First Frame) by Unity Version — ms (lower=better)" },
      scales: { yAxes: [{ ticks: { beginAtZero: true } }] },
    },
  });
}

/**
 * 마크다운 리포트 생성
 */
function generateReport(data, meta) {
  let md = "";
  md += "## 🏎️ WebGL 로딩 성능(TTFF) 리포트\n\n";

  const present = UNITY_VERSIONS.filter((v) => data[v]?.current);
  if (present.length === 0) {
    md += "⚠️ perf 결과 아티팩트를 찾지 못했습니다 (`perf-results-*`). 빌드/측정 잡 로그를 확인하세요.\n";
    return md;
  }

  const anyBaseline = UNITY_VERSIONS.some((v) => data[v]?.baseline);
  // 측정 조건(throttle/buildType)은 버전 공통이라 첫 결과에서 추출.
  const sample = data[present[0]].current;
  const t = sample.throttle || {};
  md += `> **측정 조건**: ${sample.buildType || "?"} 빌드 · 압축 \`${sample.compressionFormat ?? "?"}\` · `;
  md += `CPU ${t.cpuRate ?? "?"}× · 네트워크 ${t.netDownMbps ?? "?"}/${t.netUpMbps ?? "?"} Mbps · RTT ${t.rttMs ?? "?"}ms · `;
  md += `median-of-${sample.iterations ?? "?"}\n`;
  md += `> **TTFF** = navStart → 기본 프레임버퍼로의 첫 WebGL draw. 낮을수록 빠름.\n`;
  if (meta.headRef) md += `> **브랜치**: \`${meta.headRef}\`\n`;
  if (!anyBaseline) {
    md += `>\n> ℹ️ baseline(직전 main 측정) 부재 — Δ 생략(기록 전용 1회차). main에 머지되면 다음 PR부터 Δ가 채워집니다.\n`;
  }
  md += "\n";

  // ===== TTFF 표 =====
  md += "### ⏱️ TTFF\n\n";
  md += "| Unity | TTFF (median) | Δ vs main | 유효 샘플 |\n";
  md += "|:------|--------------:|:---------:|:--------:|\n";
  for (const v of UNITY_VERSIONS) {
    const cur = data[v]?.current;
    if (!cur) {
      md += `| ${v} | ⏳ | — | — |\n`;
      continue;
    }
    const curTtff = cur.ttffMs?.median ?? null;
    const baseTtff = data[v]?.baseline?.ttffMs?.median ?? null;
    const valid = cur.ttffMs?.values?.length ?? 0;
    md += `| ${v} | ${fmtMs(curTtff)} | ${delta(curTtff, baseTtff, "ms")} | ${valid}/${cur.iterations ?? "?"} |\n`;
  }
  md += "\n";

  // ===== on-wire 바이트 표 =====
  md += "### 📦 On-wire 전송 바이트 (transferSize median)\n\n";
  md += "| Unity | wasm | data | total | Δ total vs main |\n";
  md += "|:------|-----:|-----:|------:|:---------------:|\n";
  for (const v of UNITY_VERSIONS) {
    const cur = data[v]?.current;
    if (!cur) {
      md += `| ${v} | ⏳ | ⏳ | ⏳ | — |\n`;
      continue;
    }
    const wasm = cur.onWireBytes?.wasm?.median ?? null;
    const dataB = cur.onWireBytes?.data?.median ?? null;
    const total = cur.onWireBytes?.total?.median ?? null;
    const baseTotal = data[v]?.baseline?.onWireBytes?.total?.median ?? null;
    md += `| ${v} | ${fmtMB(wasm)} | ${fmtMB(dataB)} | ${fmtMB(total)} | ${delta(total, baseTotal, "bytes")} |\n`;
  }
  md += "\n";

  // ===== 차트 =====
  md += "### 📊 차트\n\n";
  md += `![TTFF Chart](${ttffChart(data)})\n\n`;

  // ===== 원시 샘플 (접기) =====
  md += "<details>\n<summary>🔬 원시 TTFF 샘플 (반복별)</summary>\n\n";
  for (const v of UNITY_VERSIONS) {
    const cur = data[v]?.current;
    if (!cur) continue;
    const vals = (cur.ttffMs?.values || []).map((x) => Math.round(x)).join(", ");
    md += `- **${v}**: [${vals || "없음"}] ms\n`;
  }
  md += "\n</details>\n";

  return md;
}

// ===== 메인 실행 =====
console.log(`Loading perf results from artifacts/ (os=${OS}, versions=${UNITY_VERSIONS.join(",")})...`);
const data = {};
let loaded = 0;
let baselineCount = 0;
for (const v of UNITY_VERSIONS) {
  const current = loadPerfResult(v);
  const baseline = loadBaseline(v);
  data[v] = { current, baseline };
  if (current) {
    loaded++;
    const med = current.ttffMs?.median;
    console.log(`  ✓ ${v}: TTFF median = ${med != null ? Math.round(med) + "ms" : "N/A"}${baseline ? " (baseline 있음)" : ""}`);
  }
  if (baseline) baselineCount++;
}
console.log(`Loaded ${loaded}/${UNITY_VERSIONS.length} perf results, ${baselineCount} baselines`);

const meta = { headRef: process.env.PERF_HEAD_REF || "" };
const report = generateReport(data, meta);
fs.writeFileSync("perf-report.md", report);
console.log("Report generated: perf-report.md");
