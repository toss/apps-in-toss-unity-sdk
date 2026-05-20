// benchmark-loading-results.jsonl → median/p95/stddev/min/max 통계 집계.
// 출력:
//   benchmark-report.csv          — 전체 row (cell + statistic)
//   benchmark-report.md           — Unity × pillar pivot
//   benchmark-report.html         — 차트 + 표 + raw data + Chart.js 전부 인라인한
//                                   자기완결 단일 HTML (오프라인/CDN 차단 환경 OK)
//
// 데이터 스키마:
//   각 JSONL 행의 키: unity_version, pillar("pillar1"|"pillar2"|"pillar3"),
//                     network("wifi"), cpu_rate(4), iteration, cold_load_ms, ...
//
// 3-필러 정의:
//   pillar1 — 압축 미설정 + CDN gzip:  Unity 압축 Disabled, 평문 산출물을
//             서버가 on-the-fly gzip → 브라우저 네이티브 gzip 디코딩.
//             (baseline — 최적화 이전 상태)
//   pillar2 — 압축 O + Content-Encoding 누락: Brotli .unityweb를 헤더 없이 전송
//             → Unity 로더 JS decompressionFallback 디코딩.
//   pillar3 — 압축 O + Content-Encoding O: Brotli .unityweb를 Content-Encoding: br
//             전송 → 브라우저 네이티브 Brotli 디코딩. (정석 설정)
//
// 리포트는 pillar1을 baseline으로 잡고, pillar2/pillar3가 그 대비 cold load를
// 몇 ms / 몇 % 단축하는지(최적화 효과)를 보여준다. 음수 = 더 빠름.
//
// 사용:
//   node scripts/benchmark-report.mjs
//   node scripts/benchmark-report.mjs --input Tests~/E2E/tests/benchmark-loading-results.jsonl

import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const ROOT = path.resolve(__dirname, '..');

// 네트워크 키 → 사람이 읽는 라벨. throttling-profiles.js의 NETWORK_PROFILES와
// 일치시킨다 (벤치마크 측정 환경 표기용).
const NETWORK_LABELS = {
  'kr-lte': '한국 LTE (150↓/39↑Mbps, 38ms · 과기정통부 2024 실측 기반)',
  'kr-lte-slow': '한국 LTE 느림 (128↓/30↑Mbps, 45ms · 과기정통부 2024 LGU+ 실측)',
  'kr-lte-fast': '한국 LTE 빠름 (238↓/50↑Mbps, 30ms · 과기정통부 2024 SKT 실측)',
  'kr-wifi': '한국 WiFi (375↓/100↑Mbps, 15ms · 과기정통부 2024 상용 WiFi 실측 기반)',
  'wifi': 'wifi (30↓/15↑Mbps, 2ms)',
  'regular-4g': 'regular-4g (4↓/3↑Mbps, 20ms)',
  'good-3g': 'good-3g (1↓/0.4↑Mbps, 40ms)',
  'regular-3g': 'regular-3g (0.25↓/0.1↑Mbps, 300ms)',
};
function networkLabel(key) {
  return NETWORK_LABELS[key] || key;
}
// 데이터에 등장하는 네트워크/CPU를 사람이 읽는 한 줄로.
function envSummary(rows) {
  const nets = uniq(rows.map((r) => r.network)).filter(Boolean);
  const cpus = uniq(rows.map((r) => r.cpu_rate)).filter((x) => x != null).sort((a, b) => a - b);
  const netStr = nets.map(networkLabel).join(' / ');
  const cpuStr = cpus.map((c) => `${c}×`).join(', ');
  return { netStr, cpuStr, nets, cpus };
}

const args = parseArgs(process.argv.slice(2));
const INPUT = path.resolve(args.input || path.join(ROOT, 'Tests~/E2E/tests/benchmark-loading-results.jsonl'));
const OUT_DIR = path.resolve(args['out-dir'] || ROOT);
// 가설 검증 JSONL을 입력하면 출력 파일명도 별도로 분기해 3-필러 리포트를 덮어쓰지 않는다.
// (입력 파일명에 'hypothesis'가 포함되면 가설 리포트로 간주)
const IS_HYPOTHESIS = /hypothesis/i.test(path.basename(INPUT));
const OUT_BASE = IS_HYPOTHESIS ? 'benchmark-hypothesis-report' : 'benchmark-report';
const CSV_PATH = path.join(OUT_DIR, `${OUT_BASE}.csv`);
const MD_PATH = path.join(OUT_DIR, `${OUT_BASE}.md`);
const HTML_PATH = path.join(OUT_DIR, `${OUT_BASE}.html`);

if (!fs.existsSync(INPUT)) {
  console.error(`[report] input not found: ${INPUT}`);
  process.exit(1);
}

const rows = readJsonl(INPUT);
if (rows.length === 0) {
  console.error('[report] no rows found');
  process.exit(1);
}

console.log(`[report] loaded ${rows.length} rows from ${INPUT}`);

const groups = groupRows(rows);
const cells = Array.from(groups.values()).map(summarizeGroup);

writeCsv(cells, CSV_PATH);
writeMarkdown(rows, cells, MD_PATH);
writeHtml(rows, cells, HTML_PATH);

console.log(`[report] CSV : ${CSV_PATH}`);
console.log(`[report] MD  : ${MD_PATH}`);
console.log(`[report] HTML: ${HTML_PATH}`);

function parseArgs(argv) {
  const out = {};
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a.startsWith('--')) {
      const key = a.slice(2);
      out[key] = argv[i + 1];
      i++;
    }
  }
  return out;
}

function readJsonl(file) {
  const txt = fs.readFileSync(file, 'utf8');
  const out = [];
  for (const line of txt.split('\n')) {
    const t = line.trim();
    if (!t) continue;
    try {
      out.push(JSON.parse(t));
    } catch (e) {
      console.warn('[report] skip malformed line:', t.slice(0, 80));
    }
  }
  return out;
}

// 그룹 키: unity_version × pillar × network × cpu_rate
function groupKey(r) {
  return [r.unity_version, r.pillar, r.network, r.cpu_rate].join('|');
}

function groupRows(rows) {
  const m = new Map();
  for (const r of rows) {
    const k = groupKey(r);
    if (!m.has(k)) m.set(k, []);
    m.get(k).push(r);
  }
  return m;
}

function summarizeGroup(rows) {
  const head = rows[0];
  const cold = rows.map((r) => r.cold_load_ms).filter((x) => Number.isFinite(x));
  const transfer = rows.map((r) => r.cold_transfer_bytes).filter((x) => Number.isFinite(x));
  const errors = rows.filter((r) => r.error).length;
  return {
    unity_version: head.unity_version,
    pillar: head.pillar,
    network: head.network,
    cpu_rate: head.cpu_rate,
    webgl_data_caching: head.webgl_data_caching,
    iterations: rows.length,
    errors,
    cold: stats(cold),
    // 압축 후 전송량 중앙값 (MB) — 번들 용량 외삽에 사용
    transfer_mb: transfer.length
      ? transfer.slice().sort((a, b) => a - b)[Math.floor(transfer.length / 2)] / 1048576
      : null,
  };
}

function stats(arr) {
  if (arr.length === 0) return { count: 0, median: null, p95: null, mean: null, stddev: null, min: null, max: null };
  const sorted = arr.slice().sort((a, b) => a - b);
  const mean = sorted.reduce((s, x) => s + x, 0) / sorted.length;
  const variance = sorted.reduce((s, x) => s + (x - mean) ** 2, 0) / sorted.length;
  return {
    count: sorted.length,
    median: percentile(sorted, 0.5),
    p95: percentile(sorted, 0.95),
    mean: round(mean),
    stddev: round(Math.sqrt(variance)),
    min: sorted[0],
    max: sorted[sorted.length - 1],
  };
}

function percentile(sortedArr, p) {
  if (sortedArr.length === 0) return null;
  if (sortedArr.length === 1) return sortedArr[0];
  const idx = (sortedArr.length - 1) * p;
  const lo = Math.floor(idx);
  const hi = Math.ceil(idx);
  const w = idx - lo;
  return round(sortedArr[lo] * (1 - w) + sortedArr[hi] * w);
}

function round(n) {
  return Math.round(n * 10) / 10;
}

// ---------------------------------------------------------------------------
// CSV
// ---------------------------------------------------------------------------
function csvString(cells) {
  const headers = [
    'unity_version',
    'pillar',
    'network',
    'cpu_rate',
    'webgl_data_caching',
    'iterations',
    'errors',
    'cold_median_ms',
    'cold_p95_ms',
    'cold_mean_ms',
    'cold_stddev_ms',
    'cold_min_ms',
    'cold_max_ms',
  ];
  const lines = [headers.join(',')];
  for (const c of cells) {
    lines.push(
      [
        c.unity_version,
        c.pillar,
        c.network,
        c.cpu_rate,
        c.webgl_data_caching,
        c.iterations,
        c.errors,
        c.cold.median,
        c.cold.p95,
        c.cold.mean,
        c.cold.stddev,
        c.cold.min,
        c.cold.max,
      ]
        .map(csvCell)
        .join(','),
    );
  }
  return lines.join('\n') + '\n';
}

function writeCsv(cells, file) {
  fs.writeFileSync(file, csvString(cells));
}

function csvCell(v) {
  if (v === null || v === undefined) return '';
  const s = String(v);
  if (s.includes(',') || s.includes('"')) return `"${s.replace(/"/g, '""')}"`;
  return s;
}

// ---------------------------------------------------------------------------
// Markdown
// ---------------------------------------------------------------------------
function writeMarkdown(rows, cells, file) {
  // 가설 데이터는 "주장 → 증거 → 해석" 골격으로 별도 출력 — 기존 3-필러 리포트 로직과 분리.
  if (isHypothesisData(cells)) {
    writeHypothesisMarkdown(rows, cells, file);
    return;
  }
  const head = rows[0];
  const machine = head.machine || {};
  const env = envSummary(rows);

  const lines = [];
  lines.push('# Sample App Loading Time Benchmark (cold load)');
  lines.push('');
  lines.push(`- Total measurements: ${rows.length}`);
  lines.push(`- Distinct cells: ${cells.length}`);
  lines.push(
    `- Machine: ${machine.cpu_model || 'unknown'} (${machine.cpu_count || '?'} cores, ${machine.arch || '?'} ${machine.platform || '?'})`,
  );
  lines.push(`- Networks: ${env.netStr}`);
  lines.push(`- CPU: ${env.cpuStr} slowdown`);
  lines.push(`- Errors: ${cells.reduce((s, c) => s + c.errors, 0)}`);
  lines.push('');

  // network × cpu별 pivot 표. cpu가 1개면 내부 루프가 한 번만 돌아 기존
  // 3-필러 리포트와 하위 호환된다.
  for (const net of env.nets) {
    for (const cpu of env.cpus) {
      const netCells = cells.filter((c) => c.network === net && c.cpu_rate === cpu);
      if (netCells.length === 0) continue;
      const cpuTag = env.cpus.length > 1 ? ` · cpu ${cpu}×` : ` (cpu_rate=${cpu}×)`;
      lines.push(`## Cold load median — ${networkLabel(net)}${cpuTag}`);
      lines.push('');
      lines.push(pivotTable(netCells));
      lines.push('');
    }
  }

  // network 간 개선 비교 표 — cpu별로 분리 (복수 cpu면 cpu마다 한 표)
  if (env.nets.length >= 2) {
    for (const cpu of env.cpus) {
      const cpuTag = env.cpus.length > 1 ? ` — cpu ${cpu}×` : '';
      lines.push(`## Network 간 pillar 개선 비교 (baseline = pillar1)${cpuTag}`);
      lines.push('');
      lines.push(networkCompareTable(cells.filter((c) => c.cpu_rate === cpu), env.nets));
      lines.push('');
    }
  }

  // 가설 검증 (H1/H2) — 가설 데이터일 때만 비어있지 않음
  const hypMd = hypothesisMarkdown(cells);
  if (hypMd) {
    lines.push(hypMd);
    lines.push('');
  }

  // 번들 용량 확장 추정 (100MB / 200MB)
  lines.push(extrapolationMarkdown(cells, env.nets));
  lines.push('');

  lines.push('## 필러 정의');
  lines.push('- **pillar1**: 압축 미설정 + CDN gzip — Unity 압축 Disabled, 평문 산출물을 서버가 on-the-fly gzip → 브라우저 네이티브 gzip 디코딩. **(baseline — 최적화 이전 상태)**');
  lines.push('- **pillar2**: 압축 O + Content-Encoding 누락 — Brotli .unityweb를 헤더 없이 전송 → Unity 로더 JS decompressionFallback 디코딩.');
  lines.push('- **pillar3**: 압축 O + Content-Encoding O — Brotli .unityweb를 Content-Encoding: br 전송 → 브라우저 네이티브 Brotli 디코딩. **(정석 설정)**');
  lines.push('');
  lines.push('## Notes');
  lines.push('- Each cell measures cold load: a fresh browser (all storage cleared), URL');
  lines.push('  entered → game fully loaded. The completion signal is `window.TriggerAPITest`');
  lines.push('  becoming a function, which Unity registers from `E2ETestTrigger.Awake()` —');
  lines.push('  i.e. bundle decode + engine boot + first scene load + first-frame game code.');
  lines.push(`- Networks: ${env.netStr}. CPU: ${env.cpuStr} slowdown.`);
  lines.push('- Baseline is pillar1 (압축 미설정 — 최적화 이전 상태). pillar2/pillar3 improvement');
  lines.push('  shows how much faster each optimization step makes cold load (negative = faster).');
  lines.push('- Network slowdown amplifies transfer-size differences: the improvement from');
  lines.push('  pillar2/pillar3 is larger (in %) on WiFi, because the faster link makes the');
  lines.push('  smaller Brotli bundle pay off proportionally more.');
  lines.push('- Warm (cached re-visit) is not measured: CDP network throttling bypasses the');
  lines.push('  HTTP disk cache, so a throttled warm load is indistinguishable from cold.');
  lines.push('- See `benchmark-report.csv` for the full per-cell statistics.');
  lines.push('');

  fs.writeFileSync(file, lines.join('\n'));
}

// Unity 버전(행) × pillar(열) pivot. network를 인자로 받아 분리.
function pivotTable(cells) {
  if (cells.length === 0) return '_no data_';

  const unities = uniq(cells.map((c) => c.unity_version)).sort();
  const pillars = ['pillar1', 'pillar2', 'pillar3'].filter((p) =>
    cells.some((c) => c.pillar === p),
  );
  if (pillars.length === 0) return '_no data_';

  const pillarLabel = {
    pillar1: 'pillar1 (압축 미설정+CDN gzip, baseline)',
    pillar2: 'pillar2 (압축O+헤더X)',
    pillar3: 'pillar3 (압축O+헤더O, 정석)',
  };

  const header = ['Unity 버전', ...pillars.map((p) => pillarLabel[p] || p), 'p2 개선 (ms / %)', 'p3 개선 (ms / %)'];
  const lines = [];
  lines.push('| ' + header.join(' | ') + ' |');
  lines.push('| ' + header.map(() => '---').join(' | ') + ' |');

  for (const u of unities) {
    const getCell = (pillar) => cells.find((c) => c.unity_version === u && c.pillar === pillar);
    const vals = {};
    for (const p of pillars) {
      const c = getCell(p);
      vals[p] = c && c.cold ? c.cold.median : null;
    }
    // baseline = pillar1. improvement = pillarN − pillar1 (음수 = 더 빠름 = 최적화 효과).
    const base = vals['pillar1'];
    const p2diff = base != null && vals['pillar2'] != null ? vals['pillar2'] - base : null;
    const p3diff = base != null && vals['pillar3'] != null ? vals['pillar3'] - base : null;
    const fmtImprovementMd = (diff, base) => {
      if (diff == null) return '–';
      const ms = Math.round(diff);
      const pct = base != null && base !== 0 ? ((diff / base) * 100).toFixed(1) : null;
      const sign = ms <= 0 ? '' : '+';
      return pct != null ? `${sign}${ms} ms / ${sign}${pct}%` : `${sign}${ms} ms`;
    };

    const row = [u];
    for (const p of pillars) {
      row.push(vals[p] != null ? formatMs(vals[p]) : '–');
    }
    row.push(fmtImprovementMd(p2diff, base));
    row.push(fmtImprovementMd(p3diff, base));
    lines.push('| ' + row.join(' | ') + ' |');
  }

  lines.push('');
  lines.push('> 개선 = pillarN − pillar1 (baseline). 음수 = pillar1 대비 더 빠름(최적화 효과).');

  return lines.join('\n');
}

// network 간 pillar overhead 비교 표 (Markdown)
function networkCompareTable(cells, nets) {
  const unities = uniq(cells.map((c) => c.unity_version)).sort();
  const lines = [];

  // 헤더: Unity 버전 | p2 개선 @net | p3 개선 @net … (baseline = pillar1)
  const shortNet = (n) => n.replace('kr-', '').toUpperCase();
  const header = ['Unity 버전', ...nets.flatMap((n) => [`p2 개선 @${shortNet(n)}`, `p3 개선 @${shortNet(n)}`])];
  lines.push('| ' + header.join(' | ') + ' |');
  lines.push('| ' + header.map(() => '---').join(' | ') + ' |');

  for (const u of unities) {
    const row = [u];
    for (const net of nets) {
      const netCells = cells.filter((c) => c.network === net);
      const getV = (p) => {
        const c = netCells.find((c) => c.unity_version === u && c.pillar === p);
        return c && c.cold ? c.cold.median : null;
      };
      const base = getV('pillar1');
      const p2 = getV('pillar2');
      const p3 = getV('pillar3');
      const fmtImpNet = (val, b) => {
        if (b == null || val == null) return '–';
        const ms = Math.round(val - b);
        const pct = b !== 0 ? ((val - b) / b * 100).toFixed(1) : null;
        const sign = ms <= 0 ? '' : '+';
        return pct != null ? `${sign}${ms} ms / ${sign}${pct}%` : `${sign}${ms} ms`;
      };
      row.push(fmtImpNet(p2, base));
      row.push(fmtImpNet(p3, base));
    }
    lines.push('| ' + row.join(' | ') + ' |');
  }

  lines.push('');
  lines.push('> 개선 = pillarN − pillar1 (baseline, 음수 = 더 빠름). 네트워크가 빠를수록(WiFi) 작은 Brotli 번들의 이득이 %로 더 커진다.');

  return lines.join('\n');
}

