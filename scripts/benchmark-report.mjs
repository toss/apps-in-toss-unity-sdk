// benchmark-loading-results.jsonl → median/p95/stddev/min/max 통계 집계.
// 출력:
//   benchmark-report.csv          — 전체 row (cell + statistic)
//   benchmark-report.md           — Unity 버전별 / 네트워크별 pivot
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

const args = parseArgs(process.argv.slice(2));
const INPUT = path.resolve(args.input || path.join(ROOT, 'Tests~/E2E/tests/benchmark-loading-results.jsonl'));
const OUT_DIR = path.resolve(args['out-dir'] || ROOT);
const CSV_PATH = path.join(OUT_DIR, 'benchmark-report.csv');
const MD_PATH = path.join(OUT_DIR, 'benchmark-report.md');

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

console.log(`[report] CSV: ${CSV_PATH}`);
console.log(`[report] MD : ${MD_PATH}`);

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

function groupKey(r) {
  return [r.unity_version, r.compression, r.network, r.cpu_rate].join('|');
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
  const warm = rows.map((r) => r.warm_load_ms).filter((x) => Number.isFinite(x));
  const errors = rows.filter((r) => r.error).length;
  return {
    unity_version: head.unity_version,
    compression: head.compression,
    network: head.network,
    cpu_rate: head.cpu_rate,
    webgl_data_caching: head.webgl_data_caching,
    iterations: rows.length,
    errors,
    cold: stats(cold),
    warm: stats(warm),
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

function writeCsv(cells, file) {
  const headers = [
    'unity_version',
    'compression',
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
    'warm_median_ms',
    'warm_p95_ms',
    'warm_mean_ms',
    'warm_stddev_ms',
    'warm_min_ms',
    'warm_max_ms',
  ];
  const lines = [headers.join(',')];
  for (const c of cells) {
    lines.push(
      [
        c.unity_version,
        c.compression,
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
        c.warm.median,
        c.warm.p95,
        c.warm.mean,
        c.warm.stddev,
        c.warm.min,
        c.warm.max,
      ]
        .map(csvCell)
        .join(','),
    );
  }
  fs.writeFileSync(file, lines.join('\n') + '\n');
}

function csvCell(v) {
  if (v === null || v === undefined) return '';
  const s = String(v);
  if (s.includes(',') || s.includes('"')) return `"${s.replace(/"/g, '""')}"`;
  return s;
}

function writeMarkdown(rows, cells, file) {
  const head = rows[0];
  const machine = head.machine || {};

  const lines = [];
  lines.push('# Sample App Loading Time Benchmark');
  lines.push('');
  lines.push(`- Total measurements: ${rows.length}`);
  lines.push(`- Distinct cells: ${cells.length}`);
  lines.push(
    `- Machine: ${machine.cpu_model || 'unknown'} (${machine.cpu_count || '?'} cores, ${machine.arch || '?'} ${machine.platform || '?'})`,
  );
  lines.push(`- Errors: ${cells.reduce((s, c) => s + c.errors, 0)}`);
  lines.push('');
  lines.push('## Cold load median by (Unity × compression × network), cpu rate=1×');
  lines.push(pivotTable(cells, { cpu_rate: 1 }, 'cold', 'median'));
  lines.push('');
  lines.push('## Cold load median by (Unity × compression × network), cpu rate=6×');
  lines.push(pivotTable(cells, { cpu_rate: 6 }, 'cold', 'median'));
  lines.push('');
  lines.push('## Warm load median by (Unity × compression × network), cpu rate=1×');
  lines.push(pivotTable(cells, { cpu_rate: 1 }, 'warm', 'median'));
  lines.push('');
  lines.push('## Notes');
  lines.push('- `webglDataCaching` is false for Unity 2021.3/2022.3 and true for 6000.x (sample project ProjectSettings).');
  lines.push('  → Warm performance interpretations differ between version groups: 2021/2022 reuses HTTP cache only,');
  lines.push('    6000.x additionally reuses IndexedDB UnityCache (skips decompression).');
  lines.push('- See `benchmark-report.csv` for the full per-cell statistics.');
  lines.push('');

  fs.writeFileSync(file, lines.join('\n'));
}

function pivotTable(cells, filter, kind, stat) {
  const filtered = cells.filter((c) => {
    for (const k of Object.keys(filter)) {
      if (c[k] !== filter[k]) return false;
    }
    return true;
  });
  if (filtered.length === 0) return '_no data_';

  const unities = uniq(filtered.map((c) => c.unity_version)).sort();
  const networks = uniq(filtered.map((c) => c.network));
  const compressions = uniq(filtered.map((c) => c.compression));

  const header = ['Unity \\\\ network'];
  for (const net of networks) {
    for (const cmp of compressions) {
      header.push(`${net} / ${cmp}`);
    }
  }

  const lines = [];
  lines.push('| ' + header.join(' | ') + ' |');
  lines.push('| ' + header.map(() => '---').join(' | ') + ' |');

  for (const u of unities) {
    const cells_ = [u];
    for (const net of networks) {
      for (const cmp of compressions) {
        const c = filtered.find(
          (x) => x.unity_version === u && x.network === net && x.compression === cmp,
        );
        cells_.push(c && c[kind] ? formatMs(c[kind][stat]) : '–');
      }
    }
    lines.push('| ' + cells_.join(' | ') + ' |');
  }

  return lines.join('\n');
}

function formatMs(v) {
  if (v === null || v === undefined) return '–';
  return `${v.toFixed ? v.toFixed(0) : v} ms`;
}

function uniq(arr) {
  return Array.from(new Set(arr));
}
