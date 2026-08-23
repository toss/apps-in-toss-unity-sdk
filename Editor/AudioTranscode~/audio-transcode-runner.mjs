// -----------------------------------------------------------------------
// audio-transcode-runner.mjs - 스트리밍 오디오 빌드타임 트랜스코더 (MP3 → 저비트레이트 MP3)
//
// AITAudioStreamTranscoder(C#)가 SDK 내장 Node.js로 실행하는 단일 파일 러너.
// stdin:  {"targetKbps":160,"files":[{"idx":0,"src":"/abs/in.mp3","dst":"/abs/out.mp3"}]}
// stdout: {"results":[{"idx":0,"raw":N,"out":N,"srcKbps":N,"durationSec":N,"channels":N,
//                      "sampleRate":N,"error":""}]}
//
// 코덱: mpg123-decoder(디코드) + wasm-media-encoders/LAME(인코드) — 순수 WASM, 네이티브
//   바이너리 없음. 컨테이너/코덱을 MP3로 유지하므로 런타임(UnityWebRequestMultimedia →
//   브라우저 미디어 엘리먼트) 경로가 불변이고 iOS WKWebView 호환성이 소스와 동일하다.
// 실패 정책: 파일 단위 격리 — 한 파일의 디코드/인코드 실패는 해당 항목 error 로만 보고하고
//   나머지는 계속한다. 채택/원본유지 판단은 호출부(C#) 몫.
// -----------------------------------------------------------------------
'use strict';

import { MPEGDecoder } from 'mpg123-decoder';
import { createMp3Encoder } from 'wasm-media-encoders';
import { readFileSync, writeFileSync } from 'node:fs';

/** MP3 프레임(1152샘플) 정렬 인코딩 청크. 대용량 PCM을 한 번에 넘기지 않기 위한 슬라이스 크기. */
const CHUNK_SAMPLES = 1152 * 512;

async function transcodeOne(encoderPromiseFactory, file, targetKbps) {
  const res = { idx: file.idx, raw: 0, out: 0, srcKbps: 0, durationSec: 0, channels: 0, sampleRate: 0, error: '' };
  let decoder = null;
  try {
    const buf = readFileSync(file.src);
    res.raw = buf.length;

    decoder = new MPEGDecoder();
    await decoder.ready;
    const { channelData, samplesDecoded, sampleRate, errors } = decoder.decode(new Uint8Array(buf));
    if (errors && errors.length > 0) {
      res.error = `decode errors: ${errors.length}`;
      return res;
    }

    const channels = channelData.length;
    if (samplesDecoded <= 0 || channels < 1 || channels > 2) {
      res.error = `unsupported pcm shape: ${channels}ch ${samplesDecoded} samples`;
      return res;
    }

    res.channels = channels;
    res.sampleRate = sampleRate;
    res.durationSec = samplesDecoded / sampleRate;
    res.srcKbps = Math.round((buf.length * 8) / res.durationSec / 1000);

    const encoder = await encoderPromiseFactory();
    encoder.configure({ sampleRate, channels, bitrate: targetKbps });

    const parts = [];
    for (let off = 0; off < samplesDecoded; off += CHUNK_SAMPLES) {
      const end = Math.min(off + CHUNK_SAMPLES, samplesDecoded);
      const slice = channelData.map((ch) => ch.subarray(off, end));
      const chunk = encoder.encode(slice);
      if (chunk.length > 0) {
        parts.push(Uint8Array.from(chunk));
      }
    }

    const fin = encoder.finalize();
    if (fin.length > 0) {
      parts.push(Uint8Array.from(fin));
    }

    const total = parts.reduce((n, p) => n + p.length, 0);
    const outBuf = Buffer.concat(parts.map((p) => Buffer.from(p)), total);
    if (outBuf.length <= 0) {
      res.error = 'empty encoder output';
      return res;
    }

    writeFileSync(file.dst, outBuf);
    res.out = outBuf.length;
    return res;
  } catch (e) {
    res.error = String((e && e.message) || e).slice(0, 300);
    return res;
  } finally {
    if (decoder) {
      try { decoder.free(); } catch { /* 해제 실패 무시 */ }
    }
  }
}

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

  const targetKbps = (req.targetKbps | 0) || 160;
  const results = [];
  for (const f of req.files || []) {
    // 인코더 인스턴스는 파일마다 새로 생성 — configure 재사용 상태 오염 방지.
    results.push(await transcodeOne(() => createMp3Encoder(), f, targetKbps));
  }

  process.stdout.write(JSON.stringify({ results }));
});