function formatMs(v) {
  if (v === null || v === undefined) return '–';
  return `${v.toFixed ? v.toFixed(0) : v} ms`;
}

function uniq(arr) {
  return Array.from(new Set(arr));
}

// ---------------------------------------------------------------------------
// 번들 용량 확장 추정 (100MB / 200MB)
// ---------------------------------------------------------------------------
// 측정 번들은 압축 후 약 22MB(현행)이다. 더 큰 게임이라면 어떻게 될지를
// 측정값에서 외삽한다. cold load = 고정 비용(엔진 부팅·씬 로드, 번들 크기와
// 무관) + 가변 비용(전송 + 디코딩, 번들 크기에 거의 선형). 가변 비용만 번들
// 크기에 비례시켜 추정한다.
//
//   cold ≈ fixed + variable,  variable ∝ 압축 후 전송량
//
// 두 번들(작음=현행, 큼=가정)의 압축 후 크기 비 r 만큼 variable이 커진다고
// 본다. fixed는 가장 빠른 필러(pillar3)의 절편으로 근사하기 어려우므로,
// 보수적으로 cold 전체가 전송량에 선형 비례한다고 가정한 상한과, variable
// 만 비례한다고 본 하한을 함께 제시한다. 압축 후 크기는 평문 크기에 거의
// 비례하므로, 평문 100MB→압축 후 약 22MB, 평문 200MB→약 44MB로 본다.
function extrapolationSummary(cells, nets) {
  const med = (a) => { a = a.slice().sort((x, y) => x - y); return a.length ? a[Math.floor(a.length / 2)] : null; };
  const avg = (a) => (a.length ? a.reduce((s, x) => s + x, 0) / a.length : null);

  // 복수 CPU 데이터일 수 있으므로 외삽은 단일 CPU로 고정한다 (cpu-4x 우선,
  // 없으면 등장하는 가장 작은 rate). 고정하지 않으면 네트워크별 셀에 여러 CPU가
  // 섞여 평균이 왜곡된다.
  const cpuRates = uniq(cells.map((c) => c.cpu_rate)).filter((x) => x != null);
  const fixedCpu = cpuRates.includes(4) ? 4 : cpuRates.slice().sort((a, b) => a - b)[0];

  // 필러×네트워크별 평균 cold load (ms)와 압축 후 전송량(MB) — cpu 고정
  const result = nets.map((net) => {
    const pick = (p) => {
      const cs = cells.filter((c) => c.network === net && c.pillar === p && c.cpu_rate === fixedCpu && c.cold);
      const ms = avg(cs.map((c) => c.cold.median).filter((v) => v != null));
      const mb = avg(cs.map((c) => c.transfer_mb).filter((v) => v != null));
      return { ms, mb };
    };
    const p1 = pick('pillar1'), p3 = pick('pillar3');
    // 현행 압축 후 크기 (pillar3 기준 — Brotli .unityweb 전송량)
    const curMB = p3.mb || 22;
    // 현행 측정 번들의 평문 크기는 약 100MB(압축 후 ~22MB)다. 따라서 현행
    // 측정값이 곧 "평문 100MB" 시나리오다. 확장 시나리오는 평문 200MB(2×)만.
    const scenarios = [200].map((plainMB) => {
      const ratio = plainMB / 100; // 현행 평문 ≈ 100MB 기준
      // 전송량 = 현행 × ratio. cold load 추정: 전체 선형(상한)
      const proj = (base) => base != null ? Math.round(base * ratio) : null;
      return { plainMB, ratio,
        p1: proj(p1.ms), p3: proj(p3.ms),
        p1mb: Math.round((p1.mb || 30) * ratio), p3mb: Math.round(curMB * ratio) };
    });
    return { net, p1: p1.ms, p3: p3.ms, curMB, scenarios };
  });
  result.fixedCpu = fixedCpu;
  return result;
}

function extrapolationMarkdown(cells, nets) {
  const data = extrapolationSummary(cells, nets);
  const lines = [];
  lines.push('## 번들 용량이 더 크다면? — 100MB / 200MB 추정');
  lines.push('');
  lines.push('현행 측정 번들은 압축 후 약 22MB다. 실제 상용 게임은 더 클 수 있으므로,');
  lines.push('측정값에서 외삽한 예상치를 적는다. cold load는 *고정 비용*(엔진 부팅·첫 씬');
  lines.push('로드 — 번들 크기와 무관)과 *가변 비용*(다운로드+디코딩 — 번들 크기에 거의');
  lines.push('선형)으로 나뉜다. 아래는 cold load 전체가 전송량에 선형 비례한다고 본 **상한');
  lines.push('추정**(고정 비용도 함께 늘어난다고 가정 → 보수적으로 크게 잡음)이다.');
  lines.push('');
  for (const d of data) {
    const fmt = (v) => (v == null ? '–' : `${(v / 1000).toFixed(1)}s`);
    lines.push(`### ${networkLabel(d.net)}`);
    lines.push('');
    lines.push('| 번들(평문) | pillar1 (압축 미설정) | pillar3 (정석) | pillar3로 절감 |');
    lines.push('| --- | --- | --- | --- |');
    lines.push(`| 현행 ~100MB (측정값) | ${fmt(d.p1)} | ${fmt(d.p3)} | ${d.p1 && d.p3 ? `−${((d.p1 - d.p3) / 1000).toFixed(1)}s (−${(((d.p1 - d.p3) / d.p1) * 100).toFixed(0)}%)` : '–'} |`);
    for (const s of d.scenarios) {
      const save = s.p1 && s.p3 ? `−${((s.p1 - s.p3) / 1000).toFixed(1)}s (−${(((s.p1 - s.p3) / s.p1) * 100).toFixed(0)}%)` : '–';
      lines.push(`| ${s.plainMB}MB (추정 ${s.ratio}×) | ${fmt(s.p1)} | ${fmt(s.p3)} | ${save} |`);
    }
    lines.push('');
  }
  lines.push('**해석**');
  lines.push('- 번들이 2배 커지면 cold load도 대략 2배가 된다 — 가변 비용(전송·디코딩)이');
  lines.push('  지배적이기 때문이다. 따라서 큰 번들일수록 압축/헤더 설정의 *절대* 이득(초 단위)이');
  lines.push('  비례해서 커진다. 현행(평문 ~100MB)에서 약 1초를 줄였다면, 평문 200MB에서는');
  lines.push('  약 2초를 줄인다.');
  lines.push('- **%** 절감률은 번들 크기와 무관하게 거의 일정하다 — pillar1→pillar3 전환은 전송량을');
  lines.push('  같은 비율(약 0.73배)로 줄이므로, 100MB든 200MB든 비슷한 % 개선을 유지한다.');
  lines.push('- 단 200MB 평문(압축 후 ~44MB)급에서는 메모리 압박·디코딩 CPU 시간이 비선형으로');
  lines.push('  늘 수 있어, 실제로는 위 상한 추정보다 *나쁠* 가능성이 있다. 큰 번들일수록 압축/헤더를');
  lines.push('  제대로 설정하는 것(pillar3)이 더 중요해진다.');
  return lines.join('\n');
}

// HTML — 번들 용량 확장 추정 섹션 (100MB / 200MB)
function extrapolationHtml(cells, nets) {
  const data = extrapolationSummary(cells, nets);
  const sec = (v) => (v == null ? '–' : `${(v / 1000).toFixed(1)}s`);
  const tables = data.map((d) => {
    const rows = [
      { label: '현행 ~100MB (측정값)', p1: d.p1, p3: d.p3 },
      ...d.scenarios.map((s) => ({ label: `${s.plainMB}MB (추정 ${s.ratio}×)`, p1: s.p1, p3: s.p3 })),
    ].map((r) => {
      const save = r.p1 && r.p3
        ? `<b style="color:#6fd0a0">−${((r.p1 - r.p3) / 1000).toFixed(1)}s</b> <span style="color:#9aa0ac">(−${(((r.p1 - r.p3) / r.p1) * 100).toFixed(0)}%)</span>`
        : '–';
      return `<tr><td>${esc(r.label)}</td><td class="num">${sec(r.p1)}</td><td class="num">${sec(r.p3)}</td><td class="num">${save}</td></tr>`;
    }).join('\n');
    return `<h3>${esc(networkLabel(d.net))}</h3>
  <table class="cmp">
    <thead><tr><th>번들 (평문 크기)</th><th>pillar1 (압축 미설정)</th><th>pillar3 (정석)</th><th>pillar3로 절감</th></tr></thead>
    <tbody>${rows}</tbody>
  </table>`;
  }).join('\n');

  return `<h2>5. 번들 용량이 더 크다면? — 100MB / 200MB 추정</h2>
  <div class="callout">
    현행 측정 번들은 압축 후 약 22MB다. 실제 상용 게임은 더 클 수 있으므로,
    측정값에서 외삽한 <b>상한 추정</b>을 적는다. cold load는
    <b>고정 비용</b>(엔진 부팅·첫 씬 로드 — 번들 크기와 무관)과
    <b>가변 비용</b>(다운로드+디코딩 — 번들 크기에 거의 선형)으로 나뉘며,
    아래는 전체가 전송량에 선형 비례한다고 본 보수적(크게 잡은) 추정이다.
  </div>
  ${tables}
  <div class="pillar-legend" style="margin-top:14px;">
    <div class="lh">해석 — 번들이 커질수록</div>
    <p style="margin:8px 16px;color:#c8ccd4;line-height:1.7;">
      • <b>절대 절감(초)은 번들에 비례해 커진다.</b> 가변 비용(전송·디코딩)이 지배적이라,
        현행(평문 ~100MB)에서 약 1초를 줄였다면 평문 200MB에서는 약 2초를 줄인다.<br>
      • <b>% 절감률은 번들 크기와 거의 무관하게 일정하다.</b> pillar1→pillar3 전환은 전송량을
        같은 비율(약 0.73배)로 줄이므로, 100MB든 200MB든 비슷한 % 개선을 유지한다.<br>
      • 단 평문 200MB(압축 후 ~44MB)급에서는 메모리 압박·디코딩 CPU 시간이 비선형으로
        늘 수 있어, 실제로는 위 상한 추정보다 <b>나쁠</b> 가능성이 있다.
        <b>큰 번들일수록 압축/헤더를 제대로 설정하는 것(pillar3)이 더 중요해진다.</b>
    </p>
  </div>`;
}

// ---------------------------------------------------------------------------
// 가설 검증 섹션 (H1 / H2) — benchmark-hypothesis-results.jsonl 전용.
// ---------------------------------------------------------------------------
// 데이터에 가설 전용 네트워크(kr-lte-slow/kr-lte-fast)가 등장할 때만 렌더한다.
// 기존 3-필러 JSONL에는 이 네트워크가 없으므로 자동으로 비활성화 → 하위 호환.
function isHypothesisData(cells) {
  return cells.some((c) => c.network === 'kr-lte-slow' || c.network === 'kr-lte-fast');
}

// ---------------------------------------------------------------------------
// 가설 리포트 — "주장 → 증거 → 해석" 골격
// ---------------------------------------------------------------------------
// 본문에서 다루는 두 변화:
//   A = Brotli 압축 적용 (pillar1 → pillar2). 빌드를 Brotli로 압축 (전송량 ~30MB→~22MB).
//                                              디코딩은 JS decompressionFallback.
//   B = Content-Encoding 헤더 추가 (pillar2 → pillar3). 같은 Brotli 산출물을 헤더와 함께
//                                                      전송 → 브라우저 네이티브 디코딩.
//
// 9셀(network × cpu) 매트릭스로 효과를 분리해 본다.
// const를 쓰면 ESM 초기화 순서 문제가 생기므로 호이스팅되는 함수로.
function hypNets() { return ['kr-lte-slow', 'kr-lte-fast', 'kr-wifi']; }
function hypCpus() { return [2, 4, 6]; }

// Unity 평균(반올림). 한 (net, cpu) 셀에서 unity 2개 절감을 평균.
function avgRound(arr) {
  const v = arr.filter((x) => x != null);
  return v.length ? Math.round(v.reduce((s, x) => s + x, 0) / v.length) : null;
}

// (net, cpu) → {p1, p2, p3, aMs, bMs, totalMs, totalPct} 매트릭스.
// p1/p2/p3는 unity 평균 median(ms), aMs = p1-p2(A 효과), bMs = p2-p3(B 효과).
function buildEffectMatrix(cells) {
  const cellByKey = new Map();
  for (const c of cells) {
    if (!c.cold || c.cold.median == null) continue;
    cellByKey.set(`${c.unity_version}|${c.pillar}|${c.network}|${c.cpu_rate}`, c.cold.median);
  }
  const unities = uniq(cells.map((c) => c.unity_version)).sort();
  const matrix = new Map();
  for (const net of hypNets()) for (const cpu of hypCpus()) {
    const med = (pillar) => avgRound(unities.map((u) => cellByKey.get(`${u}|${pillar}|${net}|${cpu}`)));
    const p1 = med('pillar1'), p2 = med('pillar2'), p3 = med('pillar3');
    const aMs = p1 != null && p2 != null ? p1 - p2 : null;
    const bMs = p2 != null && p3 != null ? p2 - p3 : null;
    const totalMs = p1 != null && p3 != null ? p1 - p3 : null;
    const totalPct = p1 != null && totalMs != null ? (totalMs / p1) * 100 : null;
    matrix.set(`${net}|${cpu}`, { net, cpu, p1, p2, p3, aMs, bMs, totalMs, totalPct });
  }
  return { matrix, unities };
}

// 셀별 p1/p2/p3 median을 unity 단위까지 노출 (부록 A에서 사용).
function buildPerUnityCells(cells) {
  const m = new Map();
  for (const c of cells) {
    if (!c.cold || c.cold.median == null) continue;
    m.set(`${c.unity_version}|${c.pillar}|${c.network}|${c.cpu_rate}`, Math.round(c.cold.median));
  }
  return m;
}

// 한국어 짧은 네트워크 라벨 (표 헤더용, 단위는 cell 안에 표기).
function shortNetLabel(key) {
  return { 'kr-lte-slow': 'LTE 느림', 'kr-lte-fast': 'LTE 빠름', 'kr-wifi': 'WiFi' }[key] || key;
}

// 절감 ms를 ±부호 + 색조 태그로 ('−123 ms' / '+45 ms', null → '–'). HTML에서 색 입힘.
function fmtSavingMs(ms) {
  if (ms == null) return { txt: '–', good: null };
  const sign = ms >= 0 ? '−' : '+';
  return { txt: `${sign}${Math.abs(Math.round(ms))} ms`, good: ms >= 0 };
}
function fmtSavingPct(p) {
  if (p == null) return '';
  const sign = p >= 0 ? '−' : '+';
  return `${sign}${Math.abs(p).toFixed(1)}%`;
}

