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
//   pillar2 — 압축 O + Content-Encoding 누락: Brotli .unityweb를 헤더 없이 전송
//             → Unity 로더 JS decompressionFallback 디코딩.
//   pillar3 — 압축 O + Content-Encoding O: Brotli .unityweb를 Content-Encoding: br
//             전송 → 브라우저 네이티브 Brotli 디코딩. (정석 설정, baseline)
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
const CSV_PATH = path.join(OUT_DIR, 'benchmark-report.csv');
const MD_PATH = path.join(OUT_DIR, 'benchmark-report.md');
const HTML_PATH = path.join(OUT_DIR, 'benchmark-report.html');

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

  // network별 pivot 표
  for (const net of env.nets) {
    const netCells = cells.filter((c) => c.network === net);
    lines.push(`## Cold load median — ${networkLabel(net)} (cpu_rate=${env.cpus.join('/')}×)`);
    lines.push('');
    lines.push(pivotTable(netCells));
    lines.push('');
  }

  // network 간 overhead 비교 표
  if (env.nets.length >= 2) {
    lines.push('## Network 간 pillar overhead 비교');
    lines.push('');
    lines.push(networkCompareTable(cells, env.nets));
    lines.push('');
  }

  lines.push('## 필러 정의');
  lines.push('- **pillar1**: 압축 미설정 + CDN gzip — Unity 압축 Disabled, 평문 산출물을 서버가 on-the-fly gzip → 브라우저 네이티브 gzip 디코딩.');
  lines.push('- **pillar2**: 압축 O + Content-Encoding 누락 — Brotli .unityweb를 헤더 없이 전송 → Unity 로더 JS decompressionFallback 디코딩.');
  lines.push('- **pillar3**: 압축 O + Content-Encoding O — Brotli .unityweb를 Content-Encoding: br 전송 → 브라우저 네이티브 Brotli 디코딩. **(정석 설정, baseline)**');
  lines.push('');
  lines.push('## Notes');
  lines.push('- Each cell measures cold load: a fresh browser (all storage cleared), URL');
  lines.push('  entered → game fully loaded. The completion signal is `window.TriggerAPITest`');
  lines.push('  becoming a function, which Unity registers from `E2ETestTrigger.Awake()` —');
  lines.push('  i.e. bundle decode + engine boot + first scene load + first-frame game code.');
  lines.push(`- Networks: ${env.netStr}. CPU: ${env.cpuStr} slowdown.`);
  lines.push('- Baseline is pillar3 (native Content-Encoding). pillar1/pillar2 overhead');
  lines.push('  shows the cost of each non-ideal delivery configuration.');
  lines.push('- Network slowdown amplifies transfer-size differences: pillar1 overhead is');
  lines.push('  expected to be larger on LTE (slower link) than on WiFi.');
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
    pillar1: 'pillar1 (압축 미설정+CDN gzip)',
    pillar2: 'pillar2 (압축O+헤더X)',
    pillar3: 'pillar3 (압축O+헤더O, 정석)',
  };

  const header = ['Unity 버전', ...pillars.map((p) => pillarLabel[p] || p), 'p1 vs p3 (ms / %)', 'p2 vs p3 (ms / %)'];
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
    const base = vals['pillar3'];
    const p1diff = base != null && vals['pillar1'] != null ? vals['pillar1'] - base : null;
    const p2diff = base != null && vals['pillar2'] != null ? vals['pillar2'] - base : null;
    const fmtOverheadMd = (diff, base) => {
      if (diff == null) return '–';
      const ms = Math.round(diff);
      const pct = base != null && base !== 0 ? ((diff / base) * 100).toFixed(1) : null;
      return pct != null ? `+${ms} ms / +${pct}%` : `+${ms} ms`;
    };

    const row = [u];
    for (const p of pillars) {
      row.push(vals[p] != null ? formatMs(vals[p]) : '–');
    }
    row.push(fmtOverheadMd(p1diff, base));
    row.push(fmtOverheadMd(p2diff, base));
    lines.push('| ' + row.join(' | ') + ' |');
  }

  return lines.join('\n');
}

