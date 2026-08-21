/**
 * ait-playerprefs.js (IDBFS syncfs 어댑터) 회귀 테스트
 *
 * WebGLTemplates/AITTemplate/Runtime/ait-playerprefs.js는 브라우저 없이도 node:vm
 * 샌드박스 위에서 그대로 실행할 수 있는 순수 IIFE다. Emscripten IDBFS와 앱인토스
 * Storage를 in-memory 목으로 대체하면 부팅 분기 전체를 결정적으로 돌려볼 수 있어,
 * E2E(실제 Unity 빌드)보다 훨씬 싸게 분기 판정표를 고정할 수 있다.
 *
 * ⚠️ 아래 두 가지는 이 테스트 스위트가 존재하는 핵심 이유이므로 반드시 유지한다
 *    (둘 다 실기기 E2E 9-8이 잡아낸 결함과 직결된다):
 *
 * 1) 마운트포인트는 '/idbfs/<hash>'가 아니라 '/idbfs' 그 자체다.
 *    Unity 원본 prejs(IdbFs.js)는 `FS.mkdir('/idbfs'); FS.mount(IDBFS, ..., '/idbfs')`
 *    순서로 마운트한다. 즉 mount.mountpoint는 항상 '/idbfs'이고, 앱 디렉터리
 *    '/idbfs/<hash>'는 그 안쪽에 Unity 네이티브가 persistentDataPath를 처음
 *    만질 때 비로소 생긴다. 이전 버전의 검증 하니스는 마운트포인트를
 *    '/idbfs/<hash>'로 잘못 가정했고, 그 결과 tryLegacyImport의 mountpoint
 *    검사가 항상 'skip-mountpoint'로 빠져 레거시 임포트가 영원히 발화하지
 *    않는 결함을 놓쳤다. 아래 테스트는 MOUNT='/idbfs'로 고정해 이 회귀를 잡는다
 *    (ait-playerprefs.js의 IDBFS_ROOT 상수 / tryLegacyImport 참조).
 *
 * 2) 앱 디렉터리 '<hash>'는 빌드가 서비스되는 URL(origin)에서 유도되므로
 *    origin이 바뀌면 값도 바뀐다. 옛 origin에서 쓰던 PlayerPrefs를 새 origin으로
 *    그대로 옮기면 해시가 달라지므로, 옛 경로('/idbfs/<OLD_HASH>/PlayerPrefs')를
 *    그대로 심어봤자 Unity는 그 경로를 읽지 않는다(좌초 경로). 그래서
 *    normalizeLegacyDump/resolveAppDir는 반드시 "현재" 앱 디렉터리로 리매핑해서
 *    심어야 한다 — 케이스 C가 이 리매핑을 직접 검증한다.
 */
import { describe, test, expect } from 'vitest';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SRC_PATH = path.resolve(
  __dirname,
  '../../..',
  'WebGLTemplates/AITTemplate/Runtime/ait-playerprefs.js',
);
const SRC = fs.readFileSync(SRC_PATH, 'utf8');

// ===========================================
// 고정 경로 (Unity 원본 prejs와 동일한 토폴로지 — 위 헤더 설명 참조)
// ===========================================
const MOUNT = '/idbfs'; // Unity가 IDBFS를 마운트하는 지점 그 자체
const APP = '/idbfs/decd5f9d7b96'; // 현재 origin의 앱 디렉터리
const OLD = '/idbfs/70659e603950'; // 옛 origin의 앱 디렉터리 (해시가 다르다)
const DIR_MODE = 16877;
const FILE_MODE = 33188;

interface FsEntry {
  mode: number;
  timestamp: number;
  contents?: unknown;
}

interface MountMock {
  mountpoint: string;
  type: IdbfsMock;
  idbPersistState?: unknown;
}

interface IdbfsMock {
  syncfs(mount: MountMock, populate: boolean, cb: (err: Error | null) => void): void;
  getLocalSet(
    mount: MountMock,
    cb: (err: Error | null, result?: { entries: Record<string, {}> }) => void,
  ): void;
  loadLocalEntry(path: string, cb: (err: Error | null, entry?: FsEntry) => void): void;
  storeLocalEntry(path: string, entry: FsEntry, cb: (err: Error | null) => void): void;
  removeLocalEntry(path: string, cb: (err: Error | null) => void): void;
}