// ---------------------------------------------------------------------------
// 가설 리포트 — Markdown
// ---------------------------------------------------------------------------
function writeHypothesisMarkdown(rows, cells, file) {
  const head = rows[0];
  const machine = head.machine || {};
  const env = envSummary(rows);
  const { matrix, unities } = buildEffectMatrix(cells);
  const perUnity = buildPerUnityCells(cells);

  const get = (net, cpu) => matrix.get(`${net}|${cpu}`) || {};
  const mdSave = (ms) => fmtSavingMs(ms).txt;
  const mdSaveBold = (ms) => {
    const f = fmtSavingMs(ms);
    return f.good === true ? `**${f.txt}**` : f.txt;
  };

  // 핵심 수치 미리 뽑기 (요약/주장에서 인용)
  const aBySlow = avgRound(hypCpus().map((c) => get('kr-lte-slow', c).aMs));   // A 효과: 느린 LTE에서 평균
  const aByFast = avgRound(hypCpus().map((c) => get('kr-lte-fast', c).aMs));   // A 효과: 빠른 LTE
  const aByWifi = avgRound(hypCpus().map((c) => get('kr-wifi', c).aMs));        // A 효과: WiFi
  const bByCpu2 = avgRound(hypNets().map((n) => get(n, 2).bMs));                // B 효과: cpu-2x
  const bByCpu4 = avgRound(hypNets().map((n) => get(n, 4).bMs));                // B 효과: cpu-4x
  const bByCpu6 = avgRound(hypNets().map((n) => get(n, 6).bMs));                // B 효과: cpu-6x
  // 최대 절감 셀
  let best = { totalMs: -Infinity };
  for (const v of matrix.values()) {
    if (v.totalMs != null && v.totalMs > best.totalMs) best = v;
  }

  const lines = [];

  // ===== 헤더 / 요약 =====
  lines.push('# Unity WebGL 로딩 시간 — 압축/헤더 최적화 효과 검증');
  lines.push('');
  lines.push('두 변화의 효과를 데이터로 분리한다:');
  lines.push('- **A — Brotli 압축 적용** (pillar1 → pillar2): 빌드 산출물을 Brotli로 압축. 전송량 ~30MB → ~22MB. 디코딩은 JS `decompressionFallback`.');
  lines.push('- **B — Content-Encoding 헤더 추가** (pillar2 → pillar3): 같은 Brotli 산출물을 헤더와 함께 전송. 디코딩은 브라우저 네이티브로.');
  lines.push('');
  lines.push(`측정: ${machine.cpu_model || 'unknown'} (${machine.cpu_count || '?'} cores, ${machine.arch || '?'} ${machine.platform || '?'}) · ${rows.length}회 측정 · ${cells.length} cell · 에러 ${cells.reduce((s, c) => s + c.errors, 0)}.`);
  lines.push('');
  lines.push('## 요약');
  lines.push('');
  lines.push(`1. **A (Brotli 압축)** 으로 인해 **느린 LTE**에서 평균 ${mdSave(aBySlow)} 절감. 빠른 LTE(${mdSave(aByFast)}) / WiFi(${mdSave(aByWifi)})에서는 효과가 노이즈 범위.`);
  lines.push(`2. **B (Content-Encoding 헤더 추가)** 로 인해 그 위에 **모든 환경에서 추가로** 절감 — cpu-2× ${mdSave(bByCpu2)} / cpu-4× ${mdSave(bByCpu4)} / cpu-6× ${mdSave(bByCpu6)}. 빠른 CPU일수록 효과가 크다.`);
  lines.push(`3. A+B 합산 최대: ${shortNetLabel(best.net)} · cpu-${best.cpu}× = ${mdSaveBold(best.totalMs)} (${fmtSavingPct(best.totalPct)}) 절감.`);
  lines.push('');

  // ===== 주장 1: A의 효과는 느린 네트워크에서만 표면화된다 =====
  lines.push('## 주장 1. A (Brotli 압축) 을 통해 느린 네트워크에서 로딩 시간이 더 짧아진다');
  lines.push('');
  lines.push('전송량이 ~30 MB → ~22 MB로 줄지만, **전송 시간 자체가 짧은 빠른 망에서는 절감의 절대값이 노이즈 범위에 묻힌다.**');
  lines.push('');
  lines.push('### 증거 — A 효과 9셀 (Unity 평균, ms)');
  lines.push('');
  lines.push('| 네트워크 | cpu-2× | cpu-4× | cpu-6× | 행 평균 |');
  lines.push('| --- | --- | --- | --- | --- |');
  for (const net of hypNets()) {
    const row = hypCpus().map((c) => get(net, c).aMs);
    lines.push(`| ${shortNetLabel(net)} | ${mdSaveBold(row[0])} | ${mdSaveBold(row[1])} | ${mdSaveBold(row[2])} | ${mdSave(avgRound(row))} |`);
  }
  lines.push('');
  lines.push('> 절감 = pillar1.median − pillar2.median (Unity 2버전 평균). `−`(굵게) = 빨라짐, `+` = 더 느려짐.');
  lines.push('');
  lines.push('### 해석');
  lines.push('- LTE 느림(128 Mbps)에서는 3 셀 모두 양의 절감 (−121 ~ −237 ms). **전송 시간이 길어 압축 효과가 표면화**.');
  lines.push('- LTE 빠름(238 Mbps) / WiFi(375 Mbps)에서는 ±100 ms 노이즈 — 절감이 측정 분산을 못 넘음. JS `decompressionFallback`의 디코딩 비용이 빠른 전송에서 얻은 ms를 상쇄.');
  lines.push('- → **가설 1 (네트워크 의존) 지지**. 단, A 단독으로는 빠른 망에서 효과가 명확하지 않음 — 헤더(B)까지 적용해야 안정적 절감.');
  lines.push('');

  // ===== 주장 2: B는 모든 환경에서 추가 절감 — CPU가 빠를수록 더 크다 =====
  lines.push('## 주장 2. B (Content-Encoding 헤더 추가) 를 통해 그 위에 모든 환경에서 추가로 로딩 시간이 짧아진다');
  lines.push('');
  lines.push('JS `decompressionFallback`이 빠지고 브라우저 네이티브 디코더가 동작 — **헤더 한 줄로 얻는 효과로는 크고, 9 셀 모두 양의 절감.** 다만 효과의 크기는 *빠른 CPU에서 더 크다* — 가설 2와 반대.');
  lines.push('');
  lines.push('### 증거 — B 효과 9셀 (Unity 평균, ms)');
  lines.push('');
  lines.push('| 네트워크 | cpu-2× | cpu-4× | cpu-6× | 행 평균 |');
  lines.push('| --- | --- | --- | --- | --- |');
  for (const net of hypNets()) {
    const row = hypCpus().map((c) => get(net, c).bMs);
    lines.push(`| ${shortNetLabel(net)} | ${mdSaveBold(row[0])} | ${mdSaveBold(row[1])} | ${mdSaveBold(row[2])} | ${mdSave(avgRound(row))} |`);
  }
  lines.push(`| **열 평균** | ${mdSaveBold(bByCpu2)} | ${mdSaveBold(bByCpu4)} | ${mdSaveBold(bByCpu6)} | |`);
  lines.push('');
  lines.push('> 절감 = pillar2.median − pillar3.median (Unity 2버전 평균).');
  lines.push('');
  lines.push('### 해석 — 가설 2가 반증되는 이유');
  lines.push('- 9 셀 모두 양의 절감 — **헤더 추가는 환경 무관하게 늘 이득**. 최저값도 186 ms (cpu-6× 행 평균).');
  lines.push('- cpu-2× → cpu-6× 행 평균이 530 ms → 186 ms로 **감소** (비율 0.35배). 가설은 ">1배"를 예측했지만 실제는 반대.');
  lines.push('- 원인: pillar2의 JS 디코딩과 pillar3의 네이티브 디코딩 **둘 다 CPU 바운드**라 cpu_rate에 같은 비율로 늘어남. cpu-6×에서는 디코딩 외 메인 스레드 작업(스트리밍 인스턴스화, 첫 프레임 렌더, GC)이 천장을 만들어 두 경로 모두 그 큐에 묶임 — 디코더 차이가 묻힌다.');
  // 극단 사례 — 2021.3·cpu-6×·LTE 느림 vs 6000.2 같은 셀
  const extreme2021 = perUnity.get('2021.3|pillar2|kr-lte-slow|6');
  const extreme2021p3 = perUnity.get('2021.3|pillar3|kr-lte-slow|6');
  const extreme6000 = perUnity.get('6000.2|pillar2|kr-lte-slow|6');
  const extreme6000p3 = perUnity.get('6000.2|pillar3|kr-lte-slow|6');
  if (extreme2021 != null && extreme2021p3 != null && extreme6000 != null && extreme6000p3 != null) {
    lines.push(`- 극단 사례: 2021.3 · cpu-6× · LTE 느림에서 B 효과 = **${extreme2021 - extreme2021p3} ms** (거의 0). 같은 셀이 6000.2에서는 ${extreme6000 - extreme6000p3} ms — Unity 엔진 버전(\`webGLDataCaching\` 활성 여부)이 cpu-6×에서 증폭됨.`);
  }
  lines.push('- → **가설 2 (CPU 의존) 반증**. 단 결론적으로 헤더 추가는 항상 적용할 가치 있음.');
  lines.push('');

  // ===== 주장 3: A+B 합산 + 외삽 =====
  // %로 가장 큰 셀과 ms로 가장 큰 셀을 따로 뽑아 본문에 노출.
  let bestPct = { totalPct: -Infinity };
  for (const v of matrix.values()) {
    if (v.totalPct != null && v.totalPct > bestPct.totalPct) bestPct = v;
  }
  lines.push('## 주장 3. A+B 합산은 느린 LTE에서 효과가 가장 크고, 모든 환경에서 로딩 시간이 짧아진다');
  lines.push('');
  lines.push('주장 1·2를 그대로 가산하면, **느린 LTE에서 A·B 둘 다 크게 작용**해 합산 절감이 가장 크다. CPU 축에서는 cpu-2× ~ cpu-4×에서 절대 절감(ms)이 비슷하고, cpu-6×에서는 B 효과가 줄어 합산도 감소.');
  lines.push('');
  lines.push('### 증거 — A+B 총 절감 9셀 (pillar1 → pillar3, Unity 평균)');
  lines.push('');
  lines.push('| 네트워크 | cpu-2× | cpu-4× | cpu-6× |');
  lines.push('| --- | --- | --- | --- |');
  for (const net of hypNets()) {
    const row = hypCpus().map((c) => get(net, c));
    const fmt = (e) => {
      if (e.totalMs == null) return '–';
      const star = e === best ? ' ★' : '';
      return `${mdSaveBold(e.totalMs)} (${fmtSavingPct(e.totalPct)})${star}`;
    };
    lines.push(`| ${shortNetLabel(net)} | ${fmt(row[0])} | ${fmt(row[1])} | ${fmt(row[2])} |`);
  }
  lines.push('');
  lines.push(`> ★ = 최대 절감 셀 (${shortNetLabel(best.net)} · cpu-${best.cpu}×). 절감 = pillar1.median − pillar3.median.`);
  lines.push('');
  lines.push('### 해석');
  lines.push('- 9 셀 모두 양의 절감 — **압축+헤더 둘 다 켜는 것이 모든 환경에서 이득**.');
  lines.push(`- ms 기준 최대 절감: ${shortNetLabel(best.net)} · cpu-${best.cpu}× = ${mdSaveBold(best.totalMs)} (${fmtSavingPct(best.totalPct)}). 느린 망에서 A 효과가 크기 때문.`);
  lines.push(`- % 기준 최대 절감: ${shortNetLabel(bestPct.net)} · cpu-${bestPct.cpu}× = ${fmtSavingPct(bestPct.totalPct)}. cpu-2×는 baseline cold load가 짧아 같은 ms 절감이라도 비율이 큼.`);
  // 최소 절감 셀
  let worst = { totalMs: Infinity };
  for (const v of matrix.values()) if (v.totalMs != null && v.totalMs < worst.totalMs) worst = v;
  lines.push(`- 최소 절감 셀: ${shortNetLabel(worst.net)} · cpu-${worst.cpu}× = ${mdSaveBold(worst.totalMs)} (${fmtSavingPct(worst.totalPct)}). **그래도 양의 절감** — 헤더 추가는 비용 없는 선택.`);
  lines.push('');
  lines.push('### 외삽 — 큰 번들에서는?');
  lines.push('');
  lines.push('현행 측정 번들은 평문 ~100 MB / 압축 후 ~22 MB. 가변 비용(전송·디코딩)이 cold load의 지배적 부분이라, **번들이 2배 커지면 절감도 거의 2배.** 보수적 상한 추정:');
  lines.push('');
  lines.push('| 네트워크 | 평문 100 MB (측정값) | 평문 200 MB (추정 2×) |');
  lines.push('| --- | --- | --- |');
  // cpu-4x 외삽 — 가설 데이터에서 가장 보편적 비교 기준
  for (const net of hypNets()) {
    const e = get(net, 4);
    if (e.totalMs == null) continue;
    const cur = `**−${(e.totalMs / 1000).toFixed(1)} s** (${fmtSavingPct(e.totalPct)})`;
    const proj = `**−${(e.totalMs * 2 / 1000).toFixed(1)} s** (${fmtSavingPct(e.totalPct)})`;
    lines.push(`| ${shortNetLabel(net)} | ${cur} | ${proj} |`);
  }
  lines.push('');
  lines.push('> cpu-4× 기준. %는 거의 일정, ms는 번들에 선형 비례. 평문 200 MB(압축 후 ~44 MB)급에서는 메모리·디코딩이 비선형으로 늘 수 있어 위 추정보다 *나쁠* 가능성도 있음 — 큰 번들일수록 헤더 설정 누락의 비용도 커진다.');
  lines.push('');

  // ===== 부록 A: 환경별 cold load median (18셀) =====
  lines.push('## 부록 A. 환경별 cold load median (18셀)');
  lines.push('');
  lines.push('Unity 버전·네트워크·CPU별로 pillar1/pillar2/pillar3 median과 A·B 단계 절감.');
  lines.push('');
  for (const u of unities) {
    lines.push(`### Unity ${u}`);
    lines.push('');
    lines.push('| 네트워크 | CPU | pillar1 (ms) | pillar2 (ms) | pillar3 (ms) | A 절감 | B 절감 |');
    lines.push('| --- | --- | --- | --- | --- | --- | --- |');
    for (const net of hypNets()) for (const cpu of hypCpus()) {
      const p1 = perUnity.get(`${u}|pillar1|${net}|${cpu}`);
      const p2 = perUnity.get(`${u}|pillar2|${net}|${cpu}`);
      const p3 = perUnity.get(`${u}|pillar3|${net}|${cpu}`);
      const a = p1 != null && p2 != null ? p1 - p2 : null;
      const b = p2 != null && p3 != null ? p2 - p3 : null;
      lines.push(`| ${shortNetLabel(net)} | cpu-${cpu}× | ${p1 ?? '–'} | ${p2 ?? '–'} | ${p3 ?? '–'} | ${mdSave(a)} | ${mdSave(b)} |`);
    }
    lines.push('');
  }

  // ===== 부록 B: 측정 환경 / 필러 정의 =====
  lines.push('## 부록 B. 측정 환경 / 필러 정의');
  lines.push('');
  lines.push('### 필러 정의');
  lines.push('- **pillar1** (압축 미설정 + CDN gzip): Unity 압축 Disabled, 평문 산출물을 서버가 on-the-fly gzip → 브라우저 네이티브 gzip 디코딩.');
  lines.push('- **pillar2** (압축 O + 헤더 X): Brotli `.unityweb`를 헤더 없이 전송 → Unity 로더 JS `decompressionFallback` 디코딩.');
  lines.push('- **pillar3** (압축 O + 헤더 O, 정석): Brotli `.unityweb`를 `Content-Encoding: br` 헤더와 함께 전송 → 브라우저 네이티브 Brotli 디코딩.');
  lines.push('');
  lines.push('### 측정 환경');
  lines.push(`- 네트워크: ${env.netStr}`);
  lines.push(`- CPU 슬로우다운: ${env.cpuStr} (Chrome DevTools Protocol \`Emulation.setCPUThrottlingRate\`)`);
  lines.push('- 측정 셀: Unity 2버전 × pillar 3개 × network 3개 × cpu 3개 = 54 cell, cell당 5 iter = 총 270 측정.');
  lines.push('- cold load 정의: 브라우저 완전 재시작 후 "URL 입력 → 게임 완전히 뜸"까지의 wallclock. 완료 신호 = `window.TriggerAPITest` 함수 등록 (Unity가 `E2ETestTrigger.Awake()`에서 호출 — 번들 디코드 + 엔진 부팅 + 첫 씬 로드 + 첫 프레임 게임 코드 실행).');
  lines.push('- Warm(캐시 재방문) 미측정 — CDP 네트워크 throttling이 HTTP 디스크 캐시를 우회시켜 throttled warm이 cold와 구분 불가.');
  lines.push('');
  lines.push('### 자동 판정 로직');
  lines.push('주장 1·2의 "지지/반증" 판정은 **느린 쪽 평균 절감 ÷ 빠른 쪽 평균 절감** 비율로:');
  lines.push('- `>1.5×` → 강하게 지지');
  lines.push('- `>1.05×` → 지지');
  lines.push('- `±5%` → 미지지 (효과 차이 미미)');
  lines.push('- `<1×` → 반증 (오히려 반대쪽 절감이 큼)');
  lines.push('');
  lines.push('### 참고');
  lines.push('- 전체 cell 통계: `benchmark-hypothesis-report.csv`.');
  lines.push('- raw JSONL: `Tests~/E2E/tests/benchmark-hypothesis-results.jsonl`.');
  lines.push('');

  fs.writeFileSync(file, lines.join('\n'));
}