// network 간 pillar overhead 비교 표 (Markdown)
function networkCompareTable(cells, nets) {
  const unities = uniq(cells.map((c) => c.unity_version)).sort();
  const lines = [];

  // 헤더: Unity 버전 | p1 overhead @net1 | p1 overhead @net2 | p2 overhead @net1 | p2 overhead @net2
  const shortNet = (n) => n.replace('kr-', '').toUpperCase();
  const header = ['Unity 버전', ...nets.flatMap((n) => [`p1 overhead @${shortNet(n)}`, `p2 overhead @${shortNet(n)}`])];
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
      const base = getV('pillar3');
      const p1 = getV('pillar1');
      const p2 = getV('pillar2');
      const fmtOvNet = (val, b) => {
        if (b == null || val == null) return '–';
        const ms = Math.round(val - b);
        const pct = b !== 0 ? ((val - b) / b * 100).toFixed(1) : null;
        return pct != null ? `+${ms} ms / +${pct}%` : `+${ms} ms`;
      };
      row.push(fmtOvNet(p1, base));
      row.push(fmtOvNet(p2, base));
    }
    lines.push('| ' + row.join(' | ') + ' |');
  }

  lines.push('');
  lines.push('> 네트워크가 느릴수록(LTE) 전송량 차이가 크게 작용하므로 pillar1 overhead가 더 클 것으로 예상.');

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
// HTML 리포트 — 차트(Chart.js) + 표 + raw data 내장 단일 파일.
// ---------------------------------------------------------------------------

