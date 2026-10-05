// @ts-check
import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

/**
 * ait-datarelease.js(소비한 data 구간 해제) 단위 테스트 — 브라우저·Unity 빌드 없이 Node 에서 돈다.
 *
 * 로더 패치가 부르는 훅(alloc / created)을 가짜 MEMFS 노드로 직접 구동해 해제 조건을 확인한다.
 *  - 꺼짐 조건: __AIT_PERF 가 없거나 releaseConsumedData 가 true 가 아니거나 dataRawSize 가 어긋나면 alloc 은 null(= 로더 stock).
 *  - 정상 경로: metadata 가 정확히 한 번 전부 읽히면 첫 프레임(rAF/타이머) 뒤에 부모 버퍼가 metadata 시작 오프셋으로 줄고,
 *    metadata 뒤의 파일은 내용이 보존된 독립 버퍼로 옮겨지며, 앞 파일은 그대로 읽힌다.
 *  - 취소 조건: metadata 를 두 번 읽거나 mmap 하거나, 뒤쪽 파일이 경계에 걸치거나 너무 크면 해제하지 않는다.
 *  - 해제 뒤 재읽기는 console.error 로 드러난다.
 *  - TextDecoder shim 은 alloc 부터 첫 파일 생성 직후 타이머까지만 걸린다.
 * 실제 Chromium 에서의 보유 ArrayBuffer·footprint 측정은 로컬 하네스로 따로 했다(커밋 메시지 참조).
 */

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SRC = fs.readFileSync(
  path.resolve(__dirname, '../../../WebGLTemplates/AITTemplate/Runtime/ait-datarelease.js'),
  'utf8'
);

const MB = 1048576;

/**
 * 스크립트를 격리된 가짜 window 로 로드한다. 타이머는 큐에 쌓고 flush() 로 비운다.
 * @param {any} perf  window.__AIT_PERF 값(undefined 면 객체 자체가 없다)
 */
function load(perf) {
  const win = /** @type {any} */ ({});
  if (perf !== undefined) win.__AIT_PERF = perf;
  win.unityConfig = { dataUrl: 'Build/x.data' };
  /** @type {Array<() => void>} */
  const timers = [];
  /** @type {Array<() => void>} */
  const frames = [];
  /** @type {string[]} */
  const errors = [];
  /** @type {string[]} */
  const logs = [];
  const fakeConsole = {
    log: (/** @type {any} */ m) => logs.push(String(m)),
    warn: (/** @type {any} */ m) => logs.push(String(m)),
    error: (/** @type {any} */ m) => errors.push(String(m)),
  };
  // eslint-disable-next-line no-new-func
  const fn = new Function('window', 'location', 'setTimeout', 'requestAnimationFrame', 'console', 'performance', SRC);
  fn(
    win,
    { href: 'https://example.test/' },
    (/** @type {() => void} */ f) => { timers.push(f); return timers.length; },
    (/** @type {() => void} */ f) => { frames.push(f); return frames.length; },
    fakeConsole,
    { mark() {} }
  );
  function flush() {
    // 타이머가 새 타이머/프레임을 만들 수 있으므로 비워질 때까지 돈다.
    for (let guard = 0; guard < 50 && (timers.length || frames.length); guard++) {
      const t = timers.splice(0);
      const f = frames.splice(0);
      t.forEach((x) => x());
      f.forEach((x) => x());
    }
  }
  return { win, hook: win.__AIT_DATAREL, flush, errors, logs, timers, frames };
}

/** MEMFS 모양 노드. contents 는 canOwn 으로 받은 subarray 다. */
function makeNode(/** @type {Uint8Array} */ contents) {
  const node = /** @type {any} */ ({ contents, usedBytes: contents.byteLength });
  node.stream_ops = {
    read(/** @type {any} */ stream, /** @type {Uint8Array} */ buffer, /** @type {number} */ offset, /** @type {number} */ length, /** @type {number} */ position) {
      const c = stream.node.contents;
      if (position >= stream.node.usedBytes) return 0;
      const size = Math.min(stream.node.usedBytes - position, length);
      buffer.set(c.subarray(position, position + size), offset);
      return size;
    },
    mmap() { return { ptr: 0, allocated: false }; },
    llseek() { return 0; },
  };
  return node;
}

/**
 * 로더가 하는 일을 흉내낸다: alloc 으로 버퍼를 받고, 파일 목록대로 subarray 를 만들어 created 를 부른다.
 * @param {{ hook: any }} env
 * @param {Array<[string, number]>} files [path, size] 순서대로 이어 붙인다.
 */