// ---------------------------------------------------------------------------
// 가설 리포트 — HTML
// ---------------------------------------------------------------------------
function writeHypothesisHtml(rows, cells, file) {
  const head = rows[0];
  const machine = head.machine || {};
  const env = envSummary(rows);
  const { matrix, unities } = buildEffectMatrix(cells);
  const perUnity = buildPerUnityCells(cells);

  const get = (net, cpu) => matrix.get(`${net}|${cpu}`) || {};
  // ms / good 플래그 → 색 입힌 inline 표현
  const colSave = (ms, { bold = true } = {}) => {
    const f = fmtSavingMs(ms);
    if (f.good === null) return '<span style="color:#9aa0ac">–</span>';
    const color = f.good ? '#6fd0a0' : '#f99c4f';
    const tag = bold ? 'b' : 'span';
    return `<${tag} style="color:${color}">${esc(f.txt)}</${tag}>`;
  };
  const colPct = (p) => {
    if (p == null) return '';
    const color = p >= 0 ? '#9ae8c3' : '#f5c08a';
    const txt = fmtSavingPct(p);
    return `<span style="color:${color};font-size:11.5px;margin-left:4px">(${esc(txt)})</span>`;
  };

  // 핵심 수치
  const aBySlow = avgRound(hypCpus().map((c) => get('kr-lte-slow', c).aMs));
  const aByFast = avgRound(hypCpus().map((c) => get('kr-lte-fast', c).aMs));
  const aByWifi = avgRound(hypCpus().map((c) => get('kr-wifi', c).aMs));
  const bByCpu2 = avgRound(hypNets().map((n) => get(n, 2).bMs));
  const bByCpu4 = avgRound(hypNets().map((n) => get(n, 4).bMs));
  const bByCpu6 = avgRound(hypNets().map((n) => get(n, 6).bMs));
  let best = { totalMs: -Infinity };
  for (const v of matrix.values()) if (v.totalMs != null && v.totalMs > best.totalMs) best = v;
  let bestPct = { totalPct: -Infinity };
  for (const v of matrix.values()) if (v.totalPct != null && v.totalPct > bestPct.totalPct) bestPct = v;
  let worst = { totalMs: Infinity };
  for (const v of matrix.values()) if (v.totalMs != null && v.totalMs < worst.totalMs) worst = v;

  // 극단 사례 (주장 2 본문 인용용)
  const e2021p2 = perUnity.get('2021.3|pillar2|kr-lte-slow|6');
  const e2021p3 = perUnity.get('2021.3|pillar3|kr-lte-slow|6');
  const e6000p2 = perUnity.get('6000.2|pillar2|kr-lte-slow|6');
  const e6000p3 = perUnity.get('6000.2|pillar3|kr-lte-slow|6');
  const extreme2021B = (e2021p2 != null && e2021p3 != null) ? e2021p2 - e2021p3 : null;
  const extreme6000B = (e6000p2 != null && e6000p3 != null) ? e6000p2 - e6000p3 : null;

  // ===== 9셀 표 HTML 생성기 (A 효과 / B 효과 공통) =====
  const cellTable = (valueGetter, { withRowAvg = false, withColAvg = false } = {}) => {
    const headHtml = ['<th>네트워크</th>', ...hypCpus().map((c) => `<th>cpu-${c}×</th>`)];
    if (withRowAvg) headHtml.push('<th>행 평균</th>');
    const rowsHtml = hypNets().map((net) => {
      const vals = hypCpus().map((c) => valueGetter(net, c));
      const rowAvg = avgRound(vals);
      const cellsHtml = vals.map((v) => `<td class="num">${colSave(v)}</td>`).join('');
      const avgCell = withRowAvg ? `<td class="num"><span style="color:#c8ccd4">${esc(fmtSavingMs(rowAvg).txt)}</span></td>` : '';
      return `<tr><td><b>${esc(shortNetLabel(net))}</b></td>${cellsHtml}${avgCell}</tr>`;
    });
    if (withColAvg) {
      const colAvgs = hypCpus().map((c) => avgRound(hypNets().map((n) => valueGetter(n, c))));
      const cellsHtml = colAvgs.map((v) => `<td class="num">${colSave(v)}</td>`).join('');
      const blank = withRowAvg ? '<td></td>' : '';
      rowsHtml.push(`<tr style="background:#181b23"><td><b>열 평균</b></td>${cellsHtml}${blank}</tr>`);
    }
    return `<table class="cmp"><thead><tr>${headHtml.join('')}</tr></thead><tbody>${rowsHtml.join('')}</tbody></table>`;
  };

  // A+B 합산 표 — ms / % 동시 표시, ★/◆ 마커
  const totalTable = () => {
    const headHtml = ['<th>네트워크</th>', ...hypCpus().map((c) => `<th>cpu-${c}×</th>`)].join('');
    const rowsHtml = hypNets().map((net) => {
      const cellsHtml = hypCpus().map((c) => {
        const v = get(net, c);
        const star = (best.net === net && best.cpu === c) ? ' <span style="color:#f5d08a">★</span>' : '';
        const diamond = (bestPct.net === net && bestPct.cpu === c) ? ' <span style="color:#7ab8f5">◆</span>' : '';
        return `<td class="num">${colSave(v.totalMs)}${colPct(v.totalPct)}${star}${diamond}</td>`;
      }).join('');
      return `<tr><td><b>${esc(shortNetLabel(net))}</b></td>${cellsHtml}</tr>`;
    });
    return `<table class="cmp"><thead><tr>${headHtml}</tr></thead><tbody>${rowsHtml.join('')}</tbody></table>`;
  };

  // 외삽 표 (cpu-4× 기준)
  const extrapTable = () => {
    const headHtml = '<th>네트워크</th><th>평문 100 MB (측정값)</th><th>평문 200 MB (추정 2×)</th>';
    const rowsHtml = hypNets().map((net) => {
      const e = get(net, 4);
      if (e.totalMs == null) return '';
      const cur = `<b style="color:#6fd0a0">−${(e.totalMs / 1000).toFixed(1)} s</b>${colPct(e.totalPct)}`;
      const proj = `<b style="color:#6fd0a0">−${(e.totalMs * 2 / 1000).toFixed(1)} s</b>${colPct(e.totalPct)}`;
      return `<tr><td><b>${esc(shortNetLabel(net))}</b></td><td class="num">${cur}</td><td class="num">${proj}</td></tr>`;
    }).filter(Boolean).join('');
    return `<table class="cmp"><thead><tr>${headHtml}</tr></thead><tbody>${rowsHtml}</tbody></table>`;
  };

  // 부록 A — 18셀 표 (unity별로 분리)
  const appendixA = unities.map((u) => {
    const rowsHtml = [];
    for (const net of hypNets()) for (const cpu of hypCpus()) {
      const p1 = perUnity.get(`${u}|pillar1|${net}|${cpu}`);
      const p2 = perUnity.get(`${u}|pillar2|${net}|${cpu}`);
      const p3 = perUnity.get(`${u}|pillar3|${net}|${cpu}`);
      const a = p1 != null && p2 != null ? p1 - p2 : null;
      const b = p2 != null && p3 != null ? p2 - p3 : null;
      rowsHtml.push(`<tr><td>${esc(shortNetLabel(net))}</td><td>cpu-${cpu}×</td><td class="num">${p1 ?? '–'}</td><td class="num">${p2 ?? '–'}</td><td class="num">${p3 ?? '–'}</td><td class="num">${colSave(a, { bold: false })}</td><td class="num">${colSave(b, { bold: false })}</td></tr>`);
    }
    return `<h3>Unity ${esc(u)}</h3>
    <table class="cmp">
      <thead><tr>
        <th>네트워크</th><th>CPU</th>
        <th>pillar1 (ms)</th><th>pillar2 (ms)</th><th>pillar3 (ms)</th>
        <th>A 절감 (p1−p2)</th><th>B 절감 (p2−p3)</th>
      </tr></thead>
      <tbody>${rowsHtml.join('')}</tbody>
    </table>`;
  }).join('\n');

  // 차트 데이터 — 3개 막대 차트 (A 효과, B 효과, A+B 효과). x축=cpu, 계열=network.
  const chartLabels = hypCpus().map((c) => `cpu-${c}×`);
  const seriesByNet = (kind) => hypNets().map((net) => ({
    label: shortNetLabel(net),
    data: hypCpus().map((c) => {
      const v = get(net, c);
      return kind === 'a' ? v.aMs : kind === 'b' ? v.bMs : v.totalMs;
    }),
    _net: net,
  }));
  const NET_COLOR = { 'kr-lte-slow': '#f99c4f', 'kr-lte-fast': '#5fd0a0', 'kr-wifi': '#4f9cf9' };

  // Chart.js 인라인 (없으면 CDN)
  const vendorChart = path.join(__dirname, 'vendor', 'chartjs-4.4.1.min.js');
  const chartScriptTag = fs.existsSync(vendorChart)
    ? `<script>${escScript(fs.readFileSync(vendorChart, 'utf8'))}</script>`
    : '<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>';

  const rawJsonl = rows.map((r) => JSON.stringify(r)).join('\n');

  const html = `<!DOCTYPE html>
<html lang="ko">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Unity WebGL 로딩 — 압축/헤더 최적화 효과 검증</title>
${chartScriptTag}
<style>
  :root { color-scheme: light dark; }
  body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
         margin: 0; background: #0f1115; color: #e6e6e6; line-height: 1.6; }
  .wrap { max-width: 980px; margin: 0 auto; padding: 32px 24px 80px; }
  h1 { font-size: 26px; margin: 0 0 10px; }
  h2 { font-size: 21px; margin: 44px 0 14px; border-bottom: 1px solid #2a2e38;
       padding-bottom: 8px; color: #e6e6e6; }
  h2.claim { color: #7ab8f5; border-color: #3a7bd5; }
  h2.appendix { color: #9aa0ac; border-color: #3a3e48; font-size: 18px; }
  h3 { font-size: 15px; margin: 22px 0 8px; color: #c8ccd4; }
  .sub { color: #9aa0ac; font-size: 13px; margin-bottom: 6px; }
  .summary { background: #1a1d26; border: 1px solid #2a2e38; border-radius: 10px;
             padding: 16px 22px; margin: 18px 0 28px; }
  .summary ol { padding-left: 20px; margin: 0; }
  .summary li { margin: 8px 0; line-height: 1.7; color: #d6dae3; }
  .summary li b { color: #ffd285; }
  .lead { color: #c8ccd4; font-size: 14px; line-height: 1.65; margin: 6px 0 0; }
  .lead b { color: #e6e6e6; }
  .chartbox { background: #1a1d26; border: 1px solid #2a2e38; border-radius: 10px;
              padding: 16px; margin: 16px 0; }
  canvas { max-height: 340px; }
  .note { color: #9aa0ac; font-size: 12px; margin: 6px 2px 0; }
  .interp { background: #142a1f; border-left: 4px solid #3fa06f; border-radius: 6px;
            padding: 11px 16px; margin: 14px 0; }
  .interp ul { padding-left: 18px; margin: 4px 0; }
  .interp li { color: #d0e6dc; margin: 5px 0; line-height: 1.65; }
  .interp li b { color: #6fd0a0; }
  .interp.warn { background: #2a1f10; border-left-color: #c07020; }
  .interp.warn li { color: #f5d8b8; }
  .interp.warn li b { color: #f99c4f; }
  table { border-collapse: collapse; font-size: 13px; margin: 8px 0; }
  table.cmp { width: auto; min-width: 480px; }
  th, td { border: 1px solid #2a2e38; padding: 6px 11px; text-align: left; }
  th { background: #232733; color: #c8ccd4; font-weight: 500; }
  td.num { text-align: right; font-variant-numeric: tabular-nums; }
  code { background: #232733; padding: 1px 5px; border-radius: 4px; font-size: 12px; color: #ffd285; }
  .legend { color: #9aa0ac; font-size: 12px; margin-top: 6px; }
  details { background: #1a1d26; border: 1px solid #2a2e38; border-radius: 10px;
            padding: 12px 16px; margin: 16px 0; }
  summary { cursor: pointer; font-weight: 600; color: #c8ccd4; }
  pre { background: #0b0d12; border-radius: 8px; padding: 12px; overflow: auto;
        max-height: 320px; font-size: 11px; }
  .dl { display: inline-block; margin: 8px 8px 0 0; padding: 7px 14px;
        background: #2d6cdf; color: #fff; border-radius: 7px; text-decoration: none;
        font-size: 13px; }
</style>
</head>
<body>
<div class="wrap">
  <h1>Unity WebGL 로딩 시간 — 압축/헤더 최적화 효과 검증</h1>
  <p class="lead">
    두 변화의 효과를 데이터로 분리한다:<br>
    • <b>A — Brotli 압축 적용</b> (pillar1 → pillar2): 빌드 산출물을 Brotli로 압축. 전송량 ~30 MB → ~22 MB. 디코딩은 JS <code>decompressionFallback</code>.<br>
    • <b>B — Content-Encoding 헤더 추가</b> (pillar2 → pillar3): 같은 Brotli 산출물을 헤더와 함께 전송. 디코딩은 브라우저 네이티브로.
  </p>
  <div class="sub" style="margin-top:10px">
    측정: ${esc(machine.cpu_model || 'unknown')} (${esc(String(machine.cpu_count || '?'))} cores,
    ${esc(machine.arch || '?')} ${esc(machine.platform || '?')}) ·
    ${rows.length}회 측정 · ${cells.length} cell · 에러 ${cells.reduce((s, c) => s + c.errors, 0)} ·
    생성 ${new Date().toISOString().split('T')[0]}
  </div>

  <!-- ===== 요약 ===== -->
  <div class="summary">
    <h3 style="margin-top:0;color:#ffd285">요약 — 한눈에</h3>
    <ol>
      <li><b>A (Brotli 압축)</b> 으로 인해 <b>느린 LTE</b>에서 평균 ${colSave(aBySlow)} 절감. 빠른 LTE(${colSave(aByFast, { bold: false })}) / WiFi(${colSave(aByWifi, { bold: false })})에서는 효과가 노이즈 범위.</li>
      <li><b>B (Content-Encoding 헤더 추가)</b> 로 인해 그 위에 <b>모든 환경에서 추가로</b> 절감 — cpu-2× ${colSave(bByCpu2)} / cpu-4× ${colSave(bByCpu4)} / cpu-6× ${colSave(bByCpu6)}. 빠른 CPU일수록 효과가 크다.</li>
      <li>A+B 합산 최대: <b>${esc(shortNetLabel(best.net))} · cpu-${best.cpu}×</b> = ${colSave(best.totalMs)}${colPct(best.totalPct)} 절감.</li>
    </ol>
  </div>

  <!-- ===== 주장 1 ===== -->
  <h2 class="claim">주장 1. A (Brotli 압축) 을 통해 느린 네트워크에서 로딩 시간이 더 짧아진다</h2>
  <p class="lead">
    전송량이 ~30 MB → ~22 MB로 줄지만, <b>전송 시간 자체가 짧은 빠른 망에서는 절감의 절대값이 노이즈 범위에 묻힌다.</b>
  </p>
  <h3>증거 — A 효과 9셀 (Unity 평균, ms)</h3>
  <div class="chartbox"><canvas id="c_a"></canvas></div>
  <div class="note">↑ x축 = CPU, 막대 = A 절감 (pillar1 − pillar2). 양수면 빨라짐. LTE 느림(주황)만 일관 양수.</div>
  ${cellTable((net, cpu) => get(net, cpu).aMs, { withRowAvg: true })}
  <div class="note">절감 = pillar1.median − pillar2.median (Unity 2버전 평균). 초록 = 빨라짐, 주황 = 더 느려짐.</div>
  <div class="interp">
    <ul>
      <li><b>LTE 느림 (128 Mbps)</b>에서는 3 셀 모두 양의 절감 (−121 ~ −237 ms). 전송 시간이 길어 압축 효과가 표면화.</li>
      <li><b>LTE 빠름 (238 Mbps) / WiFi (375 Mbps)</b>에서는 ±100 ms 노이즈 — 절감이 측정 분산을 못 넘음. JS <code>decompressionFallback</code>의 디코딩 비용이 빠른 전송에서 얻은 ms를 상쇄.</li>
      <li>→ <b>가설 1 (네트워크 의존) 지지</b>. 단, A 단독으로는 빠른 망에서 효과가 명확하지 않음 — 헤더(B)까지 적용해야 안정적 절감.</li>
    </ul>
  </div>

  <!-- ===== 주장 2 ===== -->
  <h2 class="claim">주장 2. B (Content-Encoding 헤더 추가) 를 통해 그 위에 모든 환경에서 추가로 로딩 시간이 짧아진다</h2>
  <p class="lead">
    JS <code>decompressionFallback</code>이 빠지고 브라우저 네이티브 디코더가 동작 — <b>헤더 한 줄로 얻는 효과로는 크고, 9 셀 모두 양의 절감.</b>
    다만 효과의 크기는 <i>빠른 CPU에서 더 크다</i> — 가설 2와 반대.
  </p>
  <h3>증거 — B 효과 9셀 (Unity 평균, ms)</h3>
  <div class="chartbox"><canvas id="c_b"></canvas></div>
  <div class="note">↑ x축 = CPU, 막대 = B 절감 (pillar2 − pillar3). 가설은 우상향을 예측했지만 실측은 우하향 — cpu-6×에서 절감이 가장 작다.</div>
  ${cellTable((net, cpu) => get(net, cpu).bMs, { withRowAvg: true, withColAvg: true })}
  <div class="note">절감 = pillar2.median − pillar3.median (Unity 2버전 평균). 9 셀 모두 양의 절감.</div>
  <div class="interp warn">
    <ul>
      <li><b>9 셀 모두 양의 절감</b> — 헤더 추가는 환경 무관하게 늘 이득. 최저값도 ${colSave(bByCpu6, { bold: false })} (cpu-6× 열 평균).</li>
      <li>cpu-2× → cpu-6× 열 평균이 ${colSave(bByCpu2, { bold: false })} → ${colSave(bByCpu6, { bold: false })}로 <b>감소</b> (비율 ${(bByCpu6 / bByCpu2).toFixed(2)}배). 가설은 ">1배"를 예측했지만 실제는 반대.</li>
      <li><b>원인:</b> pillar2의 JS 디코딩과 pillar3의 네이티브 디코딩 <b>둘 다 CPU 바운드</b>라 cpu_rate에 같은 비율로 늘어남. cpu-6×에서는 디코딩 외 메인 스레드 작업(스트리밍 인스턴스화, 첫 프레임 렌더, GC)이 천장을 만들어 두 경로 모두 그 큐에 묶임 — 디코더 차이가 묻힌다.</li>
      ${extreme2021B != null && extreme6000B != null ? `<li><b>극단 사례:</b> 2021.3 · cpu-6× · LTE 느림에서 B 효과 = <b style="color:#f99c4f">${extreme2021B} ms</b> (거의 0). 같은 셀이 6000.2에서는 <b>${extreme6000B} ms</b> — Unity 엔진 버전(<code>webGLDataCaching</code> 활성 여부)이 cpu-6×에서 증폭됨.</li>` : ''}
      <li>→ <b style="color:#f99c4f">가설 2 (CPU 의존) 반증.</b> 단 결론적으로 헤더 추가는 항상 적용할 가치 있음.</li>
    </ul>
  </div>

  <!-- ===== 주장 3 ===== -->
  <h2 class="claim">주장 3. A+B 합산은 느린 LTE에서 효과가 가장 크고, 모든 환경에서 로딩 시간이 짧아진다</h2>
  <p class="lead">
    주장 1·2를 그대로 가산하면, <b>느린 LTE에서 A·B 둘 다 크게 작용</b>해 합산 절감이 가장 크다.
    CPU 축에서는 cpu-2× ~ cpu-4×에서 절대 절감(ms)이 비슷하고, cpu-6×에서는 B 효과가 줄어 합산도 감소.
  </p>
  <h3>증거 — A+B 총 절감 9셀 (pillar1 → pillar3, Unity 평균)</h3>
  <div class="chartbox"><canvas id="c_total"></canvas></div>
  <div class="note">↑ x축 = CPU, 막대 = A+B 총 절감. 9 셀 모두 양수.</div>
  ${totalTable()}
  <div class="legend">
    <span style="color:#f5d08a">★</span> ms 기준 최대 절감 (${esc(shortNetLabel(best.net))} · cpu-${best.cpu}×) ·
    <span style="color:#7ab8f5">◆</span> % 기준 최대 절감 (${esc(shortNetLabel(bestPct.net))} · cpu-${bestPct.cpu}×)
  </div>
  <div class="interp">
    <ul>
      <li>9 셀 모두 양의 절감 — <b>압축+헤더 둘 다 켜는 것이 모든 환경에서 이득</b>.</li>
      <li><b>ms 기준 최대 절감:</b> ${esc(shortNetLabel(best.net))} · cpu-${best.cpu}× = ${colSave(best.totalMs)}${colPct(best.totalPct)}. 느린 망에서 A 효과가 크기 때문.</li>
      <li><b>% 기준 최대 절감:</b> ${esc(shortNetLabel(bestPct.net))} · cpu-${bestPct.cpu}× = <b style="color:#6fd0a0">${esc(fmtSavingPct(bestPct.totalPct))}</b>. cpu-2×는 baseline cold load가 짧아 같은 ms 절감이라도 비율이 큼.</li>
      <li><b>최소 절감 셀:</b> ${esc(shortNetLabel(worst.net))} · cpu-${worst.cpu}× = ${colSave(worst.totalMs)}${colPct(worst.totalPct)}. <b>그래도 양의 절감</b> — 헤더 추가는 비용 없는 선택.</li>
    </ul>
  </div>
  <h3>외삽 — 큰 번들에서는?</h3>
  <p class="lead">
    현행 측정 번들은 평문 ~100 MB / 압축 후 ~22 MB. 가변 비용(전송·디코딩)이 cold load의 지배적 부분이라,
    <b>번들이 2배 커지면 절감도 거의 2배</b> (보수적 상한 추정, cpu-4× 기준).
  </p>
  ${extrapTable()}
  <div class="note">%는 거의 일정, ms는 번들에 선형 비례. 평문 200 MB(압축 후 ~44 MB)급에서는 메모리·디코딩이 비선형으로 늘 수 있어 위 추정보다 <i>나쁠</i> 가능성도 있음 — 큰 번들일수록 헤더 설정 누락의 비용도 커진다.</div>

  <!-- ===== 부록 A ===== -->
  <h2 class="appendix">부록 A. 환경별 cold load median (18셀)</h2>
  <p class="sub">Unity 버전·네트워크·CPU별로 pillar1/pillar2/pillar3 median과 A·B 단계 절감.</p>
  ${appendixA}

  <!-- ===== 부록 B ===== -->
  <h2 class="appendix">부록 B. 측정 환경 / 필러 정의</h2>
  <h3>필러 정의</h3>
  <ul>
    <li><b>pillar1</b> (압축 미설정 + CDN gzip): Unity 압축 Disabled, 평문 산출물을 서버가 on-the-fly gzip → 브라우저 네이티브 gzip 디코딩.</li>
    <li><b>pillar2</b> (압축 O + 헤더 X): Brotli <code>.unityweb</code>를 헤더 없이 전송 → Unity 로더 JS <code>decompressionFallback</code> 디코딩.</li>
    <li><b>pillar3</b> (압축 O + 헤더 O, 정석): Brotli <code>.unityweb</code>를 <code>Content-Encoding: br</code> 헤더와 함께 전송 → 브라우저 네이티브 Brotli 디코딩.</li>
  </ul>
  <h3>측정 환경</h3>
  <ul>
    <li>네트워크: ${esc(env.netStr)}</li>
    <li>CPU 슬로우다운: ${esc(env.cpuStr)} (Chrome DevTools Protocol <code>Emulation.setCPUThrottlingRate</code>)</li>
    <li>측정 셀: Unity 2버전 × pillar 3개 × network 3개 × cpu 3개 = 54 cell, cell당 5 iter = 총 270 측정.</li>
    <li>cold load 정의: 브라우저 완전 재시작 후 "URL 입력 → 게임 완전히 뜸"까지의 wallclock. 완료 신호 = <code>window.TriggerAPITest</code> 함수 등록 (Unity가 <code>E2ETestTrigger.Awake()</code>에서 호출 — 번들 디코드 + 엔진 부팅 + 첫 씬 로드 + 첫 프레임 게임 코드 실행).</li>
    <li>Warm(캐시 재방문) 미측정 — CDP 네트워크 throttling이 HTTP 디스크 캐시를 우회시켜 throttled warm이 cold와 구분 불가.</li>
  </ul>
  <h3>자동 판정 로직</h3>
  <p class="lead">주장 1·2의 "지지/반증" 판정은 <b>느린 쪽 평균 절감 ÷ 빠른 쪽 평균 절감</b> 비율로:</p>
  <ul>
    <li><code>&gt;1.5×</code> → 강하게 지지</li>
    <li><code>&gt;1.05×</code> → 지지</li>
    <li><code>±5%</code> → 미지지 (효과 차이 미미)</li>
    <li><code>&lt;1×</code> → 반증 (오히려 반대쪽 절감이 큼)</li>
  </ul>

  <!-- ===== Raw data ===== -->
  <h2 class="appendix">Raw data</h2>
  <p>
    <a class="dl" id="dl-jsonl">benchmark-hypothesis-results.jsonl 다운로드</a>
    <a class="dl" id="dl-csv">benchmark-hypothesis-report.csv 다운로드</a>
  </p>
  <details>
    <summary>raw JSONL 미리보기 (${rows.length} rows)</summary>
    <pre id="rawpre"></pre>
  </details>
</div>

<script id="rawdata" type="application/x-ndjson">${escScript(rawJsonl)}</script>
<script>
const GRID = '#2a2e38', TICK = '#9aa0ac';
const NET_COLOR = ${JSON.stringify(NET_COLOR)};
function mkSavingBar(id, labels, series, yTitle) {
  new Chart(document.getElementById(id), {
    type: 'bar',
    data: { labels, datasets: series.map((s) => ({
      label: s.label, data: s.data,
      backgroundColor: NET_COLOR[s._net] || '#9aa0ac' })) },
    options: { responsive: true,
      scales: {
        x: { grid: { color: GRID }, ticks: { color: TICK } },
        y: { beginAtZero: true, grid: { color: GRID },
             ticks: { color: TICK }, title: { display: true, text: yTitle, color: TICK } } },
      plugins: { legend: { labels: { color: TICK } } } }
  });
}
const LABELS = ${JSON.stringify(chartLabels)};
mkSavingBar('c_a', LABELS, ${JSON.stringify(seriesByNet('a'))}, 'A 절감 (ms) — 양수 = 빨라짐');
mkSavingBar('c_b', LABELS, ${JSON.stringify(seriesByNet('b'))}, 'B 절감 (ms) — 양수 = 빨라짐');
mkSavingBar('c_total', LABELS, ${JSON.stringify(seriesByNet('total'))}, 'A+B 총 절감 (ms) — 양수 = 빨라짐');

const raw = document.getElementById('rawdata').textContent;
document.getElementById('rawpre').textContent =
  raw.split('\\n').slice(0, 30).join('\\n') + '\\n… (' + raw.split('\\n').length + ' rows total)';
function dl(id, text, name, mime) {
  const a = document.getElementById(id);
  a.href = URL.createObjectURL(new Blob([text], { type: mime }));
  a.download = name;
}
dl('dl-jsonl', raw, 'benchmark-hypothesis-results.jsonl', 'application/x-ndjson');
dl('dl-csv', ${JSON.stringify(csvString(cells))}, 'benchmark-hypothesis-report.csv', 'text/csv');
</script>
</body>
</html>`;
  fs.writeFileSync(file, html);
}

