#!/usr/bin/env node
// LuckyHero 빌드 산출물의 로드타임 프록시 지표를 수집한다.
//   - 게임 코드/바이너리 자체는 절대 출력하지 않는다 (사이즈 메타데이터만) — 프라이버시.
//   - 헤드라인 = 브라우저가 실제 내려받는 on-wire 바이트(압축본 .unityweb/.br/.gz)의 합.
//   - 보조 = wasm/data/framework 압축 전 크기(엔진/코드 strip 효과 가시화).
//
// 사용: node measure.mjs <gameProjectRoot> <outJsonPath> [label]
import { promises as fs } from 'node:fs';
import path from 'node:path';

const root = process.argv[2];
const outPath = process.argv[3] || path.join(root, 'luckyhero-metrics.json');
const label = process.argv[4] || 'unlabeled';

const SCAN_DIRS = [
  'webgl/Build',          // Unity 원시 WebGL 빌드
  'ait-build/dist',       // granite/vite 패키징 최종 산출물 (배포 대상)
  'webgl',                // 폴백 (레이아웃이 다른 경우)
];

const COMPRESSED_EXT = ['.unityweb', '.br', '.gz'];

function isCompressed(name) {
  return COMPRESSED_EXT.some((e) => name.endsWith(e));
}

function classify(name) {
  const n = name.toLowerCase();
  if (n.includes('.wasm')) return 'wasm';
  if (n.includes('.data')) return 'data';
  if (n.includes('.framework.js')) return 'framework';
  if (n.includes('.loader.js')) return 'loader';
  if (n.endsWith('.js') || n.includes('.js.')) return 'js';
  if (n.endsWith('.symbols.json')) return 'symbols';
  if (n.endsWith('.html') || n.endsWith('.css')) return 'page';
  return 'other';
}

async function walk(dir, acc) {
  let entries;
  try {
    entries = await fs.readdir(dir, { withFileTypes: true });
  } catch {
    return;
  }
  for (const e of entries) {
    const full = path.join(dir, e.name);
    if (e.isDirectory()) {
      await walk(full, acc);
    } else if (e.isFile()) {
      const st = await fs.stat(full);
      acc.push({ path: path.relative(root, full), size: st.size, name: e.name });
    }
  }
}

function mb(bytes) {
  return (bytes / (1024 * 1024)).toFixed(2);
}

const seen = new Set();
const files = [];
for (const rel of SCAN_DIRS) {
  const abs = path.join(root, rel);
  const before = files.length;
  await walk(abs, files);
  // 중복 디렉토리(webgl vs webgl/Build) dedup
  for (let i = before; i < files.length; i++) {
    if (seen.has(files[i].path)) {
      files[i]._dup = true;
    } else {
      seen.add(files[i].path);
    }
  }
}
const uniqueFiles = files.filter((f) => !f._dup);

const buckets = {};
let onWireTotal = 0;
let rawTotal = 0;
for (const f of uniqueFiles) {
  const kind = classify(f.name);
  const compressed = isCompressed(f.name);
  buckets[kind] ??= { rawBytes: 0, wireBytes: 0, files: [] };
  buckets[kind].files.push({ path: f.path, size: f.size, compressed });
  if (compressed) {
    buckets[kind].wireBytes += f.size;
    onWireTotal += f.size;
  } else {
    buckets[kind].rawBytes += f.size;
  }
  rawTotal += f.size;
}

// dist(배포 산출물)만의 on-wire 합 — 가장 의미있는 헤드라인
const distFiles = uniqueFiles.filter((f) => f.path.includes('ait-build/dist'));
const distWire = distFiles.reduce((s, f) => s + f.size, 0);
const distCompressed = distFiles.filter((f) => isCompressed(f.name)).reduce((s, f) => s + f.size, 0);

const summary = {
  label,
  generatedFrom: root,
  headline: {
    distTotalBytes: distWire,
    distTotalMB: mb(distWire),
    distCompressedBytes: distCompressed,
    distCompressedMB: mb(distCompressed),
    onWireCompressedTotalBytes: onWireTotal,
    onWireCompressedTotalMB: mb(onWireTotal),
  },
  buckets: Object.fromEntries(
    Object.entries(buckets).map(([k, v]) => [
      k,
      {
        rawMB: mb(v.rawBytes),
        wireMB: mb(v.wireBytes),
        fileCount: v.files.length,
      },
    ])
  ),
  files: uniqueFiles
    .map((f) => ({ path: f.path, sizeBytes: f.size, sizeMB: mb(f.size), kind: classify(f.name), compressed: isCompressed(f.name) }))
    .sort((a, b) => b.sizeBytes - a.sizeBytes),
};

await fs.writeFile(outPath, JSON.stringify(summary, null, 2));

// 사람이 읽는 요약 (사이즈 메타데이터만)
console.log('================ LuckyHero Load Metrics ================');
console.log(`label: ${label}`);
console.log('');
console.log(`On-wire 배포 산출물(ait-build/dist) 총합 : ${summary.headline.distTotalMB} MB`);
console.log(`  └ 압축본(.unityweb/.br/.gz)            : ${summary.headline.distCompressedMB} MB`);
console.log(`On-wire 압축본 전체(빌드+dist)           : ${summary.headline.onWireCompressedTotalMB} MB`);
console.log('');
console.log('버킷별 (raw=압축전, wire=압축본):');
for (const [k, v] of Object.entries(summary.buckets)) {
  console.log(`  ${k.padEnd(10)} raw=${String(v.rawMB).padStart(8)}MB  wire=${String(v.wireMB).padStart(8)}MB  files=${v.fileCount}`);
}
console.log('');
console.log('상위 12개 파일:');
for (const f of summary.files.slice(0, 12)) {
  console.log(`  ${String(f.sizeMB).padStart(8)}MB  ${f.compressed ? '[wire]' : '[raw ]'}  ${f.path}`);
}
console.log('=======================================================');
console.log(`(metrics JSON → ${outPath})`);