function runLoader(env, files) {
  const total = files.reduce((a, f) => a + f[1], 0);
  const view = env.hook.alloc(total, { url: '' });
  if (!view) return { view: null, nodes: [], total };
  for (let i = 0; i < view.length; i++) view[i] = (i * 31 + 7) & 255;
  const originals = new Map();
  const nodes = /** @type {Record<string, any>} */ ({});
  let off = 0;
  for (const [p, size] of files) {
    originals.set(p, view.slice(off, off + size));
    const node = makeNode(view.subarray(off, off + size));
    nodes[p] = env.hook.created(node, p);
    off += size;
  }
  return { view, nodes, total, originals };
}

function readAll(/** @type {any} */ node, /** @type {number} */ chunk) {
  const out = new Uint8Array(node.usedBytes);
  let pos = 0;
  while (pos < out.length) {
    const tmp = new Uint8Array(Math.min(chunk, out.length - pos));
    const n = node.stream_ops.read({ node }, tmp, 0, tmp.length, pos);
    if (n <= 0) break;
    out.set(tmp.subarray(0, n), pos);
    pos += n;
  }
  return { data: out, bytes: pos };
}

function same(/** @type {Uint8Array} */ a, /** @type {Uint8Array} */ b) {
  return Buffer.from(a.buffer, a.byteOffset, a.byteLength).equals(Buffer.from(b.buffer, b.byteOffset, b.byteLength));
}

const ON = (/** @type {number} */ raw) => ({ releaseConsumedData: true, dataRawSize: raw, unityweb: false });
const LAYOUT = /** @type {Array<[string, number]>} */ ([
  ['data.unity3d', 3 * MB],
  ['boot.config', 49],
  ['Il2CppData/Metadata/global-metadata.dat', 1 * MB],
  ['Resources/unity default resources', 256 * 1024],
]);
const LAYOUT_TOTAL = LAYOUT.reduce((a, f) => a + f[1], 0);