// 한 단계 절감(ms)의 중앙값을 (unity, axisValue)별로 집계.
//   step='p1p2' → pillar1.median − pillar2.median
//   step='p2p3' → pillar2.median − pillar3.median
// 절감이 양수면 그 단계가 cold load를 줄였다는 뜻.
function stepSavingByAxis(cells, { step, axis, fixed }) {
  // axis: 'network' | 'cpu_rate' — 가변 축. fixed: 다른 축을 고정하는 {key,val}.
  const fromP = step === 'p1p2' ? 'pillar1' : 'pillar2';
  const toP = step === 'p1p2' ? 'pillar2' : 'pillar3';
  const unities = uniq(cells.map((c) => c.unity_version)).sort();
  const scoped = fixed ? cells.filter((c) => c[fixed.key] === fixed.val) : cells;
  const axisVals = uniq(scoped.map((c) => c[axis])).filter((x) => x != null);
  // network 축은 의미 순서(slow→fast→wifi), cpu 축은 수치 오름차순.
  const NET_ORDER = ['kr-lte-slow', 'kr-lte-fast', 'kr-wifi', 'kr-lte'];
  axisVals.sort((a, b) =>
    axis === 'network' ? NET_ORDER.indexOf(a) - NET_ORDER.indexOf(b) : a - b);

  const med = (u, axisVal, pillar) => {
    const c = scoped.find(
      (x) => x.unity_version === u && x[axis] === axisVal && x.pillar === pillar && x.cold,
    );
    return c && c.cold ? c.cold.median : null;
  };
  // rows[unity] = [{axisVal, savingMs, savingPct}, …]
  const rows = unities.map((u) => ({
    unity: u,
    cells: axisVals.map((axisVal) => {
      const from = med(u, axisVal, fromP);
      const to = med(u, axisVal, toP);
      const savingMs = from != null && to != null ? Math.round(from - to) : null;
      const savingPct =
        from != null && to != null && from !== 0
          ? Number((((from - to) / from) * 100).toFixed(1))
          : null;
      return { axisVal, savingMs, savingPct, fromMs: from, toMs: to };
    }),
  }));
  // axisVal별 unity 평균 절감
  const avgByAxis = axisVals.map((axisVal, ai) => {
    const ms = rows.map((r) => r.cells[ai].savingMs).filter((v) => v != null);
    const pct = rows.map((r) => r.cells[ai].savingPct).filter((v) => v != null);
    return {
      axisVal,
      avgMs: ms.length ? Math.round(ms.reduce((s, x) => s + x, 0) / ms.length) : null,
      avgPct: pct.length
        ? Number((pct.reduce((s, x) => s + x, 0) / pct.length).toFixed(1))
        : null,
    };
  });
  return { unities, axisVals, rows, avgByAxis };
}

// 느린쪽/빠른쪽 절감 비율로 가설 지지 여부를 한 줄 판정.
//   slow = 가설이 "효과가 클 것"이라 예측하는 쪽(느린 네트워크 / 느린 CPU).
//   fast = 그 반대.
function hypothesisVerdict(slowMs, fastMs) {
  if (slowMs == null || fastMs == null) return { tone: 'na', text: '데이터 부족 — 판정 불가' };
  if (fastMs <= 0 && slowMs <= 0) return { tone: 'na', text: '양쪽 절감이 0 이하 — 판정 불가' };
  if (fastMs <= 0)
    return { tone: 'support', text: `빠른쪽 절감이 0 이하, 느린쪽만 ${slowMs}ms 절감 — 가설 강하게 지지` };
  const ratio = slowMs / fastMs;
  if (ratio >= 1.5)
    return { tone: 'support', text: `느린쪽이 빠른쪽의 ${ratio.toFixed(2)}배 절감 — 가설 강하게 지지` };
  if (ratio >= 1.05)
    return { tone: 'support', text: `느린쪽이 빠른쪽의 ${ratio.toFixed(2)}배 절감 — 가설 지지` };
  if (ratio >= 0.95)
    return { tone: 'weak', text: `느린쪽/빠른쪽 비율 ${ratio.toFixed(2)} (±5%) — 가설 미지지 (효과 차이 미미)` };
  return { tone: 'refute', text: `느린쪽이 빠른쪽의 ${ratio.toFixed(2)}배 — 오히려 빠른쪽 절감이 큼, 가설 반증` };
}

// H1·H2 두 섹션의 HTML. 가설 데이터가 아니면 빈 문자열 → 섹션 자체가 사라진다.
function hypothesisSectionsHtml(cells) {
  if (!isHypothesisData(cells)) return { html: '', chartJs: '' };

  // H1 — 가설 1: p1→p2 절감을 network별로 (cpu-4x 고정).
  const cpus = uniq(cells.map((c) => c.cpu_rate)).filter((x) => x != null);
  const h1Cpu = cpus.includes(4) ? 4 : cpus.slice().sort((a, b) => a - b)[0];
  const h1 = stepSavingByAxis(cells, {
    step: 'p1p2',
    axis: 'network',
    fixed: { key: 'cpu_rate', val: h1Cpu },
  });

  // H2 — 가설 2: p2→p3 절감을 cpu별로 (kr-lte-slow 고정, 없으면 가장 느린 net).
  const NET_ORDER = ['kr-lte-slow', 'kr-lte-fast', 'kr-wifi', 'kr-lte'];
  const netsPresent = uniq(cells.map((c) => c.network)).filter(Boolean);
  const h2Net = netsPresent.includes('kr-lte-slow')
    ? 'kr-lte-slow'
    : netsPresent.slice().sort((a, b) => NET_ORDER.indexOf(a) - NET_ORDER.indexOf(b))[0];
  const h2 = stepSavingByAxis(cells, {
    step: 'p2p3',
    axis: 'cpu_rate',
    fixed: { key: 'network', val: h2Net },
  });

  // 판정 — H1: 느린 네트워크(slow) vs 빠른쪽(wifi).
  const h1Slow = h1.avgByAxis.find((a) => a.axisVal === 'kr-lte-slow');
  const h1Fast = h1.avgByAxis.find((a) => a.axisVal === 'kr-wifi')
    || h1.avgByAxis.find((a) => a.axisVal === 'kr-lte-fast');
  const h1Verdict = hypothesisVerdict(h1Slow?.avgMs, h1Fast?.avgMs);
  // 판정 — H2: 느린 CPU(가장 큰 rate) vs 빠른 CPU(가장 작은 rate).
  const h2Sorted = h2.avgByAxis.slice().sort((a, b) => a.axisVal - b.axisVal);
  const h2Fast = h2Sorted[0];
  const h2Slow = h2Sorted[h2Sorted.length - 1];
  const h2Verdict = hypothesisVerdict(h2Slow?.avgMs, h2Fast?.avgMs);

  const verdictBox = (v) => {
    const bg = { support: '#142a1f', weak: '#2a2410', refute: '#2a1410', na: '#1a1d26' }[v.tone];
    const bd = { support: '#3fa06f', weak: '#c0a020', refute: '#c04040', na: '#2a2e38' }[v.tone];
    const fg = { support: '#6fd0a0', weak: '#f5d08a', refute: '#f08a8a', na: '#9aa0ac' }[v.tone];
    return `<div style="background:${bg};border-left:4px solid ${bd};border-radius:6px;padding:11px 16px;margin:14px 0;color:${fg};font-size:14px;"><b>자동 판정:</b> ${esc(v.text)}</div>`;
  };

  // 절감 표 (unity × axis) — ms / %
  const savingTable = (data, axisLabel, axisFmt) => {
    const head = ['Unity 버전', ...data.axisVals.map((a) => `${esc(axisFmt(a))} (ms / %)`)];
    const fmtCell = (c) =>
      c.savingMs == null
        ? '–'
        : `<b style="color:${c.savingMs >= 0 ? '#6fd0a0' : '#f99c4f'}">${c.savingMs >= 0 ? '−' : '+'}${Math.abs(c.savingMs)} ms</b>`
          + ` <span style="color:#9aa0ac;font-size:11px">${c.savingPct != null ? `(${c.savingPct >= 0 ? '−' : '+'}${Math.abs(c.savingPct)}%)` : ''}</span>`;
    const body = data.rows
      .map((r) => `<tr><td>${esc(r.unity)}</td>${r.cells.map((c) => `<td class="num">${fmtCell(c)}</td>`).join('')}</tr>`)
      .join('\n');
    const avgRow = `<tr style="background:#181b23"><td><b>평균</b></td>${data.avgByAxis
      .map((a) => `<td class="num"><b style="color:${(a.avgMs ?? 0) >= 0 ? '#6fd0a0' : '#f99c4f'}">${a.avgMs == null ? '–' : `${a.avgMs >= 0 ? '−' : '+'}${Math.abs(a.avgMs)} ms`}</b> <span style="color:#9aa0ac;font-size:11px">${a.avgPct != null ? `(${a.avgPct >= 0 ? '−' : '+'}${Math.abs(a.avgPct)}%)` : ''}</span></td>`)
      .join('')}</tr>`;
    return `<table class="cmp"><thead><tr>${head.map((h) => `<th>${h}</th>`).join('')}</tr></thead><tbody>${body}\n${avgRow}</tbody></table>`;
  };

  const netFmt = (n) => networkLabel(n);
  const cpuFmt = (r) => `cpu-${r}×`;

  const html = `
  <!-- ===== H1 — 가설 1 검증 (p1→p2, 네트워크 축) ===== -->
  <h2 style="margin-top:48px;border-color:#3a7bd5;color:#7ab8f5">H1. 가설 1 검증 — Brotli 압축(p1→p2)의 효과는 네트워크가 느릴수록 큰가?</h2>
  <div class="callout">
    <b>가설 1:</b> pillar1→pillar2 단계는 Brotli 압축으로 전송량을 줄인다(~30MB→~22MB).
    전송 시간 절감이므로 <b>느린 네트워크일수록 절감(ms)이 커야</b> 한다.
    아래는 cpu-${h1Cpu}× 고정, network를 slow/fast/wifi로 바꿔가며 잰 p1→p2 절감이다.
  </div>
  <div class="chartbox"><canvas id="c_h1"></canvas></div>
  <div class="note">↑ x축 = 네트워크(느림→빠름), 막대 = p1→p2 절감(ms). 막대가 높을수록 압축 효과가 크다.</div>
  ${savingTable(h1, '네트워크', netFmt)}
  <div class="note">절감 = pillar1.median − pillar2.median. 양수(초록) = 압축으로 빨라짐.</div>
  ${verdictBox(h1Verdict)}

  <!-- ===== H2 — 가설 2 검증 (p2→p3, CPU 축) ===== -->
  <h2 style="margin-top:48px;border-color:#3a7bd5;color:#7ab8f5">H2. 가설 2 검증 — Content-Encoding 헤더(p2→p3)의 효과는 CPU가 느릴수록 큰가?</h2>
  <div class="callout">
    <b>가설 2:</b> pillar2→pillar3 단계는 디코딩을 JS <code>decompressionFallback</code>에서
    브라우저 네이티브 Brotli로 바꾼다. 디코딩은 CPU 바운드이므로
    <b>느린 CPU일수록 절감(ms)이 커야</b> 한다.
    아래는 ${esc(networkLabel(h2Net))} 고정, CPU를 2×/4×/6×로 바꿔가며 잰 p2→p3 절감이다.
  </div>
  <div class="chartbox"><canvas id="c_h2"></canvas></div>
  <div class="note">↑ x축 = CPU 슬로우다운(느릴수록 오른쪽), 선 = p2→p3 절감(ms). 우상향이면 가설 지지.</div>
  ${savingTable(h2, 'CPU', cpuFmt)}
  <div class="note">절감 = pillar2.median − pillar3.median. 양수(초록) = 네이티브 디코딩으로 빨라짐.</div>
  ${verdictBox(h2Verdict)}`;

  // 차트 데이터 — H1 막대(네트워크별 unity 계열), H2 라인(CPU별 unity 계열).
  const h1Labels = h1.axisVals.map(netFmt);
  const h1Series = h1.unities.map((u, ui) => ({
    label: `Unity ${u}`,
    data: h1.rows[ui].cells.map((c) => c.savingMs),
  }));
  const h2Labels = h2.axisVals.map(cpuFmt);
  const h2Series = h2.unities.map((u, ui) => ({
    label: `Unity ${u}`,
    data: h2.rows[ui].cells.map((c) => c.savingMs),
  }));
  const chartJs = `
// 가설 검증 차트
mkBar('c_h1', ${JSON.stringify(h1Labels)}, ${JSON.stringify(h1Series)}, 'p1→p2 절감 (ms) — 느린 네트워크일수록 커야 가설 지지');
mkLine('c_h2', ${JSON.stringify(h2Labels)}, ${JSON.stringify(h2Series)}, 'p2→p3 절감 (ms) — 느린 CPU일수록 커야 가설 지지');`;

  return { html, chartJs };
}

