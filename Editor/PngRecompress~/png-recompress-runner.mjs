// -----------------------------------------------------------------------
// png-recompress-runner.mjs - 스트림 텍스처 PNG 무손실 재압축 러너 (oxipng WASM)
//
// AITTextureStreamRecompressor(C#)가 SDK 내장 Node.js 로 실행하는 단일 파일 러너.
// stdin:  {"level":2,"files":[{"idx":0,"src":"/abs/in.png","dst":"/abs/out.png"}]}
// stdout: {"results":[{"idx":0,"raw":N,"out":N,"error":""}]}
//
// 무손실: oxipng 는 픽셀 데이터를 바꾸지 않고 필터/deflate 만 재탐색한다 —
//   Texture2D.LoadImage 디코드 결과가 바이트 단위로 동일하다.
// 병렬: WASM 싱글스레드 인코더라 worker_threads 로 파일 단위 분산(코어 절반, 최대 4).
//   레벨 2 기준 대형(4MB) 파일 ~3.7s — 레벨 3+ 은 이득 ~2%p 에 시간 2배라 미채택.
// 실패 정책: 파일 단위 격리 — 개별 실패는 error 로만 보고, 채택 판단은 호출부(C#) 몫.
// -----------------------------------------------------------------------
'use strict';

import { isMainThread, parentPort, workerData, Worker } from 'node:worker_threads';
import { readFileSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { availableParallelism } from 'node:os';

async function makeOptimiser() {
  // @jsquash/oxipng 는 브라우저 지향이라 wasm 을 fetch(file://)로 로드하다 Node 에서
  // 실패한다 — wasm 바이트를 직접 컴파일해 init 에 주입하는 것이 Node 경로.
  const { default: optimise, init } = await import('@jsquash/oxipng/optimise.js');
  const require = createRequire(import.meta.url);
  const wasmPath = require.resolve('@jsquash/oxipng/codec/pkg/squoosh_oxipng_bg.wasm');
  await init(await WebAssembly.compile(readFileSync(wasmPath)));
  return optimise;
}

async function recompressOne(optimise, file, level) {
  const res = { idx: file.idx, raw: 0, out: 0, error: '' };
  try {
    const buf = readFileSync(file.src);
    res.raw = buf.length;
    const outBuf = Buffer.from(await optimise(buf, { level }));
    if (outBuf.length <= 0) {
      res.error = 'empty optimiser output';
      return res;
    }

    writeFileSync(file.dst, outBuf);
    res.out = outBuf.length;
    return res;
  } catch (e) {
    res.error = String((e && e.message) || e).slice(0, 300);
    return res;
  }
}

if (!isMainThread) {
  // 워커: 배정된 파일들을 순차 처리해 결과 배열로 응답.
  (async () => {
    const { files, level } = workerData;
    const optimise = await makeOptimiser();
    const results = [];
    for (const f of files) {
      results.push(await recompressOne(optimise, f, level));
    }
    parentPort.postMessage(results);
  })();
} else {
  let raw = '';
  process.stdin.on('data', (d) => { raw += d; });
  process.stdin.on('end', async () => {
    let req;
    try {
      req = JSON.parse(raw);
    } catch {
      process.stdout.write('{"results":[]}');
      process.exit(2);
      return;
    }

    const level = (req.level | 0) || 2;
    const files = req.files || [];
    const workers = Math.max(1, Math.min(4, (availableParallelism() / 2) | 0, files.length));

    // 라운드로빈 분배(파일 크기 편차 완화) 후 워커별 순차 처리.
    const chunks = Array.from({ length: workers }, () => []);
    files.forEach((f, i) => chunks[i % workers].push(f));

    const settled = await Promise.all(chunks.map((chunk) => new Promise((resolve) => {
      if (chunk.length === 0) { resolve([]); return; }
      const w = new Worker(new URL(import.meta.url), { workerData: { files: chunk, level } });
      w.once('message', (r) => { resolve(r); w.terminate(); });
      w.once('error', (e) => {
        resolve(chunk.map((f) => ({ idx: f.idx, raw: 0, out: 0, error: String((e && e.message) || e).slice(0, 300) })));
      });
    })));

    process.stdout.write(JSON.stringify({ results: settled.flat() }));
  });
}