test.describe('ait-datarelease', () => {
  test('로드하면 훅 객체를 정의한다', () => {
    const { hook } = load(ON(100));
    expect(typeof hook.alloc).toBe('function');
    expect(typeof hook.created).toBe('function');
    expect(typeof hook.getState).toBe('function');
  });

  test('꺼짐 조건에서는 alloc 이 null 이고 created 는 노드를 그대로 돌려준다', () => {
    const cases = /** @type {Array<[string, any]>} */ ([
      ['객체 없음', undefined],
      ['키 없음', {}],
      ['releaseConsumedData=false', { releaseConsumedData: false, dataRawSize: 100 }],
      ['unityweb', { releaseConsumedData: true, dataRawSize: 100, unityweb: true }],
      ['dataRawSize 미상', { releaseConsumedData: true, dataRawSize: -1 }],
      ['dataRawSize 문자열', { releaseConsumedData: true, dataRawSize: '100' }],
    ]);
    for (const [label, perf] of cases) {
      const { hook } = load(perf);
      expect(hook.alloc(100, { url: '' }), label).toBeNull();
      const node = makeNode(new Uint8Array(4));
      expect(hook.created(node, 'a'), label).toBe(node);
      expect(hook.created(undefined, 'a'), label).toBeUndefined();
      expect(hook.getState().allocated, label).toBe(false);
    }
  });

  test('d 가 RAW 와 다르면 alloc 은 null 이다(압축 전송 크기 등)', () => {
    const { hook } = load(ON(1000));
    expect(hook.alloc(999, { url: '' })).toBeNull();
    expect(hook.alloc(2000, { url: '' })).toBeNull();
    expect(hook.getState().allocated).toBe(false);
  });

  test('data 가 아닌 URL 의 응답은 거른다', () => {
    const { hook } = load(ON(1000));
    expect(hook.alloc(1000, { url: 'https://example.test/Build/other.wasm' })).toBeNull();
    const view = hook.alloc(1000, { url: 'https://example.test/Build/x.data' });
    expect(view).not.toBeNull();
  });

  test('alloc 은 크기 조절 가능 ArrayBuffer 의 뷰를 한 번만 준다', () => {
    const { hook } = load(ON(4096));
    const view = hook.alloc(4096, { url: '' });
    expect(view).toBeInstanceOf(Uint8Array);
    expect(view.length).toBe(4096);
    expect(view.buffer.resizable).toBe(true);
    expect(view.buffer.maxByteLength).toBe(4096);
    expect(hook.alloc(4096, { url: '' })).toBeNull();
  });

  test('metadata 를 한 번 전부 읽으면 첫 프레임 뒤에 부모 버퍼를 자르고 뒤 파일을 옮긴다', () => {
    const env = load(ON(LAYOUT_TOTAL));
    const { view, nodes, originals } = runLoader(env, LAYOUT);
    const buf = view.buffer;
    const metaPath = 'Il2CppData/Metadata/global-metadata.dat';
    const metaOff = 3 * MB + 49;

    const r = readAll(nodes[metaPath], 64 * 1024);
    expect(r.bytes).toBe(1 * MB);
    // 읽기 직후에는 아직 해제하지 않는다(타이머/프레임 대기).
    expect(buf.byteLength).toBe(LAYOUT_TOTAL);
    expect(env.hook.getState().armed).toBe(true);

    env.flush();

    const st = env.hook.getState();
    expect(st.released).toBe(true);
    expect(st.cancelled).toBe('');
    expect(st.before).toBe(LAYOUT_TOTAL);
    expect(st.after).toBe(metaOff);
    expect(buf.byteLength).toBe(metaOff);
    expect(st.movedFiles).toEqual(['Resources/unity default resources']);

    // metadata 는 비워졌다.
    expect(nodes[metaPath].contents.byteLength).toBe(0);
    expect(nodes[metaPath].usedBytes).toBe(0);
    // 앞 파일은 그대로 읽힌다(자르기 전 뷰 그대로 유효).
    expect(same(readAll(nodes['data.unity3d'], 1 * MB).data, originals.get('data.unity3d'))).toBe(true);
    expect(same(readAll(nodes['boot.config'], 16).data, originals.get('boot.config'))).toBe(true);
    // 뒤 파일은 내용이 보존된 독립 버퍼다.
    const moved = nodes['Resources/unity default resources'];
    expect(moved.contents.buffer).not.toBe(buf);
    expect(moved.contents.buffer.resizable).toBe(false);
    expect(same(readAll(moved, 4096).data, originals.get('Resources/unity default resources'))).toBe(true);
    expect(env.errors).toEqual([]);
  });

  test('metadata 를 여러 번에 나눠 한 번의 순회로 읽어도 해제한다', () => {
    const env = load(ON(LAYOUT_TOTAL));
    const { nodes } = runLoader(env, LAYOUT);
    readAll(nodes['Il2CppData/Metadata/global-metadata.dat'], 100000);
    env.flush();
    expect(env.hook.getState().released).toBe(true);
  });

  test('metadata 읽기가 끝나기 전에는 해제하지 않는다', () => {
    const env = load(ON(LAYOUT_TOTAL));
    const { view, nodes } = runLoader(env, LAYOUT);
    const node = nodes['Il2CppData/Metadata/global-metadata.dat'];
    const tmp = new Uint8Array(1000);
    node.stream_ops.read({ node }, tmp, 0, 1000, 0);
    env.flush();
    expect(env.hook.getState().released).toBe(false);
    expect(view.buffer.byteLength).toBe(LAYOUT_TOTAL);
  });

  test('끝부분만 먼저 읽는 경우(누적 바이트 부족)는 해제 시점이 아니다', () => {
    const env = load(ON(LAYOUT_TOTAL));
    const { nodes } = runLoader(env, LAYOUT);
    const node = nodes['Il2CppData/Metadata/global-metadata.dat'];
    const tmp = new Uint8Array(100);
    node.stream_ops.read({ node }, tmp, 0, 100, 1 * MB - 100);
    env.flush();
    expect(env.hook.getState().armed).toBe(false);
    expect(env.hook.getState().released).toBe(false);
  });

  test('metadata 를 두 번 읽으면 해제를 영구 취소한다', () => {
    const env = load(ON(LAYOUT_TOTAL));
    const { view, nodes } = runLoader(env, LAYOUT);
    const node = nodes['Il2CppData/Metadata/global-metadata.dat'];
    readAll(node, 1 * MB);
    // 해제 전(타이머 대기 중)에 한 번 더 읽는다.
    readAll(node, 1 * MB);
    env.flush();
    const st = env.hook.getState();
    expect(st.released).toBe(false);
    expect(st.cancelled).toBe('metadata-read-more-than-once');
    expect(view.buffer.byteLength).toBe(LAYOUT_TOTAL);
    expect(node.contents.byteLength).toBe(1 * MB);
  });

  test('metadata 를 mmap 하면 해제하지 않는다', () => {
    const env = load(ON(LAYOUT_TOTAL));
    const { view, nodes } = runLoader(env, LAYOUT);
    const node = nodes['Il2CppData/Metadata/global-metadata.dat'];
    node.stream_ops.mmap({ node }, 0, 1 * MB, 0, 2, 0);
    readAll(node, 1 * MB);
    env.flush();
    expect(env.hook.getState().released).toBe(false);
    expect(env.hook.getState().cancelled).toBe('metadata-mmap');
    expect(view.buffer.byteLength).toBe(LAYOUT_TOTAL);
  });

  test('해제 뒤 metadata 를 읽으면 console.error 로 드러나고 lateReads 가 센다', () => {
    const env = load(ON(LAYOUT_TOTAL));
    const { nodes } = runLoader(env, LAYOUT);
    const node = nodes['Il2CppData/Metadata/global-metadata.dat'];
    readAll(node, 1 * MB);
    env.flush();
    expect(env.hook.getState().released).toBe(true);
    expect(env.errors.length).toBe(0);

    const tmp = new Uint8Array(16);
    const n = node.stream_ops.read({ node }, tmp, 0, 16, 0);
    expect(n).toBe(0); // 비워진 노드는 EOF 로 보인다
    expect(env.hook.getState().lateReads).toBe(1);
    expect(env.errors.length).toBe(1);
    expect(env.errors[0]).toContain('[AIT-DataRelease]');
  });

  test('metadata 는 공유 stream_ops 가 아니라 노드 단위 사본에만 훅을 건다', () => {
    const env = load(ON(LAYOUT_TOTAL));
    const view = env.hook.alloc(LAYOUT_TOTAL, { url: '' });
    const shared = makeNode(view.subarray(0, 8)).stream_ops;
    const meta = makeNode(view.subarray(8, 64));
    meta.stream_ops = shared;
    const other = makeNode(view.subarray(64, 128));
    other.stream_ops = shared;
    const origRead = shared.read;
    env.hook.created(meta, 'Il2CppData/Metadata/global-metadata.dat');
    env.hook.created(other, 'data.unity3d');
    expect(shared.read).toBe(origRead);
    expect(other.stream_ops).toBe(shared);
    expect(meta.stream_ops).not.toBe(shared);
  });

  test('metadata 뒤 파일이 경계에 걸치면 해제하지 않는다', () => {
    const env = load(ON(LAYOUT_TOTAL));
    const { view, nodes } = runLoader(env, LAYOUT);
    const metaPath = 'Il2CppData/Metadata/global-metadata.dat';
    // data.unity3d 가 metadata 시작 너머까지 이어지는 것처럼 만든다.
    nodes['data.unity3d'].contents = new Uint8Array(view.buffer, 0, 3 * MB + 100);
    // 경계 판정은 recs 에 기록된 노드의 현재 contents 를 본다.
    readAll(nodes[metaPath], 1 * MB);
    env.flush();
    expect(env.hook.getState().released).toBe(false);
    expect(env.hook.getState().cancelled).toContain('straddle');
    expect(view.buffer.byteLength).toBe(LAYOUT_TOTAL);
  });

  test('metadata 뒤 파일이 너무 크면(16MB 초과) 해제하지 않는다', () => {
    const layout = /** @type {Array<[string, number]>} */ ([
      ['data.unity3d', 1 * MB],
      ['Il2CppData/Metadata/global-metadata.dat', 1 * MB],
      ['Resources/huge', 17 * MB],
    ]);
    const total = layout.reduce((a, f) => a + f[1], 0);
    const env = load(ON(total));
    const { view, nodes } = runLoader(env, layout);
    readAll(nodes['Il2CppData/Metadata/global-metadata.dat'], 1 * MB);
    env.flush();
    expect(env.hook.getState().released).toBe(false);
    expect(env.hook.getState().cancelled).toContain('movers-too-large');
    expect(view.buffer.byteLength).toBe(total);
  });

  test('metadata 가 마지막 파일이면 옮길 파일 없이 자른다', () => {
    const layout = /** @type {Array<[string, number]>} */ ([
      ['data.unity3d', 2 * MB],
      ['Il2CppData/Metadata/global-metadata.dat', 1 * MB],
    ]);
    const total = 3 * MB;
    const env = load(ON(total));
    const { view, nodes, originals } = runLoader(env, layout);
    readAll(nodes['Il2CppData/Metadata/global-metadata.dat'], 1 * MB);
    env.flush();
    expect(env.hook.getState().released).toBe(true);
    expect(env.hook.getState().movedFiles).toEqual([]);
    expect(view.buffer.byteLength).toBe(2 * MB);
    expect(same(readAll(nodes['data.unity3d'], 1 * MB).data, originals.get('data.unity3d'))).toBe(true);
  });

  test('metadata 가 없으면(IL2CPP 아님 등) 아무것도 해제하지 않는다', () => {
    const layout = /** @type {Array<[string, number]>} */ ([
      ['data.unity3d', 2 * MB],
      ['boot.config', 49],
    ]);
    const env = load(ON(2 * MB + 49));
    const { view } = runLoader(env, layout);
    env.flush();
    expect(env.hook.getState().metaFound).toBe(false);
    expect(env.hook.getState().released).toBe(false);
    expect(view.buffer.byteLength).toBe(2 * MB + 49);
  });

  test('노드 내용이 크기 조절 불가 버퍼면(다른 경로로 만든 파일) created 는 기록하지 않는다', () => {
    const env = load(ON(100));
    expect(env.hook.alloc(100, { url: '' })).not.toBeNull();
    const node = makeNode(new Uint8Array(100));
    expect(env.hook.created(node, 'Il2CppData/Metadata/global-metadata.dat')).toBe(node);
    expect(env.hook.getState().metaFound).toBe(false);
    expect(node.stream_ops.read.toString()).not.toContain('late-read');
  });

  test('TextDecoder shim 은 alloc 부터 첫 파일 생성 직후 타이머까지만 걸린다', () => {
    const original = TextDecoder.prototype.decode;
    try {
      const env = load(ON(LAYOUT_TOTAL));
      expect(TextDecoder.prototype.decode).toBe(original);
      const view = env.hook.alloc(LAYOUT_TOTAL, { url: '' });
      expect(TextDecoder.prototype.decode).not.toBe(original);
      // 크기 조절 가능 버퍼 위의 뷰도 디코드된다(Chromium 은 원본이 이를 거부한다).
      view.set(new TextEncoder().encode('hello'), 0);
      expect(new TextDecoder().decode(view.subarray(0, 5))).toBe('hello');
      // 일반 입력은 그대로 통과한다.
      expect(new TextDecoder().decode(new TextEncoder().encode('héllo'))).toBe('héllo');

      env.hook.created(makeNode(view.subarray(0, 64)), 'a');
      expect(TextDecoder.prototype.decode).not.toBe(original); // 같은 동기 구간에서는 아직 걸려 있다
      env.flush();
      expect(TextDecoder.prototype.decode).toBe(original);
    } finally {
      TextDecoder.prototype.decode = original;
    }
  });

  test('shim 이 걸린 사이 다른 코드가 decode 를 다시 감쌌다면 건드리지 않는다', () => {
    const original = TextDecoder.prototype.decode;
    try {
      const env = load(ON(LAYOUT_TOTAL));
      const view = env.hook.alloc(LAYOUT_TOTAL, { url: '' });
      const other = function (/** @type {any} */ a, /** @type {any} */ b) { return original.call(this, a, b); };
      TextDecoder.prototype.decode = other;
      env.hook.created(makeNode(view.subarray(0, 64)), 'a');
      env.flush();
      expect(TextDecoder.prototype.decode).toBe(other);
    } finally {
      TextDecoder.prototype.decode = original;
    }
  });

  test('크기 조절 ArrayBuffer 를 지원하지 않는 엔진에서는 alloc 이 null 이다', () => {
    const resize = ArrayBuffer.prototype.resize;
    try {
      // @ts-ignore 테스트용으로 기능을 지운다.
      delete ArrayBuffer.prototype.resize;
      const { hook, win } = load(ON(100));
      expect(win.__AIT_DATAREL).toBe(hook);
      expect(hook.alloc(100, { url: '' })).toBeNull();
      expect(hook.getState().reason).toBe('no-resizable-arraybuffer');
    } finally {
      // @ts-ignore
      ArrayBuffer.prototype.resize = resize;
    }
  });

  test('getState 는 복사본을 돌려준다', () => {
    const { hook } = load(ON(100));
    const s = hook.getState();
    s.released = true;
    expect(hook.getState().released).toBe(false);
  });
});