function writeHtml(rows, cells, file) {
  const head = rows[0];
  const machine = head.machine || {};
  const unities = uniq(cells.map((c) => c.unity_version)).sort();
  const env = envSummary(rows);

  // 3-필러 정의 (표시용 레이블)
  const PILLAR_LABELS = {
    pillar1: 'pillar1 — 압축 미설정+CDN gzip',
    pillar2: 'pillar2 — 압축O+헤더X (jsfallback)',
    pillar3: 'pillar3 — 압축O+헤더O (정석, baseline)',
  };
  const PILLAR_SHORT = {
    pillar1: 'pillar1',
    pillar2: 'pillar2',
    pillar3: 'pillar3 (baseline)',
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
  //   baseline = pillar3 (정석 — Content-Encoding: br + 네이티브 Brotli)
  //   pillar1 overhead = pillar1 − pillar3 (양수 = pillar3보다 느림)
  //   pillar2 overhead = pillar2 − pillar3 (양수 = pillar3보다 느림)
  // pillar3이 가장 빠르다고 가정. 만약 실측에서 다른 필러가 더 빠르면
  // overhead가 음수로 표시된다.
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

    // pillar1/2의 pillar3 대비 overhead (ms)
    const overheadData = (pData) =>
      unities.map((u, i) => {
        const base = p3data[i];
        const val = pData[i];
        if (base == null || val == null) return null;
        return Math.round(val - base);
      });
    const p1overhead = overheadData(p1data);
    const p2overhead = overheadData(p2data);

    // % overhead
    const overheadPct = (pData) =>
      unities.map((u, i) => {
        const base = p3data[i];
        const val = pData[i];
        if (base == null || val == null || base === 0) return null;
        return Number(((val - base) / base) * 100).toFixed(1);
      });
    const p1overheadPct = overheadPct(p1data);
    const p2overheadPct = overheadPct(p2data);

    const p1overheadAvg = avgOf(p1overhead);
    const p2overheadAvg = avgOf(p2overhead);
    const p1overheadPctAvg = avgPctOf(p1overheadPct);
    const p2overheadPctAvg = avgPctOf(p2overheadPct);

    // 차트 데이터셋
    const c1series = presentPillars.map((p) => ({
      label: PILLAR_SHORT[p] || p,
      data: unities.map((u) => { const c = cellMap(u, p); return c && c.cold ? c.cold.median : null; }),
    }));
    const c2series = [
      { label: 'pillar3 (baseline)', kind: 'base',
        data: unities.map((u, i) => (p3data[i] != null ? Math.round(p3data[i]) : null)) },
      ...(presentPillars.includes('pillar2')
        ? [{ label: 'pillar2 overhead vs pillar3', kind: 'gain', data: p2overhead }] : []),
      ...(presentPillars.includes('pillar1')
        ? [{ label: 'pillar1 overhead vs pillar3', kind: 'gain2', data: p1overhead }] : []),
    ];
    const c3series = [];
    if (presentPillars.includes('pillar1'))
      c3series.push({ label: 'pillar1 overhead (ms)', data: p1overhead });
    if (presentPillars.includes('pillar2'))
      c3series.push({ label: 'pillar2 overhead (ms)', data: p2overhead });
    const c4series = [];
    if (presentPillars.includes('pillar1'))
      c4series.push({ label: 'pillar1 overhead (%)', data: p1overheadPct.map((v) => (v != null ? Number(v) : null)) });
    if (presentPillars.includes('pillar2'))
      c4series.push({ label: 'pillar2 overhead (%)', data: p2overheadPct.map((v) => (v != null ? Number(v) : null)) });

    // Unity 버전별 비교 표 행
    const fmtOv = (v) =>
      v == null ? '–' : v > 0 ? `<b style="color:#f99c4f">+${v}</b>` : `<b style="color:#6fd0a0">${v}</b>`;
    const fmtPct = (v) =>
      v == null ? '–' : Number(v) > 0 ? `<b style="color:#f99c4f">+${v}%</b>` : `<b style="color:#6fd0a0">${v}%</b>`;
    const unityTableRows = unities.map((u, i) => {
      const p1 = p1data[i];
      const p2 = p2data[i];
      const p3 = p3data[i];
      const mb = transferByUnity[u] != null ? (transferByUnity[u] / 1048576).toFixed(1) : '–';
      return `<tr>
<td>${esc(u)}</td>
<td class="num">${mb}</td>
<td class="num">${p3 != null ? Math.round(p3) : '–'}</td>
<td class="num">${p2 != null ? Math.round(p2) : '–'}</td>
<td class="num">${p1 != null ? Math.round(p1) : '–'}</td>
<td class="num">${fmtOv(p2overhead[i])} <span style="color:#9aa0ac;font-size:11px">${fmtPct(p2overheadPct[i])}</span></td>
<td class="num">${fmtOv(p1overhead[i])} <span style="color:#9aa0ac;font-size:11px">${fmtPct(p1overheadPct[i])}</span></td>
</tr>`;
    }).join('\n');

    return { p1overheadAvg, p2overheadAvg, p1overheadPctAvg, p2overheadPctAvg,
             c1series, c2series, c3series, c4series, unityTableRows, idSuffix };
  }

  // network별 데이터 빌드
  const netDataList = env.nets.map((net, ni) => {
    const netCells = cells.filter((c) => c.network === net);
    return { net, label: networkLabel(net), ...buildNetworkData(netCells, `n${ni}`) };
  });

  // 전체 요약용 (모든 network 평균)
  const allP1overheadAvg = avgOf(netDataList.map((d) => d.p1overheadAvg).filter((v) => v != null));
  const allP2overheadAvg = avgOf(netDataList.map((d) => d.p2overheadAvg).filter((v) => v != null));
  const allP1overheadPctAvg = avgPctOf(netDataList.map((d) => d.p1overheadPctAvg).filter((v) => v != null));
  const allP2overheadPctAvg = avgPctOf(netDataList.map((d) => d.p2overheadPctAvg).filter((v) => v != null));

  // 요약 카드에 사용할 값 (단일 network면 그 값, 복수면 전체 평균)
  const p2overheadAvg = netDataList.length === 1 ? netDataList[0].p2overheadAvg : allP2overheadAvg;
  const p1overheadAvg = netDataList.length === 1 ? netDataList[0].p1overheadAvg : allP1overheadAvg;
  const p2overheadPctAvg = netDataList.length === 1 ? netDataList[0].p2overheadPctAvg : allP2overheadPctAvg;
  const p1overheadPctAvg = netDataList.length === 1 ? netDataList[0].p1overheadPctAvg : allP1overheadPctAvg;

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
    <b>pillar3(정석)</b>이 각 버전에서 최저점이어야 정상이다.</div>
  <div class="chartbox"><canvas id="c1_${sn}"></canvas></div>
  <div class="note">↑ Unity 버전별로 pillar1/2/3의 cold load median(ms)을 계열별로 비교.
    pillar3 = baseline(브라우저 네이티브 Brotli). pillar1 = CDN gzip.
    pillar2 = JS decompressionFallback.</div>

  <!-- ===== 2. pillar3 기준 오버헤드 [${sn}] ===== -->
  <h2>2. pillar3 대비 오버헤드 (필러별 추가 비용)${isMultiNet ? ` — ${esc(d.label)}` : ''}</h2>
  <div class="warnbar">
    <b>pillar2 오버헤드 평균 ${d.p2overheadAvg != null ? `+${d.p2overheadAvg} ms` : '–'}${d.p2overheadPctAvg != null ? ` (+${d.p2overheadPctAvg}%)` : ''}</b> — Brotli 번들을 Content-Encoding 헤더 없이 전송하면
    브라우저가 Unity JS fallback에 Brotli 디코딩을 맡겨 이 비용이 발생한다.<br>
    <b>pillar1 오버헤드 평균 ${d.p1overheadAvg != null ? `+${d.p1overheadAvg} ms` : '–'}${d.p1overheadPctAvg != null ? ` (+${d.p1overheadPctAvg}%)` : ''}</b> — 압축 미설정(평문 빌드)은 번들이 커서 전송 시간 자체가 늘어난다.
  </div>
  <h3>오버헤드 — ms</h3>
  <div class="chartbox"><canvas id="c3_${sn}"></canvas></div>
  <div class="note">↑ pillar3 대비 추가 소요 시간(ms). 양수 = pillar3보다 느림.
    막대가 없으면 해당 버전 데이터 없음.</div>

  <h3>오버헤드 — % (pillar3 대비)</h3>
  <div class="chartbox"><canvas id="c4_${sn}"></canvas></div>
  <div class="note">↑ pillar3 대비 추가 소요 시간(%). 계산식: (pillar_N − pillar3) / pillar3 × 100.
    양수 = pillar3보다 느림. 막대가 없으면 해당 버전 데이터 없음.</div>

  <!-- ===== 3. 스택 막대: pillar3 + 오버헤드 [${sn}] ===== -->
  <h2>3. cold load 구성 — pillar3 baseline + 오버헤드${isMultiNet ? ` — ${esc(d.label)}` : ''}</h2>
  <div class="chartbox"><canvas id="c2_${sn}"></canvas></div>
  <div class="note">↑ 회색/파랑 = pillar3 cold load(baseline), 주황/붉은 슬랩 = 각 필러의 추가 비용.
    스택 전체 높이 = 해당 필러의 총 cold load.</div>

  <!-- ===== 4. Unity 버전별 상세 비교 표 [${sn}] ===== -->
  <h2>4. Unity 버전별 필러 비교${isMultiNet ? ` — ${esc(d.label)}` : ` (${esc(env.netStr)} · cpu ${esc(env.cpuStr)})`}</h2>
  <table class="cmp">
    <thead><tr>
      <th>Unity 버전</th><th>번들 크기 (MB)</th>
      <th>pillar3 baseline (ms)</th>
      <th>pillar2 (ms)</th>
      <th>pillar1 (ms)</th>
      <th>pillar2 overhead (ms / %)</th>
      <th>pillar1 overhead (ms / %)</th>
    </tr></thead>
    <tbody>${d.unityTableRows}</tbody>
  </table>
  <div class="note">오버헤드 = 해당 필러 − pillar3. 주황 = pillar3보다 느림, 초록 = 빠름(역전).
    번들 크기는 pillar별 첫 측정 전송량 기준.</div>`;
  }).join('\n\n');

  // JS 차트 초기화 코드 (network별)
  const chartInitCode = netDataList.map((d) => {
    const sn = d.idSuffix;
    return `// [${sn}] ${d.label}
mkLine('c1_${sn}', UNITIES, ${JSON.stringify(d.c1series)}, 'cold load median (ms)');
mkGain('c2_${sn}', UNITIES, ${JSON.stringify(d.c2series)}, 'cold load (ms) — pillar3 + overhead');
mkBar('c3_${sn}', UNITIES, ${JSON.stringify(d.c3series)}, 'overhead vs pillar3 (ms)');
mkBar('c4_${sn}', UNITIES, ${JSON.stringify(d.c4series)}, 'overhead vs pillar3 (%)');`;
  }).join('\n');

  // network 간 비교 차트 데이터 (pillar1/2 overhead: LTE vs WiFi)
  // 각 계열 = (network, pillar) 조합
  const NET_COMP_COLORS = ['#4f9cf9', '#5fd0a0', '#f99c4f', '#d05f9c'];
  const netCompP1series = netDataList.map((d, i) => ({
    label: `pillar1 overhead @${d.net.replace('kr-', '').toUpperCase()} (ms)`,
    data: d.c3series.find((s) => s.label.includes('pillar1'))?.data ?? unities.map(() => null),
    _colorIdx: i,
  }));
  const netCompP2series = netDataList.map((d, i) => ({
    label: `pillar2 overhead @${d.net.replace('kr-', '').toUpperCase()} (ms)`,
    data: d.c3series.find((s) => s.label.includes('pillar2'))?.data ?? unities.map(() => null),
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
      backgroundColor: NET_COMP_COLORS[s._colorIdx !== undefined ? s._colorIdx : i] })) },
    options: { responsive: true,
      scales: {
        x: { grid: { color: GRID }, ticks: { color: TICK } },
        y: { beginAtZero: true, grid: { color: GRID },
             ticks: { color: TICK }, title: { display: true, text: yTitle, color: TICK } } },
      plugins: { legend: { labels: { color: TICK } } } }
  });
}
mkBarNetComp('c_netcomp_p1', UNITIES, ${JSON.stringify(netCompP1series)}, 'pillar1 overhead vs pillar3 (ms)');
mkBarNetComp('c_netcomp_p2', UNITIES, ${JSON.stringify(netCompP2series)}, 'pillar2 overhead vs pillar3 (ms)');`
    : '// 단일 network — 비교 차트 생략';

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
  .pillar-legend tr.p3 td:first-child::after { content: ' ✓ baseline'; color: #6fd0a0;
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
        <tr><td><b>pillar 1</b><br><span style="color:#9aa0ac;">압축을 안 켬</span></td>
          <td>짐을 안 접고 그대로 부침 — 택배기사(서버)가 보다 못해<br>대신 대충 묶어줌</td>
          <td>개발자가 압축 설정을 빠뜨려 게임 파일이 원본 크기 그대로.
            서버(CDN)가 전송 직전 급한 대로 압축해 주지만, 압축이 헐거워<br>
            전송할 용량이 여전히 큼</td>
          <td><b>가장 느림</b></td></tr>
        <tr><td><b>pillar 2</b><br><span style="color:#9aa0ac;">압축은 켰는데<br>표시를 빠뜨림</span></td>
          <td>짐은 잘 접어 압축했는데, 상자에 "압축됨" 라벨을<br>안 붙여 보냄</td>
          <td>게임은 잘 압축됐지만 서버가 "압축돼 있다"는 표시를 안 보냄.
            브라우저가 못 알아채서, 게임이 자체적으로 압축을 푸는데<br>
            이 방식이 브라우저 기본 기능보다 느림</td>
          <td><b>중간</b></td></tr>
        <tr class="p3"><td><b>pillar 3</b><br><span style="color:#9aa0ac;">둘 다 제대로</span></td>
          <td>짐을 잘 접고 "압축됨" 라벨도 또렷이 붙여 보냄</td>
          <td>압축도 했고 표시도 정확히 붙음. 브라우저가 자기 기본 기능으로
            가장 빠르게 압축을 풂. <b>권장 설정</b></td>
          <td><b>가장 빠름<br>(기준점)</b></td></tr>
      </tbody>
    </table>
    <p style="margin:8px 0 0;color:#9aa0ac;font-size:12px;">
      ※ 아래 모든 수치는 pillar 3(권장 설정)을 기준점으로, 나머지가 얼마나 더 느린지를 보여줍니다.
    </p>
  </div>

  <!-- ===== 필러 범례 (기술 상세) ===== -->
  <div class="pillar-legend">
    <div class="lh">3-필러 정의 — 기술 상세 (압축/전송 설정의 세 가지 현실 시나리오)</div>
    <table>
      <thead><tr><th>필러</th><th>Unity 빌드 압축</th><th>서버 Content-Encoding</th>
        <th>브라우저 동작</th><th>설명</th></tr></thead>
      <tbody>
        <tr><td>pillar1</td><td>Disabled (평문)</td><td>gzip (CDN on-the-fly)</td>
          <td>네이티브 gzip 디코딩</td>
          <td>CDN이 평문을 실시간 gzip 압축 전송 — 압축 설정 누락 시나리오</td></tr>
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
    pillar3 대비 오버헤드 요약 — 네트워크별 (baseline = pillar3, 정석 설정)</div>
  ${netDataList.map((d) => `
  <div class="sub" style="margin:10px 0 4px;color:#7ab8f5;font-size:12px;font-weight:600;">${esc(d.label)}</div>
  <div class="cards">
    <div class="card gain">
      <div class="x">pillar3</div>
      <div class="v">정석 — Brotli + Content-Encoding: br</div>
      <div class="k">baseline (가장 빠름)</div>
    </div>
    <div class="card warn">
      <div class="x">${d.p2overheadAvg != null ? `+${d.p2overheadAvg} ms` : '–'}${d.p2overheadPctAvg != null ? ` <span style="font-size:18px;font-weight:500">(+${d.p2overheadPctAvg}%)</span>` : ''}</div>
      <div class="v">pillar2 평균 오버헤드</div>
      <div class="k">헤더 생략 → JS fallback 디코딩 비용</div>
    </div>
    <div class="card warn">
      <div class="x">${d.p1overheadAvg != null ? `+${d.p1overheadAvg} ms` : '–'}${d.p1overheadPctAvg != null ? ` <span style="font-size:18px;font-weight:500">(+${d.p1overheadPctAvg}%)</span>` : ''}</div>
      <div class="v">pillar1 평균 오버헤드</div>
      <div class="k">압축 미설정 + CDN gzip 비용</div>
    </div>
  </div>`).join('\n')}
  <div class="note">오버헤드 = 해당 필러 cold load − pillar3 cold load (양수 = pillar3보다 느림).
    Unity 버전 전체 평균값. cpu ${esc(env.cpuStr)}.</div>

  <!-- ===== 네트워크 간 비교 섹션 (핵심 인사이트) ===== -->
  ${netDataList.length >= 2 ? `
  <h2 style="margin-top:48px;">LTE vs WiFi — 네트워크 간 overhead 비교 (측정의 핵심 인사이트)</h2>
  <div class="callout">
    <b>이 측정의 핵심 질문:</b> 네트워크가 느릴수록(LTE) 전송량 차이가 크게 작용한다.
    <b>pillar1</b>(압축 미설정 → 번들이 더 큼)은 LTE에서 WiFi보다 overhead가 더 클 것으로 예상.
    <b>pillar2</b>(CPU 디코딩 비용 중심)는 네트워크 속도 영향이 상대적으로 작을 것으로 예상.
  </div>

  <h3>overhead 수치 비교 (Unity 버전 × network)</h3>
  <table class="cmp">
    <thead><tr>
      <th>Unity 버전</th>
      ${netDataList.map((d) => `<th>p1 overhead @${esc(d.net.replace('kr-','').toUpperCase())} (ms)</th><th>p2 overhead @${esc(d.net.replace('kr-','').toUpperCase())} (ms)</th>`).join('')}
    </tr></thead>
    <tbody>
      ${unities.map((u, ui) => {
        const fmtOv = (v) =>
          v == null ? '–' : v > 0 ? `<b style="color:#f99c4f">+${v}</b>` : `<b style="color:#6fd0a0">${v}</b>`;
        const cols = netDataList.map((d) => {
          // c3series의 데이터는 overhead이므로 buildNetworkData에서 직접 꺼내야 함
          // p1overhead/p2overhead는 d.c3series를 통해 접근
          const p1ov = d.c3series.find((s) => s.label.includes('pillar1'));
          const p2ov = d.c3series.find((s) => s.label.includes('pillar2'));
          return `<td class="num">${fmtOv(p1ov ? p1ov.data[ui] : null)}</td><td class="num">${fmtOv(p2ov ? p2ov.data[ui] : null)}</td>`;
        }).join('');
        return `<tr><td>${esc(u)}</td>${cols}</tr>`;
      }).join('\n')}
    </tbody>
  </table>
  <div class="note">오버헤드 = 해당 필러 − pillar3. 주황 = pillar3보다 느림, 초록 = 빠름(역전).
    네트워크가 느릴수록(LTE) pillar1 overhead가 더 클 것으로 예상.</div>

  <h3>pillar1 overhead 비교 — LTE vs WiFi</h3>
  <div class="chartbox"><canvas id="c_netcomp_p1"></canvas></div>
  <div class="note">↑ pillar1 overhead(ms)를 네트워크별로 비교. 느린 LTE에서 overhead가 더 클 것으로 예상.</div>

  <h3>pillar2 overhead 비교 — LTE vs WiFi</h3>
  <div class="chartbox"><canvas id="c_netcomp_p2"></canvas></div>
  <div class="note">↑ pillar2 overhead(ms)를 네트워크별로 비교. CPU 디코딩 비용 중심이므로 네트워크 차이가 적을 것으로 예상.</div>
  ` : ''}

  <!-- ===== 섹션 1~4: network별 차트 + 표 ===== -->
  ${networkSections}

  <!-- ===== 5. 종합 ===== -->
  <h2>5. 종합</h2>
  <div class="callout">
    <b>핵심 결론:</b><br>
    • <b>pillar3(정석)</b>이 가장 빠르다 — Brotli 번들 + <code>Content-Encoding: br</code>로
      브라우저 네이티브 디코딩.<br>
    • <b>pillar2</b>는 Brotli 빌드를 만들고도 헤더를 빠뜨린 경우 — Unity JS fallback이
      Brotli를 디코딩하며 불필요한 CPU 비용이 추가된다.<br>
    • <b>pillar1</b>은 압축을 전혀 설정하지 않은 경우 — CDN gzip이 보정해주지만
      Brotli 대비 번들이 크고 전송 시간이 길다.<br>
    • <b>LTE vs WiFi</b> — 느린 네트워크(LTE)에서는 번들 크기 차이(pillar1 ↔ pillar3)의 영향이 커져
      pillar1 overhead가 WiFi보다 더 크게 나타날 것으로 예상한다.
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

  <!-- ===== 6. Raw data ===== -->
  <h2>6. Raw data</h2>
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
  'pillar3 (baseline)': '#5fd0a0',         // 초록 — 정석/최선
  'pillar2': '#f99c4f',                    // 주황 — 헤더 누락 오버헤드
  'pillar1': '#d05f9c',                    // 분홍 — 압축 미설정
  'pillar1 overhead (ms)': '#d05f9c',      // 분홍 — 압축 미설정 (ms 차트)
  'pillar2 overhead (ms)': '#f99c4f',      // 주황 — 헤더 누락 (ms 차트)
  'pillar1 overhead (%)': '#d05f9c',       // 분홍 — 압축 미설정 (% 차트)
  'pillar2 overhead (%)': '#f99c4f',       // 주황 — 헤더 누락 (% 차트)
  'pillar1 overhead vs pillar3': '#d05f9c',
  'pillar2 overhead vs pillar3': '#f99c4f',
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

// 스택 막대: base(pillar3) + 필러별 오버헤드 슬랩.
// base는 회색, gain(오버헤드)은 주황/분홍.
function mkGain(id, labels, series, yTitle) {
  const colors = { base: '#3a4254', gain: '#f99c4f', gain2: '#d05f9c' };
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
          let base = 0, overhead = 0;
          for (const it of items) {
            const v = it.parsed.y || 0;
            if (it.dataset.label && it.dataset.label.includes('baseline')) base += v;
            else overhead += v;
          }
          if (!overhead) return '';
          const total = base + overhead;
          return 'pillar3 대비 +' + overhead + 'ms (' + ((overhead / base) * 100).toFixed(1) + '% 느림)';
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