/** Emscripten IDBFS 백엔드를 흉내 내는 in-memory 목(mock). */
function makeFs(initial: Record<string, FsEntry>): { idbfs: IdbfsMock; entries: Map<string, FsEntry> } {
  const entries = new Map<string, FsEntry>(Object.entries(initial));
  const idbfs: IdbfsMock = {
    syncfs(_mount, _populate, cb) {
      cb(null);
    },
    getLocalSet(_mount, cb) {
      const out: Record<string, {}> = {};
      for (const k of entries.keys()) out[k] = {};
      cb(null, { entries: out });
    },
    loadLocalEntry(entryPath, cb) {
      const e = entries.get(entryPath);
      if (!e) {
        cb(new Error('ENOENT'));
        return;
      }
      cb(null, e);
    },
    storeLocalEntry(entryPath, entry, cb) {
      // Emscripten storeLocalEntry는 디렉터리 엔트리에만 mkdirTree를 부르고 파일에는
      // FS.writeFile을 부른다. 따라서 파일은 부모가 **존재하고 또 디렉터리여야** 쓰인다
      // — 존재만 보면 부모가 파일일 때 실제로는 ENOTDIR로 실패할 쓰기를 목이 성공으로
      // 처리해, "말이 안 되는 경로로 임포트 성공"이 테스트를 통과해버린다.
      const parent = entryPath.slice(0, entryPath.lastIndexOf('/'));
      const isDir = (entry.mode & 61440) === 16384;
      if (!isDir && parent) {
        const parentEntry = entries.get(parent);
        if (!parentEntry) {
          cb(new Error('ENOENT: ' + parent));
          return;
        }
        if ((parentEntry.mode & 61440) !== 16384) {
          cb(new Error('ENOTDIR: ' + parent));
          return;
        }
      }
      entries.set(entryPath, entry);
      cb(null);
    },
    removeLocalEntry(entryPath, cb) {
      entries.delete(entryPath);
      cb(null);
    },
  };
  return { idbfs, entries };
}

interface BootOptions {
  storageSeed?: Record<string, string>;
  legacySource?: { readIdbfs: () => Promise<unknown> } | null;
  fsInit?: Record<string, FsEntry>;
  isProduction?: boolean;
}

interface BootResult {
  // ait-playerprefs.js가 window에 동적으로 __AIT_PP 등을 붙이는 순수 JS 샌드박스라
  // 정적 타입을 걸기보다 any로 다루는 편이 테스트 의도를 더 잘 드러낸다.
  win: any;
  store: Map<string, string>;
  entries: Map<string, FsEntry>;
  mount: MountMock;
  idbfs: IdbfsMock;
}

/** ait-playerprefs.js를 node:vm 샌드박스에 로드하고 IDBFS 마운트까지 완료한 상태로 부팅한다. */
function boot(opts: BootOptions = {}): BootResult {
  const { storageSeed = {}, legacySource = null, fsInit = {}, isProduction = false } = opts;
  const store = new Map<string, string>(Object.entries(storageSeed));
  const win: any = {
    setTimeout,
    clearTimeout,
    btoa,
    atob,
    console,
    Promise,
    Date,
    localStorage: { getItem: () => null, setItem: () => {}, removeItem: () => {} },
    sessionStorage: { getItem: () => null, setItem: () => {}, removeItem: () => {} },
    addEventListener: () => {},
    __AIT_PLAYERPREFS: { isProduction, enabled: true },
    __AIT_PLAYERPREFS_STORAGE__: {
      getItem: (k: string) => Promise.resolve(store.has(k) ? store.get(k)! : null),
      setItem: (k: string, v: string) => {
        store.set(k, v);
        return Promise.resolve();
      },
    },
  };
  if (legacySource) win.__AIT_PP_LEGACY_SOURCE__ = legacySource;

  const ctx = vm.createContext({
    window: win,
    console,
    setTimeout,
    clearTimeout,
    btoa,
    atob,
    Promise,
    Date,
    document: { addEventListener: () => {} },
    Uint8Array,
    ArrayBuffer,
    Object,
    Array,
    JSON,
    Number,
    String,
    Math,
    Error,
    isFinite,
  });
  (ctx as any).globalThis = ctx;
  vm.runInContext(SRC, ctx);

  const { idbfs, entries } = makeFs(fsInit);
  const mount: MountMock = { mountpoint: MOUNT, type: idbfs };
  const cfg: any = {};
  win.__AIT_PP.configure(cfg);
  const Module: any = {};
  for (const fn of cfg.preRun) fn(Module);
  // preRun이 심어둔 defineProperty 트랩(setter)을 통해 실제 마운트를 주입한다.
  Module.__unityIdbfsMount = { mount };
  return { win, store, entries, mount, idbfs };
}