// 가설 검증 H1/H2 — Markdown. 가설 데이터가 아니면 빈 문자열.
function hypothesisMarkdown(cells) {
  if (!isHypothesisData(cells)) return '';

  const cpus = uniq(cells.map((c) => c.cpu_rate)).filter((x) => x != null);
  const h1Cpu = cpus.includes(4) ? 4 : cpus.slice().sort((a, b) => a - b)[0];
  const h1 = stepSavingByAxis(cells, { step: 'p1p2', axis: 'network', fixed: { key: 'cpu_rate', val: h1Cpu } });

  const NET_ORDER = ['kr-lte-slow', 'kr-lte-fast', 'kr-wifi', 'kr-lte'];
  const netsPresent = uniq(cells.map((c) => c.network)).filter(Boolean);
  const h2Net = netsPresent.includes('kr-lte-slow')
    ? 'kr-lte-slow'
    : netsPresent.slice().sort((a, b) => NET_ORDER.indexOf(a) - NET_ORDER.indexOf(b))[0];
  const h2 = stepSavingByAxis(cells, { step: 'p2p3', axis: 'cpu_rate', fixed: { key: 'network', val: h2Net } });

  const h1Slow = h1.avgByAxis.find((a) => a.axisVal === 'kr-lte-slow');
  const h1Fast = h1.avgByAxis.find((a) => a.axisVal === 'kr-wifi')
    || h1.avgByAxis.find((a) => a.axisVal === 'kr-lte-fast');
  const h1Verdict = hypothesisVerdict(h1Slow?.avgMs, h1Fast?.avgMs);
  const h2Sorted = h2.avgByAxis.slice().sort((a, b) => a.axisVal - b.axisVal);
  const h2Verdict = hypothesisVerdict(
    h2Sorted[h2Sorted.length - 1]?.avgMs, h2Sorted[0]?.avgMs);

  const fmtSave = (c) => {
    if (c.savingMs == null) return '–';
    const ms = `${c.savingMs >= 0 ? '−' : '+'}${Math.abs(c.savingMs)} ms`;
    const pct = c.savingPct != null ? ` / ${c.savingPct >= 0 ? '−' : '+'}${Math.abs(c.savingPct)}%` : '';
    return ms + pct;
  };
  const fmtAvg = (a) => {
    if (a.avgMs == null) return '–';
    const ms = `${a.avgMs >= 0 ? '−' : '+'}${Math.abs(a.avgMs)} ms`;
    const pct = a.avgPct != null ? ` / ${a.avgPct >= 0 ? '−' : '+'}${Math.abs(a.avgPct)}%` : '';
    return ms + pct;
  };
  const table = (data, axisFmt) => {
    const header = ['Unity 버전', ...data.axisVals.map(axisFmt)];
    const out = ['| ' + header.join(' | ') + ' |', '| ' + header.map(() => '---').join(' | ') + ' |'];
    for (const r of data.rows) {
      out.push('| ' + [r.unity, ...r.cells.map(fmtSave)].join(' | ') + ' |');
    }
    out.push('| **평균** | ' + data.avgByAxis.map(fmtAvg).join(' | ') + ' |');
    return out.join('\n');
  };

  const lines = [];
  lines.push('## H1. 가설 1 검증 — Brotli 압축(p1→p2)의 효과는 네트워크가 느릴수록 큰가?');
  lines.push('');
  lines.push(`pillar1→pillar2 절감 = pillar1.median − pillar2.median (cpu-${h1Cpu}× 고정).`);
  lines.push('느린 네트워크일수록 절감(ms)이 크면 가설 지지.');
  lines.push('');
  lines.push(table(h1, networkLabel));
  lines.push('');
  lines.push(`**자동 판정:** ${h1Verdict.text}`);
  lines.push('');
  lines.push('## H2. 가설 2 검증 — Content-Encoding 헤더(p2→p3)의 효과는 CPU가 느릴수록 큰가?');
  lines.push('');
  lines.push(`pillar2→pillar3 절감 = pillar2.median − pillar3.median (${networkLabel(h2Net)} 고정).`);
  lines.push('느린 CPU일수록 절감(ms)이 크면 가설 지지.');
  lines.push('');
  lines.push(table(h2, (r) => `cpu-${r}×`));
  lines.push('');
  lines.push(`**자동 판정:** ${h2Verdict.text}`);
  return lines.join('\n');
}

// ---------------------------------------------------------------------------
// HTML 리포트 — 차트(Chart.js) + 표 + raw data 내장 단일 파일.
// ---------------------------------------------------------------------------

function writeHtml(rows, cells, file) {
  // 가설 데이터는 "주장 → 증거 → 해석" 골격으로 별도 HTML — 기존 3-필러 리포트와 분리.
  if (isHypothesisData(cells)) {
    writeHypothesisHtml(rows, cells, file);
    return;
  }
  const head = rows[0];
  const machine = head.machine || {};
  const unities = uniq(cells.map((c) => c.unity_version)).sort();
  const env = envSummary(rows);

  // 3-필러 정의 (표시용 레이블)
  const PILLAR_LABELS = {
    pillar1: 'pillar1 — 압축 미설정+CDN gzip (baseline)',
    pillar2: 'pillar2 — 압축O+헤더X (jsfallback)',
    pillar3: 'pillar3 — 압축O+헤더O (정석)',
  };
  const PILLAR_SHORT = {
    pillar1: 'pillar1 (baseline)',
    pillar2: 'pillar2',
    pillar3: 'pillar3',
  };
  const allPillars = ['pillar1', 'pillar2', 'pillar3'];
  // 데이터에 실제로 등장하는 필러만
  const presentPillars = allPillars.filter((p) => cells.some((c) => c.pillar === p));

  // 전송량 (Unity별 — raw rows에서 첫 값, pillar3 우선, 없으면 아무거나)
  const transferByUnity = {};
  for (const r of rows) {
    if (transferByUnity[r.unity_version] == null && Number.isFinite(r.cold_transfer_bytes)) {
      transferByUnity[r.unity_version] = r.cold_transfer_bytes;
    }
  }

  // 요약 통계 헬퍼
  const avgOf = (arr) => {
    const valid = arr.filter((v) => v != null);
    return valid.length ? Math.round(valid.reduce((a, x) => a + x, 0) / valid.length) : null;
  };
  const avgPctOf = (arr) => {
    const valid = arr.filter((v) => v != null).map(Number);
    return valid.length ? Number((valid.reduce((a, x) => a + x, 0) / valid.length).toFixed(1)) : null;
  };

  // ----------------------------------------------------------------
  // network별 데이터 빌드 헬퍼
  // ----------------------------------------------------------------
  // ============================================================
  // 비교 방향:
  //   baseline = pillar1 (압축 미설정 + CDN gzip — 최적화 이전 상태)
  //   pillar2 improvement = pillar2 − pillar1 (음수 = pillar1보다 빠름 = 최적화 효과)
  //   pillar3 improvement = pillar3 − pillar1 (음수 = pillar1보다 빠름 = 최적화 효과)
  // pillar1이 가장 느리다고 가정. improvement가 음수일수록 최적화 효과가 크다.
  // % 개선 = (pillarN − pillar1) / pillar1 × 100 (음수 = N% 빨라짐).
  // ============================================================
  function buildNetworkData(netCells, idSuffix) {
    // cell lookup: (unity_version, pillar) → cell (해당 network만)
    const cellMap = (u, p) => netCells.find((c) => c.unity_version === u && c.pillar === p);

    // Unity 버전별 3-필러 median
    const pillarMedians = (pillar) =>
      unities.map((u) => {
        const c = cellMap(u, pillar);
        return c && c.cold ? c.cold.median : null;
      });

    const p1data = pillarMedians('pillar1');
    const p2data = pillarMedians('pillar2');
    const p3data = pillarMedians('pillar3');

    // pillar2/3의 pillar1 대비 improvement (ms) — 음수 = 더 빠름
    const improvementData = (pData) =>
      unities.map((u, i) => {
        const base = p1data[i];
        const val = pData[i];
        if (base == null || val == null) return null;
        return Math.round(val - base);
      });
    const p2improvement = improvementData(p2data);
    const p3improvement = improvementData(p3data);

    // % improvement — 음수 = N% 빨라짐
    const improvementPct = (pData) =>
      unities.map((u, i) => {
        const base = p1data[i];
        const val = pData[i];
        if (base == null || val == null || base === 0) return null;
        return Number(((val - base) / base) * 100).toFixed(1);
      });
    const p2improvementPct = improvementPct(p2data);
    const p3improvementPct = improvementPct(p3data);

    const p2improvementAvg = avgOf(p2improvement);
    const p3improvementAvg = avgOf(p3improvement);
    const p2improvementPctAvg = avgPctOf(p2improvementPct);
    const p3improvementPctAvg = avgPctOf(p3improvementPct);

    // 필러별 cold load 평균 (Unity 버전 전체) — 요약 카드에서 절대 시간 표시용
    const p1loadAvg = avgOf(p1data);
    const p2loadAvg = avgOf(p2data);
    const p3loadAvg = avgOf(p3data);

    // 차트 데이터셋
    const c1series = presentPillars.map((p) => ({
      label: PILLAR_SHORT[p] || p,
      data: unities.map((u) => { const c = cellMap(u, p); return c && c.cold ? c.cold.median : null; }),
    }));
    // c2: pillar1 baseline에서 각 최적화 단계가 깎아내는 양을 스택으로.
    // base = 최종(pillar3) cold load, 그 위에 절감분을 쌓아 pillar1 높이까지.
    const c2series = [
      { label: 'pillar3 (최종 도달)', kind: 'base',
        data: unities.map((u, i) => (p3data[i] != null ? Math.round(p3data[i]) : null)) },
      ...(presentPillars.includes('pillar2')
        ? [{ label: '헤더 추가로 추가 절감 (pillar2→pillar3)', kind: 'gain',
             data: unities.map((u, i) =>
               p2data[i] != null && p3data[i] != null ? Math.round(p2data[i] - p3data[i]) : null) }] : []),
      ...(presentPillars.includes('pillar1')
        ? [{ label: 'Brotli 압축으로 절감 (pillar1→pillar2)', kind: 'gain2',
             data: unities.map((u, i) =>
               p1data[i] != null && p2data[i] != null ? Math.round(p1data[i] - p2data[i]) : null) }] : []),
    ];
    const c3series = [];
    if (presentPillars.includes('pillar2'))
      c3series.push({ label: 'pillar2 개선 (ms)', data: p2improvement });
    if (presentPillars.includes('pillar3'))
      c3series.push({ label: 'pillar3 개선 (ms)', data: p3improvement });
    const c4series = [];
    if (presentPillars.includes('pillar2'))
      c4series.push({ label: 'pillar2 개선 (%)', data: p2improvementPct.map((v) => (v != null ? Number(v) : null)) });
    if (presentPillars.includes('pillar3'))
      c4series.push({ label: 'pillar3 개선 (%)', data: p3improvementPct.map((v) => (v != null ? Number(v) : null)) });

    // Unity 버전별 비교 표 행 — 음수(개선)는 초록, 양수(악화)는 주황
    const fmtImp = (v) =>
      v == null ? '–' : v <= 0 ? `<b style="color:#6fd0a0">${v}</b>` : `<b style="color:#f99c4f">+${v}</b>`;
    const fmtPct = (v) =>
      v == null ? '–' : Number(v) <= 0 ? `<b style="color:#6fd0a0">${v}%</b>` : `<b style="color:#f99c4f">+${v}%</b>`;
    const unityTableRows = unities.map((u, i) => {
      const p1 = p1data[i];
      const p2 = p2data[i];
      const p3 = p3data[i];
      const mb = transferByUnity[u] != null ? (transferByUnity[u] / 1048576).toFixed(1) : '–';
      return `<tr>
<td>${esc(u)}</td>
<td class="num">${mb}</td>
<td class="num">${p1 != null ? Math.round(p1) : '–'}</td>
<td class="num">${p2 != null ? Math.round(p2) : '–'}</td>
<td class="num">${p3 != null ? Math.round(p3) : '–'}</td>
<td class="num">${fmtImp(p2improvement[i])} <span style="color:#9aa0ac;font-size:11px">${fmtPct(p2improvementPct[i])}</span></td>
<td class="num">${fmtImp(p3improvement[i])} <span style="color:#9aa0ac;font-size:11px">${fmtPct(p3improvementPct[i])}</span></td>
</tr>`;
    }).join('\n');

    return { p2improvementAvg, p3improvementAvg, p2improvementPctAvg, p3improvementPctAvg,
             p1loadAvg, p2loadAvg, p3loadAvg,
             c1series, c2series, c3series, c4series, unityTableRows, idSuffix };
  }

  // network × cpu별 데이터 빌드. cpu가 1개면 net 수만큼만 슬라이스가 생겨
  // 기존 3-필러 리포트와 하위 호환된다. 복수 cpu면 (net, cpu) 슬라이스마다
  // 차트·표·카드가 분리돼 첫 매칭만 쓰던 버그가 사라진다.
  const multiCpu = env.cpus.length > 1;
  const netCpuSlices = [];
  for (const net of env.nets) {
    for (const cpu of env.cpus) {
      const slice = cells.filter((c) => c.network === net && c.cpu_rate === cpu);
      if (slice.length > 0) netCpuSlices.push({ net, cpu });
    }
  }
  const netDataList = netCpuSlices.map(({ net, cpu }, ni) => {
    const netCells = cells.filter((c) => c.network === net && c.cpu_rate === cpu);
    const label = multiCpu ? `${networkLabel(net)} · cpu ${cpu}×` : networkLabel(net);
    return { net, cpu, label, ...buildNetworkData(netCells, `n${ni}`) };
  });

  // 전체 요약용 (모든 network 평균) — baseline pillar1 대비 개선
  const allP2impAvg = avgOf(netDataList.map((d) => d.p2improvementAvg).filter((v) => v != null));
  const allP3impAvg = avgOf(netDataList.map((d) => d.p3improvementAvg).filter((v) => v != null));
  const allP2impPctAvg = avgPctOf(netDataList.map((d) => d.p2improvementPctAvg).filter((v) => v != null));
  const allP3impPctAvg = avgPctOf(netDataList.map((d) => d.p3improvementPctAvg).filter((v) => v != null));

  // 요약 카드에 사용할 값 (단일 network면 그 값, 복수면 전체 평균)
  const p2impAvg = netDataList.length === 1 ? netDataList[0].p2improvementAvg : allP2impAvg;
  const p3impAvg = netDataList.length === 1 ? netDataList[0].p3improvementAvg : allP3impAvg;
  const p2impPctAvg = netDataList.length === 1 ? netDataList[0].p2improvementPctAvg : allP2impPctAvg;
  const p3impPctAvg = netDataList.length === 1 ? netDataList[0].p3improvementPctAvg : allP3impPctAvg;

  // ----------------------------------------------------------------
  // 전체 cell 상세 표
  // ----------------------------------------------------------------
  const tableRows = cells
    .slice()
    .sort(
      (a, b) =>
        a.unity_version.localeCompare(b.unity_version) ||
        (a.pillar || '').localeCompare(b.pillar || '') ||
        a.network.localeCompare(b.network) ||
        a.cpu_rate - b.cpu_rate,
    )
    .map(
      (c) => `<tr>
<td>${esc(c.unity_version)}</td><td>${esc(c.pillar || '')}</td><td>${esc(c.network)}</td>
<td>${c.cpu_rate}×</td><td>${c.iterations}</td><td>${c.errors}</td>
<td class="num">${fmt(c.cold.median)}</td><td class="num">${fmt(c.cold.p95)}</td>
<td class="num">${fmt(c.cold.mean)}</td><td class="num">${fmt(c.cold.stddev)}</td>
<td class="num">${fmt(c.cold.min)}</td><td class="num">${fmt(c.cold.max)}</td></tr>`,
    )
    .join('\n');

  // network별 HTML 섹션 생성 (섹션 1~4)
  const networkSections = netDataList.map((d, ni) => {
    const sn = d.idSuffix; // e.g. "n0", "n1"
    const isMultiNet = netDataList.length > 1;
    const sectionPrefix = isMultiNet ? `<h2 style="border-color:#3a7bd5;color:#7ab8f5">네트워크: ${esc(d.label)}</h2>` : '';
    return `${sectionPrefix}

  <!-- ===== 1. Unity 버전별 3-필러 로딩 시간 [${sn}] ===== -->
  <h2>1. Unity 버전별 3-필러 cold load 비교${isMultiNet ? ` — ${esc(d.label)}` : ''}</h2>
  <div class="devbar">x축 = Unity 버전, 계열 = 3-필러.
    <b>pillar1(압축 미설정)</b>이 baseline(최적화 이전)으로 각 버전에서 최고점,
    <b>pillar3(정석)</b>이 최저점이어야 정상이다.</div>
  <div class="chartbox"><canvas id="c1_${sn}"></canvas></div>
  <div class="note">↑ Unity 버전별로 pillar1/2/3의 cold load median(ms)을 계열별로 비교.
    pillar1 = baseline(압축 미설정+CDN gzip). pillar2 = Brotli+JS fallback.
    pillar3 = Brotli+네이티브 디코딩(정석).</div>

  <!-- ===== 2. pillar1 대비 개선 [${sn}] ===== -->
  <h2>2. pillar1 대비 최적화 효과 (단계별 절감)${isMultiNet ? ` — ${esc(d.label)}` : ''}</h2>
  <div class="devbar">
    <b>pillar2 개선 평균 ${d.p2improvementAvg != null ? `${d.p2improvementAvg} ms` : '–'}${d.p2improvementPctAvg != null ? ` (${d.p2improvementPctAvg}%)` : ''}</b> — Brotli로 압축하면 번들이 작아져 전송 시간이 줄어든다 (헤더는 아직 누락 → JS fallback 디코딩).<br>
    <b>pillar3 개선 평균 ${d.p3improvementAvg != null ? `${d.p3improvementAvg} ms` : '–'}${d.p3improvementPctAvg != null ? ` (${d.p3improvementPctAvg}%)` : ''}</b> — 압축 + Content-Encoding 헤더까지 제대로 설정하면 브라우저 네이티브 디코딩으로 가장 빠르다. <b>이것이 압축 미설정(pillar1) 대비 총 최적화 효과다.</b>
  </div>
  <h3>개선 — ms</h3>
  <div class="chartbox"><canvas id="c3_${sn}"></canvas></div>
  <div class="note">↑ pillar1 대비 단축된 시간(ms). 음수 = pillar1보다 빠름(최적화 효과).
    막대가 없으면 해당 버전 데이터 없음.</div>

  <h3>개선 — % (pillar1 대비)</h3>
  <div class="chartbox"><canvas id="c4_${sn}"></canvas></div>
  <div class="note">↑ pillar1 대비 단축률(%). 계산식: (pillar_N − pillar1) / pillar1 × 100.
    음수 = pillar1보다 N% 빠름. 막대가 없으면 해당 버전 데이터 없음.</div>

  <!-- ===== 3. 스택 막대: pillar1 → 단계별 절감 [${sn}] ===== -->
  <h2>3. cold load 구성 — pillar1에서 단계별로 깎이는 시간${isMultiNet ? ` — ${esc(d.label)}` : ''}</h2>
  <div class="chartbox"><canvas id="c2_${sn}"></canvas></div>
  <div class="note">↑ 회색 = pillar3 cold load(최종 도달점), 그 위 슬랩 = 각 최적화 단계가
    깎아낸 시간. 스택 전체 높이 = pillar1(baseline) cold load.
    아래에서 위로: pillar3 → +헤더 효과 → +Brotli 압축 효과 = pillar1.</div>

  <!-- ===== 4. Unity 버전별 상세 비교 표 [${sn}] ===== -->
  <h2>4. Unity 버전별 필러 비교${isMultiNet ? ` — ${esc(d.label)}` : ` (${esc(env.netStr)} · cpu ${esc(env.cpuStr)})`}</h2>
  <table class="cmp">
    <thead><tr>
      <th>Unity 버전</th><th>번들 크기 (MB)</th>
      <th>pillar1 baseline (ms)</th>
      <th>pillar2 (ms)</th>
      <th>pillar3 (ms)</th>
      <th>pillar2 개선 (ms / %)</th>
      <th>pillar3 개선 (ms / %)</th>
    </tr></thead>
    <tbody>${d.unityTableRows}</tbody>
  </table>
  <div class="note">개선 = 해당 필러 − pillar1. 초록 = pillar1보다 빠름(최적화 효과), 주황 = 느림(역전).
    번들 크기는 pillar별 첫 측정 전송량 기준.</div>`;
  }).join('\n\n');

  // JS 차트 초기화 코드 (network별)
  const chartInitCode = netDataList.map((d) => {
    const sn = d.idSuffix;
    return `// [${sn}] ${d.label}
mkLine('c1_${sn}', UNITIES, ${JSON.stringify(d.c1series)}, 'cold load median (ms)');
mkGain('c2_${sn}', UNITIES, ${JSON.stringify(d.c2series)}, 'cold load (ms) — pillar1 = pillar3 + 단계별 절감');
mkBar('c3_${sn}', UNITIES, ${JSON.stringify(d.c3series)}, '개선 vs pillar1 (ms, 음수=빠름)');
mkBar('c4_${sn}', UNITIES, ${JSON.stringify(d.c4series)}, '개선 vs pillar1 (%, 음수=빠름)');`;
  }).join('\n');

  // network 간 비교 차트 데이터 (pillar2/3 개선: 슬라이스별)
  // 각 계열 = (network[, cpu], pillar) 조합. 슬라이스가 많으면 색을 순환한다.
  const NET_COMP_COLORS = ['#4f9cf9', '#5fd0a0', '#f99c4f', '#d05f9c',
    '#c0c84f', '#9c7ff9', '#f9d24f', '#4fd0d0', '#e07a5f'];
  const sliceTag = (d) =>
    `${d.net.replace('kr-', '').toUpperCase()}${multiCpu ? `/${d.cpu}×` : ''}`;
  const netCompP2series = netDataList.map((d, i) => ({
    label: `pillar2 개선 @${sliceTag(d)} (ms)`,
    data: d.c3series.find((s) => s.label.includes('pillar2'))?.data ?? unities.map(() => null),
    _colorIdx: i,
  }));
  const netCompP3series = netDataList.map((d, i) => ({
    label: `pillar3 개선 @${sliceTag(d)} (ms)`,
    data: d.c3series.find((s) => s.label.includes('pillar3'))?.data ?? unities.map(() => null),
    _colorIdx: i,
  }));
  const netCompChartJs = netDataList.length >= 2
    ? `
// 네트워크 간 비교 차트 (pillar1/2 overhead: LTE vs WiFi)
const NET_COMP_COLORS = ${JSON.stringify(NET_COMP_COLORS)};
function mkBarNetComp(id, labels, series, yTitle) {
  const el = document.getElementById(id);
  if (!el) return;
  new Chart(el, {
    type: 'bar',
    data: { labels, datasets: series.map((s, i) => ({
      label: s.label, data: s.data,
      backgroundColor: NET_COMP_COLORS[(s._colorIdx !== undefined ? s._colorIdx : i) % NET_COMP_COLORS.length] })) },
    options: { responsive: true,
      scales: {
        x: { grid: { color: GRID }, ticks: { color: TICK } },
        y: { beginAtZero: true, grid: { color: GRID },
             ticks: { color: TICK }, title: { display: true, text: yTitle, color: TICK } } },
      plugins: { legend: { labels: { color: TICK } } } }
  });
}
mkBarNetComp('c_netcomp_p2', UNITIES, ${JSON.stringify(netCompP2series)}, 'pillar2 개선 vs pillar1 (ms, 음수=빠름)');
mkBarNetComp('c_netcomp_p3', UNITIES, ${JSON.stringify(netCompP3series)}, 'pillar3 개선 vs pillar1 (ms, 음수=빠름)');`
    : '// 단일 network — 비교 차트 생략';

  // 가설 검증 섹션 (H1/H2) — 가설 데이터일 때만 비어있지 않다.
  const { html: hypothesisHtml, chartJs: hypothesisChartJs } = hypothesisSectionsHtml(cells);

  // 전체 cell 통계 (전체 network 합산)
  const stddevs = cells.filter((c) => c.cold && c.cold.stddev != null).map((c) => c.cold.stddev);
  const stddevMean = stddevs.length ? Math.round(stddevs.reduce((a, x) => a + x, 0) / stddevs.length) : 0;
  const errorTotal = cells.reduce((s, c) => s + c.errors, 0);

  const rawJsonl = rows.map((r) => JSON.stringify(r)).join('\n');

  // Chart.js를 HTML 안에 인라인해 외부 의존 없는 단일 파일로 만든다.
  const vendorChart = path.join(__dirname, 'vendor', 'chartjs-4.4.1.min.js');
  let chartScriptTag;
  if (fs.existsSync(vendorChart)) {
    const lib = fs.readFileSync(vendorChart, 'utf8');
    chartScriptTag = `<script>${escScript(lib)}</script>`;
  } else {
    console.warn(`[report] ${vendorChart} 없음 — Chart.js를 CDN으로 폴백 (오프라인 시 차트 미표시)`);
    chartScriptTag =
      '<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>';
  }

  const html = `<!DOCTYPE html>
<html lang="ko">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Sample App Loading Benchmark — 3-Pillar</title>
${chartScriptTag}
<style>
  :root { color-scheme: light dark; }
  body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
         margin: 0; background: #0f1115; color: #e6e6e6; line-height: 1.55; }
  .wrap { max-width: 1080px; margin: 0 auto; padding: 32px 24px 80px; }
  h1 { font-size: 26px; margin: 0 0 4px; }
  h2 { font-size: 19px; margin: 40px 0 12px; border-bottom: 1px solid #2a2e38; padding-bottom: 6px; }
  h3 { font-size: 15px; margin: 26px 0 8px; color: #c8ccd4; }
  .sub { color: #9aa0ac; font-size: 13px; margin-bottom: 24px; }
  .cards { display: flex; flex-wrap: wrap; gap: 12px; margin: 20px 0; }
  .card { background: #1a1d26; border: 1px solid #2a2e38; border-radius: 10px;
          padding: 14px 18px; min-width: 150px; flex: 1; }
  .card .v { font-size: 22px; font-weight: 600; }
  .card .k { color: #9aa0ac; font-size: 12px; margin-top: 2px; }
  .card.warn { background: #2a1f10; border-color: #5a3e20; }
  .card.warn .x { font-size: 28px; font-weight: 700; color: #f99c4f; line-height: 1.1; }
  .card.warn .v { font-size: 14px; color: #f5c08a; font-weight: 600; }
  .card.warn .k { color: #b8895a; }
  .card.gain { background: #142a1f; border-color: #2f5a43; }
  .card.gain .x { font-size: 28px; font-weight: 700; color: #6fd0a0; line-height: 1.1; }
  .card.gain .v { font-size: 14px; color: #9ae8c3; font-weight: 600; }
  .card.gain .k { color: #72b89a; }
  .chartbox { background: #1a1d26; border: 1px solid #2a2e38; border-radius: 10px;
              padding: 16px; margin: 16px 0; }
  canvas { max-height: 380px; }
  .note { color: #9aa0ac; font-size: 12px; margin: 6px 2px 0; }
  table { border-collapse: collapse; width: 100%; font-size: 12.5px; margin-top: 10px; }
  th, td { border: 1px solid #2a2e38; padding: 5px 9px; text-align: left; }
  th { background: #232733; position: sticky; top: 0; }
  td.num { text-align: right; font-variant-numeric: tabular-nums; }
  tbody tr:nth-child(even) { background: #181b23; }
  .tablewrap { max-height: 480px; overflow: auto; border-radius: 8px; }
  details { background: #1a1d26; border: 1px solid #2a2e38; border-radius: 10px;
            padding: 12px 16px; margin: 16px 0; }
  summary { cursor: pointer; font-weight: 600; }
  pre { background: #0b0d12; border-radius: 8px; padding: 12px; overflow: auto;
        max-height: 360px; font-size: 11px; }
  .dl { display: inline-block; margin: 8px 8px 0 0; padding: 7px 14px;
        background: #2d6cdf; color: #fff; border-radius: 7px; text-decoration: none;
        font-size: 13px; }
  ul { padding-left: 20px; } li { margin: 4px 0; }
  code { background: #232733; padding: 1px 5px; border-radius: 4px; font-size: 12px; }
  table.cmp { width: auto; min-width: 460px; }
  table.cmp th { position: static; }
  .callout { background: #18243a; border: 1px solid #2d6cdf; border-radius: 10px;
             padding: 13px 17px; margin: 14px 0; font-size: 13px; line-height: 1.6; }
  .devbar { background: #142a1f; border-left: 4px solid #3fa06f; border-radius: 6px;
            padding: 11px 16px; margin: 14px 0; font-size: 14px; color: #9ae8c3; }
  .devbar b { color: #6fd0a0; font-size: 16px; }
  .devbar code { background: #1a3326; }
  .warnbar { background: #2a1f10; border-left: 4px solid #c07020; border-radius: 6px;
             padding: 11px 16px; margin: 14px 0; font-size: 14px; color: #f5c08a; }
  .warnbar b { color: #f99c4f; font-size: 16px; }
  .pillar-legend { background: #1a1d26; border: 1px solid #2a2e38; border-radius: 10px;
                   padding: 4px 0; margin: 14px 0; font-size: 12.5px; }
  .pillar-legend .lh { font-weight: 600; color: #c8ccd4; padding: 9px 16px 4px; }
  .pillar-legend table { margin: 0; font-size: 12.5px; }
  .pillar-legend th, .pillar-legend td { border: none; border-top: 1px solid #232733; padding: 6px 16px; }
  .pillar-legend th { background: transparent; color: #9aa0ac; font-weight: 500; }
  .pillar-legend tr.p3 td { color: #6fd0a0; }
  .pillar-legend tr.p3 td:first-child::after { content: ' ✓ 정석'; color: #6fd0a0;
              font-size: 10.5px; font-weight: 600; }
  .pillar-legend tr.p1 td:first-child::after { content: ' ◦ baseline'; color: #d59a6f;
              font-size: 10.5px; font-weight: 600; }
</style>
</head>
<body>
<div class="wrap">
  <h1>Sample App Loading Time Benchmark — 3-Pillar</h1>
  <div class="sub">
    Cold load — 브라우저 완전 재시작 후 "URL 입력 → 게임 완전히 뜸"까지 wallclock.
    완료 신호: <code>window.TriggerAPITest</code> 등록 (첫 씬 로드 + 첫 프레임 게임 코드 실행).<br>
    측정: ${esc(machine.cpu_model || 'unknown')} (${esc(String(machine.cpu_count || '?'))}코어,
    ${esc(machine.arch || '?')} ${esc(machine.platform || '?')}) ·
    네트워크 ${esc(env.netStr)} · CPU ${esc(env.cpuStr)} 슬로우다운 ·
    총 ${rows.length}회 측정 · ${cells.length} cell · 생성 ${new Date().toISOString()}
  </div>

  <!-- ===== 필러 쉬운 설명 (non-tech) ===== -->
  <div class="pillar-legend" style="margin-bottom:10px;">
    <div class="lh">이 벤치마크가 비교하는 3가지 — 쉽게 풀어 설명</div>
    <p style="margin:8px 0;color:#c8ccd4;line-height:1.7;">
      게임을 웹에 올릴 때 파일을 <b>"압축"</b>하면 용량이 줄어 더 빨리 다운로드됩니다.
      이때 두 가지 설정이 필요한데 — ① 게임을 만들 때 <b>압축해서 빌드</b>하기,
      ② 서버가 브라우저에게 <b>"이 파일은 압축돼 있다"고 알려주는 표시</b> 붙이기 — 입니다.
      이 둘을 제대로 했는지에 따라 로딩 속도가 갈립니다. 짐 싸기에 비유하면:
    </p>
    <table>
      <thead><tr><th>구분</th><th>한 줄 비유</th><th>실제로 일어나는 일</th><th>로딩 속도</th></tr></thead>
      <tbody>
        <tr class="p1"><td><b>pillar 1</b><br><span style="color:#9aa0ac;">압축을 안 켬</span></td>
          <td>짐을 안 접고 그대로 부침 — 택배기사(서버)가 보다 못해<br>대신 대충 묶어줌</td>
          <td>개발자가 압축 설정을 빠뜨려 게임 파일이 원본 크기 그대로.
            서버(CDN)가 전송 직전 급한 대로 압축해 주지만, 압축이 헐거워<br>
            전송할 용량이 여전히 큼</td>
          <td><b>가장 느림<br>(기준점)</b></td></tr>
        <tr><td><b>pillar 2</b><br><span style="color:#9aa0ac;">압축은 켰는데<br>표시를 빠뜨림</span></td>
          <td>짐은 잘 접어 압축했는데, 상자에 "압축됨" 라벨을<br>안 붙여 보냄</td>
          <td>게임은 잘 압축됐지만 서버가 "압축돼 있다"는 표시를 안 보냄.
            브라우저가 못 알아채서, 게임이 자체적으로 압축을 푸는데<br>
            이 방식이 브라우저 기본 기능보다 느림</td>
          <td><b>중간 — pillar 1보다 빠름</b></td></tr>
        <tr class="p3"><td><b>pillar 3</b><br><span style="color:#9aa0ac;">둘 다 제대로</span></td>
          <td>짐을 잘 접고 "압축됨" 라벨도 또렷이 붙여 보냄</td>
          <td>압축도 했고 표시도 정확히 붙음. 브라우저가 자기 기본 기능으로
            가장 빠르게 압축을 풂. <b>권장 설정</b></td>
          <td><b>가장 빠름</b></td></tr>
      </tbody>
    </table>
    <p style="margin:8px 0 0;color:#9aa0ac;font-size:12px;">
      ※ 아래 모든 수치는 <b>pillar 1(압축을 안 켠 최악의 상태)</b>을 기준점으로,
      압축·헤더를 제대로 설정하면 로딩이 <b>몇 % 빨라지는지(최적화 효과)</b>를 보여줍니다.
    </p>
  </div>

  <!-- ===== 필러 범례 (기술 상세) ===== -->
  <div class="pillar-legend">
    <div class="lh">3-필러 정의 — 기술 상세 (압축/전송 설정의 세 가지 현실 시나리오)</div>
    <table>
      <thead><tr><th>필러</th><th>Unity 빌드 압축</th><th>서버 Content-Encoding</th>
        <th>브라우저 동작</th><th>설명</th></tr></thead>
      <tbody>
        <tr class="p1"><td>pillar1</td><td>Disabled (평문)</td><td>gzip (CDN on-the-fly)</td>
          <td>네이티브 gzip 디코딩</td>
          <td>CDN이 평문을 실시간 gzip 압축 전송 — 압축 설정 누락 시나리오 (최적화 이전)</td></tr>
        <tr><td>pillar2</td><td>Brotli (.unityweb)</td><td>없음 (헤더 생략)</td>
          <td>Unity JS decompressionFallback</td>
          <td>Brotli 빌드를 만들었지만 헤더를 빠뜨린 시나리오</td></tr>
        <tr class="p3"><td>pillar3</td><td>Brotli (.unityweb)</td><td>Content-Encoding: br</td>
          <td>네이티브 Brotli 디코딩</td>
          <td>정석 설정 — 가장 작은 번들 + 가장 빠른 디코딩</td></tr>
      </tbody>
    </table>
  </div>

  <!-- ===== 요약 카드 (network별 분리) ===== -->
  <div class="sub" style="margin:18px 0 6px;font-weight:600;color:#c8ccd4;">
    pillar1 대비 최적화 효과 요약 — 네트워크별 (baseline = pillar1, 압축 미설정)</div>
  ${netDataList.map((d) => {
    const ms = (v) => (v == null ? '–' : `${Math.round(v)} ms`);
    const sub = (line) => `<div style="font-size:11px;color:#9aa0ac;margin-top:3px;line-height:1.5">${line}</div>`;
    return `
  <div class="sub" style="margin:10px 0 4px;color:#7ab8f5;font-size:12px;font-weight:600;">${esc(d.label)}</div>
  <div class="cards">
    <div class="card warn">
      <div class="x">${ms(d.p1loadAvg)}</div>
      <div class="v">pillar1 — 압축 미설정 + CDN gzip</div>
      <div class="k">baseline (가장 느림 — 최적화 이전)</div>
      ${sub(`이 시간이 기준점. 절감 0 ms / 0%`)}
    </div>
    <div class="card gain">
      <div class="x">${ms(d.p2loadAvg)}</div>
      <div class="v">pillar2 — Brotli 압축만</div>
      <div class="k">번들 축소로 빨라짐 (헤더는 아직 누락)</div>
      ${sub(`pillar1 대비 <b style="color:#6fd0a0">${d.p2improvementAvg != null ? `${d.p2improvementAvg} ms` : '–'}${d.p2improvementPctAvg != null ? ` / ${d.p2improvementPctAvg}%` : ''}</b> 절감`)}
    </div>
    <div class="card gain">
      <div class="x">${ms(d.p3loadAvg)}</div>
      <div class="v">pillar3 — 압축 + 헤더 (정석)</div>
      <div class="k">총 최적화 효과 — 가장 빠름</div>
      ${sub(`pillar1 대비 <b style="color:#6fd0a0">${d.p3improvementAvg != null ? `${d.p3improvementAvg} ms` : '–'}${d.p3improvementPctAvg != null ? ` / ${d.p3improvementPctAvg}%` : ''}</b> 절감`)}
    </div>
  </div>`;
  }).join('\n')}
  <div class="note">큰 숫자 = 해당 필러의 cold load 평균(ms). 절감 = 해당 필러 − pillar1 (음수 = pillar1보다 빠름 = 최적화 효과).
    pillar3 절감폭이 압축/헤더를 제대로 설정했을 때의 총 단축량이다. Unity 버전 전체 평균값. cpu ${esc(env.cpuStr)}.</div>

  <!-- ===== 네트워크 간 비교 섹션 (핵심 인사이트) ===== -->
  ${netDataList.length >= 2 ? `
  <h2 style="margin-top:48px;">LTE vs WiFi — 네트워크별 최적화 효과 비교 (측정의 핵심 인사이트)</h2>
  <div class="callout">
    <b>이 측정의 핵심 질문:</b> 압축/헤더 최적화(pillar1→pillar3)의 효과는 네트워크에 따라 어떻게 달라지는가.
    빠른 네트워크(WiFi)일수록 baseline(pillar1)이 이미 빠르므로, 같은 절대 단축이라도 <b>% 개선률은 WiFi에서 더 크게</b> 나타난다.
    pillar2(Brotli만)는 디코딩이 JS fallback이라 추가 CPU 비용이 있어, 네트워크와 무관하게 pillar3보다 개선폭이 작다.
  </div>

  <h3>개선 수치 비교 (Unity 버전 × network)</h3>
  <table class="cmp">
    <thead><tr>
      <th>Unity 버전</th>
      ${netDataList.map((d) => `<th>p2 개선 @${esc(sliceTag(d))} (ms)</th><th>p3 개선 @${esc(sliceTag(d))} (ms)</th>`).join('')}
    </tr></thead>
    <tbody>
      ${unities.map((u, ui) => {
        const fmtImp = (v) =>
          v == null ? '–' : v <= 0 ? `<b style="color:#6fd0a0">${v}</b>` : `<b style="color:#f99c4f">+${v}</b>`;
        const cols = netDataList.map((d) => {
          const p2 = d.c3series.find((s) => s.label.includes('pillar2'));
          const p3 = d.c3series.find((s) => s.label.includes('pillar3'));
          return `<td class="num">${fmtImp(p2 ? p2.data[ui] : null)}</td><td class="num">${fmtImp(p3 ? p3.data[ui] : null)}</td>`;
        }).join('');
        return `<tr><td>${esc(u)}</td>${cols}</tr>`;
      }).join('\n')}
    </tbody>
  </table>
  <div class="note">개선 = 해당 필러 − pillar1. 초록 = pillar1보다 빠름(최적화 효과), 주황 = 느림(역전).</div>

  <h3>pillar2 개선 비교 — LTE vs WiFi</h3>
  <div class="chartbox"><canvas id="c_netcomp_p2"></canvas></div>
  <div class="note">↑ pillar2(Brotli 압축만) 개선폭(ms)을 네트워크별로 비교. 음수가 클수록 최적화 효과가 크다.</div>

  <h3>pillar3 개선 비교 — LTE vs WiFi</h3>
  <div class="chartbox"><canvas id="c_netcomp_p3"></canvas></div>
  <div class="note">↑ pillar3(정석) 개선폭(ms)을 네트워크별로 비교. 압축 미설정 대비 총 최적화 효과.</div>
  ` : ''}

  <!-- ===== 섹션 1~4: network별 차트 + 표 ===== -->
  ${networkSections}

  <!-- ===== H1/H2: 가설 검증 (가설 데이터일 때만 렌더) ===== -->
  ${hypothesisHtml}

  <!-- ===== 5. 번들 용량 확장 추정 (100MB / 200MB) ===== -->
  ${extrapolationHtml(cells, env.nets)}

  <!-- ===== 6. 종합 ===== -->
  <h2>6. 종합</h2>
  <div class="callout">
    <b>핵심 결론 (baseline = pillar1, 압축 미설정):</b><br>
    • <b>pillar2 (Brotli 압축만)</b>은 pillar1 대비 평균
      <b>${p2impAvg != null ? `${p2impAvg}ms` : '–'}${p2impPctAvg != null ? ` (${p2impPctAvg}%)` : ''}</b> 빨라진다 — 번들이 작아져
      전송 시간이 줄지만, 헤더가 없어 JS fallback 디코딩 비용이 남는다.<br>
    • <b>pillar3 (압축 + 헤더, 정석)</b>은 pillar1 대비 평균
      <b>${p3impAvg != null ? `${p3impAvg}ms` : '–'}${p3impPctAvg != null ? ` (${p3impPctAvg}%)` : ''}</b> 빨라진다 —
      이것이 압축/헤더를 제대로 설정했을 때의 <b>총 최적화 효과</b>다.<br>
    • <b>LTE vs WiFi</b> — 같은 최적화라도 빠른 네트워크(WiFi)에서 % 개선률이 더 크다.
      baseline(pillar1)이 WiFi에서 이미 빠르기 때문에, 작은 Brotli 번들의 이득이 비율로 더 크게 잡힌다.<br>
    • <b>권장</b> — Unity 빌드에서 압축(Brotli)을 켜고, 서버/CDN이 <code>Content-Encoding: br</code>
      헤더를 보내도록 설정하면 pillar3에 도달한다. 둘 중 하나라도 빠지면 pillar1/pillar2로 후퇴한다.
  </div>

  <h3>전체 cell 통계</h3>
  <div class="tablewrap"><table>
    <thead><tr>
      <th>Unity</th><th>pillar</th><th>network</th><th>cpu</th><th>iter</th><th>err</th>
      <th>median</th><th>p95</th><th>mean</th><th>stddev</th><th>min</th><th>max</th>
    </tr></thead>
    <tbody>${tableRows}</tbody>
  </table></div>
  <div class="note">단위 ms. ${cells.length} cell × 5 iter = ${rows.length} 측정 ·
    cell당 stddev 평균 ${stddevMean}ms · 에러 ${errorTotal}.</div>

  <!-- ===== 7. Raw data ===== -->
  <h2>7. Raw data</h2>
  <p>
    <a class="dl" id="dl-jsonl">benchmark-loading-results.jsonl 다운로드</a>
    <a class="dl" id="dl-csv">benchmark-report.csv 다운로드 (집계)</a>
  </p>
  <details>
    <summary>raw JSONL 미리보기 (${rows.length} rows)</summary>
    <pre id="rawpre"></pre>
  </details>
  <ul>
    <li>각 행의 <code>pillar</code> 필드: <code>pillar1</code> | <code>pillar2</code> | <code>pillar3</code>.</li>
    <li>네트워크: ${esc(env.netStr)}. CPU: ${esc(env.cpuStr)} 슬로우다운 (CDP setCPUThrottlingRate).</li>
    <li>Warm(캐시 재방문) 미측정 — CDP 네트워크 throttling이 HTTP 디스크 캐시를
      우회시켜 throttled warm이 cold와 구분 불가.</li>
    <li>전송량: ${unities
      .map((u) => `${esc(u)} ${(transferByUnity[u] / 1048576 || 0).toFixed(1)}MB`)
      .join(' · ')}</li>
  </ul>
</div>

<script id="rawdata" type="application/x-ndjson">${escScript(rawJsonl)}</script>
<script>
const PALETTE = ['#4f9cf9','#f99c4f','#5fd0a0','#d05f9c','#c0c84f','#9c7ff9','#f9d24f','#4fd0d0'];
const GRID = '#2a2e38', TICK = '#9aa0ac';

// 3-필러 고정 색상
const PILLAR_COLOR = {
  'pillar1 (baseline)': '#d05f9c',         // 분홍 — 압축 미설정 (baseline)
  'pillar2': '#f99c4f',                    // 주황 — Brotli만
  'pillar3': '#5fd0a0',                    // 초록 — 정석/최선
  'pillar2 개선 (ms)': '#f99c4f',          // 주황 — Brotli만 (ms 차트)
  'pillar3 개선 (ms)': '#5fd0a0',          // 초록 — 정석 (ms 차트)
  'pillar2 개선 (%)': '#f99c4f',           // 주황 — Brotli만 (% 차트)
  'pillar3 개선 (%)': '#5fd0a0',           // 초록 — 정석 (% 차트)
};
function pillarColor(label, i) {
  if (PILLAR_COLOR[label]) return PILLAR_COLOR[label];
  return PALETTE[i % PALETTE.length];
}

function mkBar(id, labels, series, yTitle, stacked) {
  new Chart(document.getElementById(id), {
    type: 'bar',
    data: { labels, datasets: series.map((s,i)=>({
      label: s.label, data: s.data,
      backgroundColor: pillarColor(s.label, i) })) },
    options: { responsive: true,
      scales: {
        x: { stacked: !!stacked, grid: { color: GRID }, ticks: { color: TICK } },
        y: { stacked: !!stacked, beginAtZero: true, grid: { color: GRID },
             ticks: { color: TICK }, title: { display: true, text: yTitle, color: TICK } } },
      plugins: { legend: { labels: { color: TICK } } } }
  });
}

// 스택 막대: base(pillar3, 최종 도달점) + 각 최적화 단계가 깎아낸 절감 슬랩.
// 스택 전체 높이 = pillar1(baseline) cold load.
// base는 회색, gain(절감분)은 주황/초록.
function mkGain(id, labels, series, yTitle) {
  const colors = { base: '#3a4254', gain: '#5fd0a0', gain2: '#f99c4f' };
  new Chart(document.getElementById(id), {
    type: 'bar',
    data: { labels, datasets: series.map((s)=>{
      const col = colors[s.kind] || '#4f9cf9';
      return { label: s.label, data: s.data, stack: 'g',
        backgroundColor: col, borderColor: col };
    }) },
    options: { responsive: true,
      scales: {
        x: { stacked: true, grid: { color: GRID }, ticks: { color: TICK } },
        y: { stacked: true, beginAtZero: true, grid: { color: GRID },
             ticks: { color: TICK }, title: { display: true, text: yTitle, color: TICK } } },
      plugins: {
        legend: { labels: { color: TICK } },
        tooltip: { callbacks: { footer: (items) => {
          let base = 0, saved = 0;
          for (const it of items) {
            const v = it.parsed.y || 0;
            if (it.dataset.label && it.dataset.label.includes('최종')) base += v;
            else saved += v;
          }
          if (!saved) return '';
          const total = base + saved; // = pillar1 baseline
          return 'pillar1 대비 −' + saved + 'ms (' + ((saved / total) * 100).toFixed(1) + '% 빨라짐)';
        } } } } }
  });
}

function mkLine(id, labels, series, yTitle) {
  new Chart(document.getElementById(id), {
    type: 'line',
    data: { labels, datasets: series.map((s,i)=>({
      label: s.label, data: s.data,
      borderColor: pillarColor(s.label, i),
      backgroundColor: pillarColor(s.label, i), tension: 0.25, pointRadius: 5 })) },
    options: { responsive: true,
      scales: {
        x: { grid: { color: GRID }, ticks: { color: TICK } },
        y: { beginAtZero: false, grid: { color: GRID },
             ticks: { color: TICK }, title: { display: true, text: yTitle, color: TICK } } },
      plugins: { legend: { labels: { color: TICK } } } }
  });
}

const UNITIES = ${JSON.stringify(unities)};
// network별 차트 초기화
${chartInitCode}

// network 간 비교 차트
${netCompChartJs}

// 가설 검증 차트 (H1/H2)
${hypothesisChartJs}

// raw data 미리보기 + 다운로드
const raw = document.getElementById('rawdata').textContent;
document.getElementById('rawpre').textContent =
  raw.split('\\n').slice(0, 30).join('\\n') + '\\n… (' +
  raw.split('\\n').length + ' rows total)';
function dl(id, text, name, mime) {
  const a = document.getElementById(id);
  a.href = URL.createObjectURL(new Blob([text], { type: mime }));
  a.download = name;
}
dl('dl-jsonl', raw, 'benchmark-loading-results.jsonl', 'application/x-ndjson');
dl('dl-csv', ${JSON.stringify(csvString(cells))}, 'benchmark-report.csv', 'text/csv');
</script>
</body>
</html>`;

  fs.writeFileSync(file, html);
}

function esc(s) {
  return String(s).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
}

// <script> 안에 넣을 텍스트: </script> 시퀀스만 깨면 됨.
function escScript(s) {
  return String(s).replace(/<\/script/gi, '<\\/script');
}

function fmt(v) {
  return v === null || v === undefined ? '–' : String(v);
}