/** b.idbfs.syncfs(populate)를 Promise로 감싼 헬퍼 — 각 케이스의 반복 보일러플레이트 축소용. */
function syncfs(b: BootResult, populate: boolean): Promise<void> {
  return new Promise((resolve) => b.idbfs.syncfs(b.mount, populate, () => resolve()));
}

function wait(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

const dirEntry = (): FsEntry => ({ mode: DIR_MODE, timestamp: 1 });
const ppEntry = (text: string): FsEntry => ({
  mode: FILE_MODE,
  timestamp: 1700000000000,
  contents: new Uint8Array(Buffer.from(text)),
});

// "이 origin에서 한 번 이상 부팅한 설치" — 앱 디렉터리는 있고 PlayerPrefs는 없다
// (SDK는 Sentry 설치 id 등을 persistentDataPath에 남기므로 실제로 흔한 상태다)
const bootedBefore: Record<string, FsEntry> = {
  [MOUNT]: dirEntry(),
  [APP]: dirEntry(),
  [APP + '/Sentry']: dirEntry(),
};

function manifestFiles(raw: string | undefined | null): string[] | null {
  if (raw === undefined || raw === null) return null;
  return Object.keys(JSON.parse(JSON.parse(raw).inline).files);
}

describe('ait-playerprefs.js — IDBFS syncfs 어댑터', () => {
  describe('기본 부트 경로', () => {
    test('A) absent + 로컬 빈 상태 + 레거시 없음 → 빈 매니페스트를 쓰지 않는다', async () => {
      const b = boot({ fsInit: { [MOUNT]: dirEntry() } });
      await syncfs(b, true);
      await wait(300);
      const s = b.win.__AIT_PP.status();
      expect(s.mode).toBe('ait');
      expect(b.store.has('AITUnityFS_v1_manifest')).toBe(false);
      expect(s.legacyImport).toBe('none');
    });

    test('B) absent + 로컬 PlayerPrefs 존재 → 기존 마이그레이션 승격 push는 그대로 동작한다', async () => {
      const b = boot({ fsInit: { ...bootedBefore, [APP + '/PlayerPrefs']: ppEntry('hello') } });
      await syncfs(b, true);
      await wait(300);
      const files = manifestFiles(b.store.get('AITUnityFS_v1_manifest'));
      expect(files).not.toBeNull();
      expect(files).toContain(APP + '/PlayerPrefs');
    });

    test('G) 레거시 훅 미설치 → 어댑터 도입 이전과 동일하게 동작한다 (무회귀)', async () => {
      const b = boot({ fsInit: bootedBefore });
      await syncfs(b, true);
      await wait(300);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('none');
      expect(s.legacyBackend).toBe('none');
      expect(b.store.has('AITUnityFS_v1_manifest')).toBe(false);
    });
  });

  describe('레거시 origin 마이그레이션 (tryLegacyImport)', () => {
    // ★ C) 9-8 회귀 재현: absent + 옛 origin 해시의 레거시 덤프
    //    → 마운트포인트가 아니라 앱 디렉터리 기준으로 리매핑돼 임포트되어야 한다.
    //    (마운트포인트를 '/idbfs/<hash>'로 잘못 잡던 이전 하니스에서는 항상
    //     skip-mountpoint로 빠져 이 경로가 통과하지 못했다 — 파일 헤더 설명 참조)
    test('C) 옛 origin 해시의 레거시 덤프가 현재 앱 디렉터리로 리매핑되어 임포트된다', async () => {
      const legacyDump = {
        [OLD + '/PlayerPrefs']: {
          mode: FILE_MODE,
          timestamp: 5,
          contents: Array.from(Buffer.from('legacy-bytes')),
        },
      };
      const b = boot({
        fsInit: bootedBefore,
        isProduction: true,
        legacySource: { readIdbfs: () => Promise.resolve(legacyDump) },
      });
      await syncfs(b, true);
      await wait(300);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('imported');
      expect(b.entries.has(APP + '/PlayerPrefs')).toBe(true); // 현재 앱 디렉터리로 리매핑
      expect(b.entries.has(OLD + '/PlayerPrefs')).toBe(false); // 옛 경로는 심지 않는다
      const bytes = b.entries.get(APP + '/PlayerPrefs');
      expect(bytes).toBeDefined();
      expect(Buffer.from(bytes!.contents as Uint8Array).toString()).toBe('legacy-bytes');
      const files = manifestFiles(b.store.get('AITUnityFS_v1_manifest'));
      expect(files).toContain(APP + '/PlayerPrefs'); // 임포트 후 승격 push
    });

    test('C2) present-empty 매니페스트에서도 같은 마이그레이션 창이 열려 있어야 한다', async () => {
      const legacyDump = {
        [OLD + '/PlayerPrefs']: {
          mode: FILE_MODE,
          timestamp: 5,
          contents: Array.from(Buffer.from('abc')),
        },
      };
      const emptyManifest = JSON.stringify({
        v: 1,
        seq: 3,
        ts: 1,
        inline: JSON.stringify({ v: 1, seq: 3, scope: 'playerprefs', files: {} }),
      });
      const b = boot({
        fsInit: bootedBefore,
        storageSeed: { AITUnityFS_v1_manifest: emptyManifest },
        legacySource: { readIdbfs: () => Promise.resolve(legacyDump) },
      });
      await syncfs(b, true);
      await wait(300);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('imported');
    });

    // ★ C3) 앱 디렉터리가 아직 없는 최초 부팅 → 심을 곳을 모르므로 포기하되 창은 열어둔다.
    //    appDir('<hash>')는 origin에서 유도되는 값이라 우리가 계산할 수 없다(파일 헤더
    //    설명 2번 참조). 이 origin에서 한 번도 부팅한 적이 없으면 로컬 파일 목록에
    //    '/idbfs/<hash>' 후보가 아예 없으므로 추측해서 심는 대신 포기해야 한다.
    test('C3) 앱 디렉터리 없음 → skip-unknown-appdir, 좌초 경로 미기록, 창 유지', async () => {
      const legacyDump = {
        [OLD + '/PlayerPrefs']: {
          mode: FILE_MODE,
          timestamp: 5,
          contents: Array.from(Buffer.from('abc')),
        },
      };
      const b = boot({
        fsInit: { [MOUNT]: dirEntry() },
        legacySource: { readIdbfs: () => Promise.resolve(legacyDump) },
      });
      await syncfs(b, true);
      await wait(300);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('skip-unknown-appdir');
      expect([...b.entries.keys()].some((k) => /PlayerPrefs$/.test(k))).toBe(false);
      expect(b.store.has('AITUnityFS_v1_manifest')).toBe(false);
      expect(s.mode).toBe('ait'); // 부팅 자체는 정상 진행
    });

    test('C4) 앱 디렉터리 후보가 2개 이상이면 추측하지 않는다', async () => {
      const legacyDump = {
        [OLD + '/PlayerPrefs']: {
          mode: FILE_MODE,
          timestamp: 5,
          contents: Array.from(Buffer.from('abc')),
        },
      };
      const b = boot({
        fsInit: { [MOUNT]: dirEntry(), [APP]: dirEntry(), '/idbfs/other_app_dir': dirEntry() },
        legacySource: { readIdbfs: () => Promise.resolve(legacyDump) },
      });
      await syncfs(b, true);
      await wait(300);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('skip-unknown-appdir');
      // C3과 같은 기준: 아무것도 심지 않고 매니페스트도 남기지 않아야 창이 유지된다
      expect([...b.entries.keys()].some((k) => /PlayerPrefs$/.test(k))).toBe(false);
      expect(b.store.has('AITUnityFS_v1_manifest')).toBe(false);
      expect(s.mode).toBe('ait');
    });

    // ⚠️ C5는 **알려진 위험을 고정하는** 테스트다. 통과가 곧 "올바르다"는 뜻이 아니다.
    //
    // 후보가 정확히 1개인데 그게 현재 앱 디렉터리가 아닐 수 있다. 같은 origin에서
    // 서빙 URL만 바뀐 경우(경로 버저닝 /app/v1 → /app/v2 등)가 그렇다 — 옛 URL의
    // /idbfs/<hashA>는 populate로 복원되지만 현재 빌드의 <hashB>는 아직 없으므로,
    // 유일 후보 검사를 <hashA>가 통과한다. 그러면 좌초 경로에 심고 그것이 매니페스트로
    // 승격되며, 다음 부팅은 snapshotHasScopedFile이 true라 창이 영구히 닫힌다.
    // (같은 상황에서 옛 디렉터리에 PlayerPrefs가 남아 있으면 skip-local-present로
    //  먼저 빠지므로, 위험한 조합은 "PlayerPrefs 없는 stale 디렉터리 1개"뿐이다.)
    //
    // 지금은 getPlatformLegacySource()가 null stub이라 훅 없이는 도달하지 않는다.
    // 플랫폼 조회 수단을 실제로 연결하기 전에 반드시 해소해야 하는 항목이며
    // (TODO.md P2 참조), 그때 이 테스트의 기대값이 바뀌어야 한다.
    test('C5) [알려진 위험] 유일 후보가 stale 디렉터리면 좌초 경로에 심는다', async () => {
      const STALE = '/idbfs/staleoldurlhash';
      const legacyDump = {
        [OLD + '/PlayerPrefs']: {
          mode: FILE_MODE,
          timestamp: 5,
          contents: Array.from(Buffer.from('abc')),
        },
      };
      const b = boot({
        // 옛 URL이 남긴 디렉터리 하나. PlayerPrefs는 없어서 skip-local-present에 안 걸린다.
        fsInit: { [MOUNT]: dirEntry(), [STALE]: dirEntry(), [STALE + '/Sentry']: dirEntry() },
        legacySource: { readIdbfs: () => Promise.resolve(legacyDump) },
      });
      await syncfs(b, true);
      await wait(300);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('imported');
      // 현재 거동: Unity가 앞으로 만들 디렉터리가 아니라 stale 쪽에 심힌다
      expect(b.entries.has(STALE + '/PlayerPrefs')).toBe(true);
      expect(b.entries.has(APP + '/PlayerPrefs')).toBe(false);
    });

    // 목이 Emscripten만큼 엄격한지도 함께 지킨다 — 부모가 파일이면 ENOTDIR이라
    // 아무것도 심히지 않고, 매니페스트도 남지 않아 창이 유지돼야 한다.
    test('C6) 유일 후보가 디렉터리가 아니라 파일이면 아무것도 심지 못한다', async () => {
      const legacyDump = {
        [OLD + '/PlayerPrefs']: {
          mode: FILE_MODE,
          timestamp: 5,
          contents: Array.from(Buffer.from('abc')),
        },
      };
      const b = boot({
        fsInit: { [MOUNT]: dirEntry(), '/idbfs/save.dat': { mode: FILE_MODE, timestamp: 1, contents: [1] } },
        legacySource: { readIdbfs: () => Promise.resolve(legacyDump) },
      });
      await syncfs(b, true);
      await wait(300);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyBytes).toBe(0);
      expect(b.entries.has('/idbfs/save.dat/PlayerPrefs')).toBe(false);
      expect(b.store.has('AITUnityFS_v1_manifest')).toBe(false);
      expect(s.mode).toBe('ait');
    });

    // collectScoped의 getLocalSet은 자체 try/catch를 갖지만, 그 뒤 loadEntrySync 루프는
    // Emscripten 호출을 그대로 부르므로 동기 throw가 가능하다(구버전 ErrnoError 등).
    // 이 루프는 scoped 파일이 있을 때만 돌고, seam 이전에는 tryLegacyImport가 그 앞에서
    // 무조건 물러났기 때문에 이 경로 자체가 죽어 있었다. 이제는 레거시 훅이 걸린 부팅마다
    // 지나가므로, 여기서 새어 나가면 populatePath의 try까지 올라가 세션 전체가 vanilla로
    // 강등된다 — 부가 기능(레거시 임포트) 실패가 본 기능(영속화)을 꺼서는 안 된다.
    test('C7) 로컬 수집 중 동기 throw가 세션을 vanilla로 떨어뜨리지 않는다', async () => {
      const b = boot({
        // scoped 파일이 있어야 collectScoped가 loadEntrySync 루프까지 들어간다
        fsInit: {
          [MOUNT]: dirEntry(),
          [APP]: dirEntry(),
          [APP + '/PlayerPrefs']: { mode: FILE_MODE, timestamp: 1, contents: Array.from(Buffer.from('local')) },
        },
        legacySource: { readIdbfs: () => Promise.resolve({}) },
      });
      b.idbfs.loadLocalEntry = () => {
        throw Object.assign(new Error('ErrnoError'), { errno: 44 });
      };
      await syncfs(b, true);
      await wait(300);
      const s = b.win.__AIT_PP.status();
      expect(s.mode).toBe('ait');
      expect(s.legacyImport).toBe('skip-unknown-local');
    });

    test('D) 적대적 contents(숫자) → 거대 할당 없이 즉시 거부한다', async () => {
      const legacyDump = { [OLD + '/PlayerPrefs']: { mode: FILE_MODE, timestamp: 0, contents: 500000000 } };
      const t0 = Date.now();
      const b = boot({ fsInit: bootedBefore, legacySource: { readIdbfs: () => Promise.resolve(legacyDump) } });
      await syncfs(b, true);
      await wait(300);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('empty');
      expect(s.legacyBytes).toBe(0);
      expect(Date.now() - t0).toBeLessThan(2000); // 할당 폭탄 없이 즉시 종료
      expect(b.store.has('AITUnityFS_v1_manifest')).toBe(false);
    });

    test('D2) 상한(256KB) 초과 → 거부한다', async () => {
      const legacyDump = {
        [OLD + '/PlayerPrefs']: { mode: FILE_MODE, timestamp: 0, contents: new Uint8Array(300 * 1024) },
      };
      const b = boot({ fsInit: bootedBefore, legacySource: { readIdbfs: () => Promise.resolve(legacyDump) } });
      await syncfs(b, true);
      await wait(300);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyBytes).toBe(0);
      expect(String(s.lastError)).toMatch(/크기/);
    });

    test('D3) 상한 이내(1KB) → 정상 임포트된다', async () => {
      const legacyDump = {
        [OLD + '/PlayerPrefs']: { mode: FILE_MODE, timestamp: 0, contents: new Uint8Array(1024) },
      };
      const b = boot({ fsInit: bootedBefore, legacySource: { readIdbfs: () => Promise.resolve(legacyDump) } });
      await syncfs(b, true);
      await wait(300);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('imported');
      expect(s.legacyBytes).toBe(1024);
    });

    test('F) 레거시 read가 hang되면 타임아웃 후에도 빈 매니페스트를 남기지 않는다 (다음 부팅 재시도 가능)', async () => {
      const b = boot({ fsInit: bootedBefore, legacySource: { readIdbfs: () => new Promise(() => {}) } });
      await syncfs(b, true);
      await wait(1600);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('timeout');
      expect(s.mode).toBe('ait');
      expect(b.store.has('AITUnityFS_v1_manifest')).toBe(false);
    });
  });

  describe('DeleteAll → persist 반영', () => {
    test('E) present(데이터 있음) → 로컬 삭제 후 persist하면 빈 files가 push된다', async () => {
      const seedFs = { ...bootedBefore, [APP + '/PlayerPrefs']: ppEntry('hi') };
      const b0 = boot({ fsInit: seedFs });
      await syncfs(b0, true);
      await wait(300);
      const seeded = b0.store.get('AITUnityFS_v1_manifest');
      expect(manifestFiles(seeded)).not.toBeNull(); // 사전 조건: 데이터 매니페스트 성립

      const b = boot({ storageSeed: { AITUnityFS_v1_manifest: seeded! }, fsInit: { ...seedFs } });
      await syncfs(b, true);
      await wait(200);
      b.entries.delete(APP + '/PlayerPrefs'); // PlayerPrefs.DeleteAll 모사
      await syncfs(b, false);
      await wait(200);
      const files = manifestFiles(b.store.get('AITUnityFS_v1_manifest'));
      expect(Array.isArray(files)).toBe(true);
      expect(files).toHaveLength(0);
    });
  });
});
