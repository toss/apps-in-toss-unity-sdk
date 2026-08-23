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
 *    그대로 심어봤자 Unity는 그 경로를 읽지 않는다(좌초 경로). 그래서 심을 때는
 *    반드시 "현재" 앱 디렉터리로 리매핑해야 한다(pickLegacyTarget) — 케이스 C가
 *    이 리매핑을 직접 검증한다. 다만 그 "현재" 값은 **추측하지 않는다**: 엔진이
 *    lookup 미스로 직접 건네준 parent 노드에서만 얻는다(tryPlantAt).
 *
 * 3) 이 하니스의 단일 진실원은 **노드 트리**다(flat 경로 Map이 아니다).
 *    Emscripten의 파일시스템은 FSNode 그래프이고, 앱 디렉터리를 "관측"하는 유일한
 *    지점도 그 그래프의 node_ops 계약(lookup/mknod)이다. 경로 문자열 Map으로는
 *    "Unity가 아직 없는 PlayerPrefs를 열려고 한다"는 이벤트 자체를 표현할 수 없어,
 *    앱 디렉터리를 추측하지 않는 구현을 검증할 수 없다. 그래서 makeFs는 노드 트리를
 *    유지하고 `entries`(경로 Map 뷰)는 거기서 파생시킨다 — 기존 단언은 그대로 쓰되
 *    새 단언은 트리(`b.root`, `simulateUnityBoot`)를 쓸 수 있다.
 *    참조한 원본 계약: library_fs.js:40-53(FSNode), :225-244(FS.lookupNode),
 *    :614-616(FS.lookup), :618-648(FS.mknod/FS.mkdir),
 *    library_memfs.js:20-32(ops_table), :68-94(createNode), :183-188(lookup/mknod).
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

// stat.mode 비트 / errno — Emscripten과 같은 값 (library_fs.js, ERRNO_CODES)
const S_IFMT = 61440;
const S_IFDIR = 16384;
const S_IFREG = 32768;
const ERRNO_ENOENT = 44;
const ERRNO_ENOTDIR = 54;

function isDirMode(mode: number): boolean {
  return (mode & S_IFMT) === S_IFDIR;
}

/** Emscripten ErrnoError 흉내 — 구버전은 message가 비어 있을 수 있어 errno를 함께 싣는다. */
function fsError(errno: number, message: string): Error {
  return Object.assign(new Error(message), { errno });
}

/** IDBFS FILE_DATA 엔트리 — loadLocalEntry/storeLocalEntry가 주고받는 값 */
interface FsEntry {
  mode: number;
  timestamp: number;
  contents?: unknown;
}

/**
 * MEMFS 노드 테이블(node_ops) 중 우리가 쓰는 부분.
 *
 * - `lookup`: FS.lookupNode(library_fs.js:225-244)는 nameTable 캐시를 먼저 뒤지고
 *   **미스일 때만** FS.lookup(:614-616) → `parent.node_ops.lookup`을 부르며, 그
 *   반환값을 그대로 노드로 채택한다. MEMFS 원본(library_memfs.js:183-185)은 무조건
 *   ENOENT를 throw하므로 이 호출은 "지금 없는 이름을 누군가 찾는다"는 이벤트와 같다.
 * - `mknod`: FS.mkdir/FS.mknod(library_fs.js:618-648)가 거치는 유일한 생성 경로다
 *   (`ops_table.dir.node`에는 `mkdir` 키 자체가 없다 — library_memfs.js:20-32).
 */
interface NodeOps {
  lookup(parent: FsNode, name: string): FsNode;
  mknod(parent: FsNode, name: string, mode: number, dev: number): FsNode;
  /**
   * `setattr`: FS.truncate와 FS.open(O_TRUNC)이 거치는 **유일한** 경로다
   * (library_fs.js:1042-1045 → FS.truncate → node_ops.setattr). §1-5의 실측 증상
   * (심자마자 잘림)이 여기를 지나가므로, 잘림 파수꾼도 이 함수를 감싼다.
   */
  setattr?(node: FsNode, attr: SetattrArgs): void;
  [key: string]: unknown;
}

/** node_ops.setattr의 인자 — size가 있을 때만 잘림이다(chmod/utime은 size undefined) */
interface SetattrArgs {
  mode?: number;
  timestamp?: number;
  size?: number;
}

/**
 * MEMFS stream_ops(library_memfs.js ops_table.file.stream) 중 우리가 쓰는 부분.
 * FS.open은 이 테이블을 **open 시점에** FSStream으로 캡처하고(FS.createStream),
 * 이후 fread는 그 캡처본만 통과한다 — 파수꾼의 read 관측이 걸리는 지점이다.
 */
interface StreamOps {
  read(
    stream: { node: FsNode },
    buffer: Uint8Array,
    offset: number,
    length: number,
    position: number,
  ): number;
  [key: string]: unknown;
}

/** MEMFS.ops_table 흉내 — 노드마다가 아니라 **테이블 하나를 공유**한다 */
interface MemfsOps {
  dir: NodeOps;
  file: NodeOps;
  /** 파일 노드가 공유하는 stream_ops 테이블 (ops_table.file.stream) */
  fileStream: StreamOps;
}

/** MEMFS FSNode 흉내 (library_fs.js:40-53 + library_memfs.js:68-94) */
interface FsNode {
  name: string;
  /** 마운트 루트는 자기 자신을 가리킨다 (library_fs.js:41) */
  parent: FsNode;
  mode: number;
  timestamp: number;
  /** 디렉터리면 자식 노드 map, 파일이면 바이트 배열(생성 직후에는 null) */
  contents: Record<string, FsNode> | unknown;
  node_ops: NodeOps;
  /** 파일 노드에만 붙는다 (MEMFS.createNode: 디렉터리는 stream_ops가 없다) */
  stream_ops?: StreamOps;
  /** FS.mount가 마운트 루트에 걸어두는 mount 객체 */
  mount?: MountMock;
}

interface MountMock {
  mountpoint: string;
  type: IdbfsMock;
  root?: FsNode;
  idbPersistState?: unknown;
}

interface IdbfsMock {
  syncfs(mount: MountMock, populate: boolean, cb: (err: Error | null) => void): void;
  getLocalSet(
    mount: MountMock,
    cb: (err: Error | null, result?: { entries: Record<string, { timestamp: number }> }) => void,
  ): void;
  loadLocalEntry(path: string, cb: (err: Error | null, entry?: FsEntry) => void): void;
  storeLocalEntry(path: string, entry: FsEntry, cb: (err: Error | null) => void): void;
  removeLocalEntry(path: string, cb: (err: Error | null) => void): void;
  /**
   * 원본 syncfs가 하는 일을 주입하는 훅. 어댑터는 포획 시점에 원본 함수 참조를
   * origSyncfs로 들고 가므로(`b.idbfs.syncfs`를 나중에 갈아끼워도 소용없다),
   * "엔진 populate 도중의 FS.mkdirTree" 같은 사건은 이 훅으로만 재현할 수 있다.
   */
  onOrigSyncfs?: (populate: boolean) => void;
}

/** 트리에서 파생되는 "경로 → 엔트리" 뷰 (기존 단언이 쓰던 Map 표면만 제공한다) */
interface FsView {
  has(path: string): boolean;
  get(path: string): FsEntry | undefined;
  delete(path: string): boolean;
  keys(): string[];
  /** 새 단언용: 경로에 해당하는 노드 자체 */
  node(path: string): FsNode | null;
}

// ===========================================
// 노드 트리 (하니스의 단일 진실원)
// ===========================================

function childOf(node: FsNode, name: string): FsNode | null {
  if (!isDirMode(node.mode)) return null;
  const contents = node.contents as Record<string, FsNode>;
  if (!contents || !Object.prototype.hasOwnProperty.call(contents, name)) return null;
  return contents[name];
}

/** MEMFS.createNode(library_memfs.js:68-94) — 디렉터리에만 contents({})가 붙는다 */
function createNode(parent: FsNode | null, name: string, mode: number, ops: MemfsOps): FsNode {
  const dir = isDirMode(mode);
  const node: FsNode = {
    name,
    parent: null as unknown as FsNode,
    mode,
    timestamp: Date.now(),
    contents: dir ? {} : null,
    // ⚠️ 부모의 테이블이 아니라 **공유 테이블**을 붙인다. IdbFs.js:45가 새 노드마다
    //    mnt.node_ops를 다시 대입하는 이유가 바로 이것이다(자동 전파가 없다).
    node_ops: dir ? ops.dir : ops.file,
  };
  // MEMFS는 정규 파일에만 stream_ops를 붙인다. 이것도 **공유 테이블**이다.
  if (!dir) node.stream_ops = ops.fileStream;
  node.parent = parent || node;
  if (parent) (parent.contents as Record<string, FsNode>)[name] = node;
  return node;
}

/**
 * MEMFS ops_table(library_memfs.js:20-32) 흉내.
 * 한 번 만들면 그 파일시스템의 모든 디렉터리 노드가 **같은 객체**를 공유한다 —
 * 이 공유가 "node_ops를 in-place로 고치면 안 된다"는 제약의 근거다.
 */
function makeMemfsOps(): MemfsOps {
  const table: MemfsOps = {
    dir: {
      lookup(_parent: FsNode, name: string): FsNode {
        throw fsError(ERRNO_ENOENT, 'ENOENT: ' + name);
      },
      mknod(parent: FsNode, name: string, mode: number, _dev: number): FsNode {
        return createNode(parent, name, mode, table);
      },
    },
    // 파일 노드 테이블에는 lookup/mknod가 없다 (MEMFS ops_table.file.node).
    // setattr은 **있다** — 이것을 빼면 FS.open(O_TRUNC)이 표현되지 않아 잘림 파수꾼
    // (installTruncationSentinel)이 조용히 아무것도 감싸지 않고, 파수꾼 케이스가
    // 전부 가짜 통과한다.
    file: {
      setattr(node: FsNode, attr: SetattrArgs): void {
        if (typeof attr.mode === 'number') node.mode = attr.mode;
        if (typeof attr.timestamp === 'number') node.timestamp = attr.timestamp;
        if (typeof attr.size !== 'number') return;
        // ⚠️ **실제로 자른다.** 관측만 하고 바이트를 그대로 두면 "심은 값이 살아남았다"는
        //    단언이 잘림 부팅에서도 통과해버려 모델 판별(i-a vs i-b)이 무의미해진다.
        const cur = node.contents as ArrayLike<number> | null;
        const next = new Uint8Array(attr.size);
        if (cur && typeof cur.length === 'number') {
          const n = Math.min(cur.length, attr.size);
          for (let i = 0; i < n; i++) next[i] = cur[i];
        }
        node.contents = next;
      },
    } as unknown as NodeOps,
    // 파일 노드가 공유하는 stream_ops. 파수꾼이 이것을 in-place로 고치면 파일시스템
    // 전역이 오염된다(무회귀 계약 5) — 반드시 클론 후 노드에만 지정해야 한다.
    fileStream: {
      read(stream, buffer, offset, length, position): number {
        const c = stream.node.contents as ArrayLike<number> | null;
        const size = c && typeof c.length === 'number' ? c.length : 0;
        if (position >= size) return 0;
        const n = Math.min(size - position, length);
        for (let i = 0; i < n; i++) buffer[offset + i] = c![position + i];
        return n;
      },
    },
  };
  return table;
}

/**
 * O_TRUNC 흉내 (fopen(path,"wb") / PlayerPrefs.DeleteAll 등).
 *
 * ⚠️ 하니스 계약: 잘림은 **반드시 `node_ops.setattr(node, {size:0})`을 경유**한다.
 *    `node.contents`를 직접 비우면 파수꾼이 관측할 기회 자체가 사라져, 파수꾼이
 *    통째로 빠져 있어도 테스트가 통과한다(가짜 통과).
 */
function truncateNode(node: FsNode): void {
  const setattr = node.node_ops.setattr;
  if (typeof setattr !== 'function') throw new Error('setattr 없는 노드는 자를 수 없다');
  setattr.call(node.node_ops, node, { size: 0 });
}

/**
 * fopen(path,"rb") + fread 흉내.
 *
 * ⚠️ 하니스 계약: 읽기는 **open 시점에 캡처한 `stream_ops.read`만** 경유한다
 *    (FS.open → FS.createStream이 stream_ops를 스트림에 싣는다). `node.contents`를
 *    직접 읽으면 파수꾼의 read 관측(plantSeenRead)이 발화하지 않는데도 "읽었다"는
 *    단언이 통과해 모델 i-b 판정이 가짜가 된다.
 */
function readFileViaStream(node: FsNode): Uint8Array {
  const sops = node.stream_ops; // ← open 시점 캡처
  const read = sops && typeof sops.read === 'function' ? sops.read : null;
  if (!sops || !read) return new Uint8Array(0);
  const stream = { node };
  const out: number[] = [];
  const chunk = new Uint8Array(64);
  let pos = 0;
  for (;;) {
    const n = read.call(sops, stream, chunk, 0, chunk.length, pos);
    if (!n || n <= 0) break;
    for (let i = 0; i < n; i++) out.push(chunk[i]);
    pos += n;
    if (out.length > 1024 * 1024) break; // 폭주 방지 안전판
  }
  return new Uint8Array(out);
}

const decodeBytes = (v: unknown): string =>
  new TextDecoder().decode(new Uint8Array((v as ArrayLike<number>) || []));

/** 마운트 루트('/idbfs')를 기준으로 절대경로를 노드로 푼다 */
function resolveNode(root: FsNode, p: string): FsNode | null {
  if (p === MOUNT) return root;
  if (p.indexOf(MOUNT + '/') !== 0) return null;
  const parts = p.slice(MOUNT.length + 1).split('/');
  let cur: FsNode = root;
  for (let i = 0; i < parts.length; i++) {
    if (!parts[i]) return null;
    const next = childOf(cur, parts[i]);
    if (!next) return null;
    cur = next;
  }
  return cur;
}

function nodeToEntry(node: FsNode): FsEntry {
  const e: FsEntry = { mode: node.mode, timestamp: node.timestamp };
  // 실제 loadLocalEntry도 디렉터리에는 contents를 싣지 않는다
  if (!isDirMode(node.mode)) e.contents = node.contents;
  return e;
}

function collectPaths(node: FsNode, prefix: string, out: string[]): void {
  out.push(prefix);
  if (!isDirMode(node.mode)) return;
  const contents = (node.contents as Record<string, FsNode>) || {};
  for (const name of Object.keys(contents)) collectPaths(contents[name], prefix + '/' + name, out);
}

function makeFsView(root: FsNode): FsView {
  return {
    node: (p) => resolveNode(root, p),
    has: (p) => resolveNode(root, p) !== null,
    get: (p) => {
      const n = resolveNode(root, p);
      return n ? nodeToEntry(n) : undefined;
    },
    delete: (p) => {
      const n = resolveNode(root, p);
      if (!n || n === root) return false;
      const contents = n.parent.contents as Record<string, FsNode>;
      delete contents[n.name];
      return true;
    },
    keys: () => {
      const out: string[] = [];
      collectPaths(root, MOUNT, out);
      return out;
    },
  };
}

/**
 * Emscripten IDBFS 백엔드를 흉내 내는 in-memory 목(mock).
 * 트리(`root`)가 진실원이고 `entries`는 그 파생 뷰다.
 * `sharedOps`를 넘기면 여러 목이 같은 ops_table을 공유한다(전역 테이블 오염 검증용).
 */
function makeFs(
  initial: Record<string, FsEntry>,
  sharedOps?: MemfsOps,
): { idbfs: IdbfsMock; entries: FsView; root: FsNode; ops: MemfsOps } {
  const ops = sharedOps || makeMemfsOps();
  // FS.mount → MEMFS.mount는 name='/', parent=self인 마운트 루트를 만든다.
  // 이 노드가 곧 MOUNT('/idbfs')이고, 앱 디렉터리는 그 자식이다.
  const root = createNode(null, '/', DIR_MODE, ops);
  const entries = makeFsView(root);

  const idbfs: IdbfsMock = {
    syncfs(_mount, populate, cb) {
      const hook = idbfs.onOrigSyncfs;
      // 원본 populate가 MEMFS에 만드는 것들(FS.mkdirTree로 좌초 디렉터리 복원 등)
      if (typeof hook === 'function') hook(populate);
      cb(null);
    },
    getLocalSet(_mount, cb) {
      const out: Record<string, { timestamp: number }> = {};
      for (const p of entries.keys()) out[p] = { timestamp: entries.node(p)!.timestamp };
      cb(null, { entries: out });
    },
    loadLocalEntry(entryPath, cb) {
      const n = resolveNode(root, entryPath);
      if (!n) {
        cb(fsError(ERRNO_ENOENT, 'ENOENT: ' + entryPath));
        return;
      }
      cb(null, nodeToEntry(n));
    },
    storeLocalEntry(entryPath, entry, cb) {
      // Emscripten storeLocalEntry는 디렉터리 엔트리에만 mkdirTree를 부르고 파일에는
      // FS.writeFile을 부른다. 따라서 파일은 부모가 **존재하고 또 디렉터리여야** 쓰인다
      // — 존재만 보면 부모가 파일일 때 실제로는 ENOTDIR로 실패할 쓰기를 목이 성공으로
      // 처리해, "말이 안 되는 경로로 임포트 성공"이 테스트를 통과해버린다.
      const dir = isDirMode(entry.mode);
      // 디렉터리도 정규 파일도 아니면 Emscripten은 'node type not supported'로 **실패**한다
      // (library_idbfs.js storeLocalEntry의 마지막 else). 타입 비트가 빠진 mode(0o644 등)를
      // 성공으로 처리하면 "말이 안 되는 mode로 임포트 성공"이 테스트를 통과해버린다.
      if (!dir && (entry.mode & S_IFMT) !== S_IFREG) {
        cb(new Error('node type not supported'));
        return;
      }
      if (entryPath === MOUNT) {
        if (!dir) {
          cb(fsError(ERRNO_ENOTDIR, 'ENOTDIR: ' + MOUNT));
          return;
        }
        root.mode = entry.mode;
        root.timestamp = entry.timestamp;
        cb(null);
        return;
      }
      if (entryPath.indexOf(MOUNT + '/') !== 0) {
        cb(fsError(ERRNO_ENOENT, 'ENOENT: ' + entryPath));
        return;
      }
      const parts = entryPath.slice(MOUNT.length + 1).split('/');
      const parentPath = entryPath.slice(0, entryPath.lastIndexOf('/'));
      const name = parts[parts.length - 1];

      if (dir) {
        // mkdirTree: 중간 디렉터리도 반드시 parent.node_ops.mknod를 거쳐 생긴다
        let cur = root;
        for (let i = 0; i < parts.length; i++) {
          let next = childOf(cur, parts[i]);
          if (!next) {
            if (!isDirMode(cur.mode)) {
              cb(fsError(ERRNO_ENOTDIR, 'ENOTDIR: ' + parentPath));
              return;
            }
            next = cur.node_ops.mknod(cur, parts[i], DIR_MODE, 0);
          }
          cur = next;
        }
        if (!isDirMode(cur.mode)) {
          cb(fsError(ERRNO_ENOTDIR, 'ENOTDIR: ' + entryPath));
          return;
        }
        cur.mode = entry.mode;
        cur.timestamp = entry.timestamp;
        cb(null);
        return;
      }

      const parent = resolveNode(root, parentPath);
      if (!parent) {
        cb(fsError(ERRNO_ENOENT, 'ENOENT: ' + parentPath));
        return;
      }
      if (!isDirMode(parent.mode)) {
        cb(fsError(ERRNO_ENOTDIR, 'ENOTDIR: ' + parentPath));
        return;
      }
      // FS.writeFile → FS.open은 없는 이름에 대해 **lookup 미스를 한 번 거친 뒤**
      // mknod한다(library_fs.js:225-244 → :614). 즉 우리 자신의 쓰기도 lookup 훅을
      // 재진입시킬 수 있다 — 그 경로를 목에서도 그대로 재현해야 재진입 가드를 검증할 수 있다.
      let node = childOf(parent, name);
      if (!node) {
        try {
          node = parent.node_ops.lookup(parent, name) || null;
        } catch {
          node = null;
        }
      }
      if (!node) node = parent.node_ops.mknod(parent, name, entry.mode, 0);
      // FS.writeFile은 O_WRONLY|O_CREAT|O_TRUNC로 여는데, FS.open은 created 여부와
      // 무관하게 O_TRUNC면 FS.truncate → node_ops.setattr(size:0)을 부른다
      // (library_fs.js:1042-1045). 이 호출을 재현하지 않으면 "우리 자신의 쓰기가
      // 파수꾼을 오발화시키지 않는가"라는 순서 불변식이 검증되지 않는다.
      if (typeof node.node_ops.setattr === 'function') {
        node.node_ops.setattr(node, { size: 0 });
      }
      node.mode = entry.mode;
      node.timestamp = entry.timestamp;
      node.contents = entry.contents === undefined ? null : entry.contents;
      cb(null);
    },
    removeLocalEntry(entryPath, cb) {
      entries.delete(entryPath);
      cb(null);
    },
  };

  // 초기 상태 씨딩도 storeLocalEntry와 같은 규칙을 탄다(경로 규칙 이중 구현 방지)
  for (const [p, e] of Object.entries(initial)) {
    idbfs.storeLocalEntry(p, e, (err) => {
      if (err) throw err;
    });
  }
  return { idbfs, entries, root, ops };
}

/**
 * FS.lookupNode(library_fs.js:225-244) 흉내: nameTable(=parent.contents) 히트가 있으면
 * 그 노드를, 없으면 parent.node_ops.lookup의 **반환값을 그대로** 노드로 쓴다.
 * throw(ENOENT)는 "그런 파일 없음"이므로 null.
 */
function lookupNode(parent: FsNode, name: string): FsNode | null {
  const hit = childOf(parent, name);
  if (hit) return hit;
  try {
    return parent.node_ops.lookup(parent, name) || null;
  } catch {
    return null;
  }
}

/**
 * Unity 네이티브가 PlayerPrefs 파일의 "존재"를 판정하는 방식. 라운드 5 실측으로
 * 2021.3이 lookup 미스에 O_TRUNC를 들고 오는 것까지는 확인됐지만, 판정의 근거가
 * 디렉터리 유무인지 readdir 내용인지는 아직 가려지지 않았다(명세 결정 변경 1).
 * 이 유닛은 두 모델을 **둘 다** 돌려 각각의 기대 동작을 고정하는 판별 도구다.
 *
 *  - `readFirst`         : 기존 모델. 첫 접근이 read-open이라 lookup 미스가 곧 관측이다.
 *                          (fread 자체는 테스트가 `readFileViaStream`으로 따로 낸다 —
 *                           "심었지만 아직 읽지 않은" 상태를 표현해야 하기 때문이다.)
 *  - `dirCheckWriteFirst`: 모델 i-a. 디렉터리 유무만 보고 곧장 fopen(path,"wb"),
 *                          즉 O_WRONLY|O_CREAT|O_TRUNC — 심어둔 내용이 잘린다.
 *  - `readdirCheckThenRead`: 모델 i-b. readdir에 PlayerPrefs가 보이면 fopen(path,"rb")로
 *                          읽는다 — mkdir-plant가 유효해지는 모델.
 */
type BootModel = 'readFirst' | 'dirCheckWriteFirst' | 'readdirCheckThenRead';

/**
 * Unity 네이티브의 첫 PlayerPrefs 접근을 흉내 낸다.
 *  ① persistentDataPath 디렉터리 생성: FS.mkdir(:641-648) → FS.mknod(:618-634) →
 *     parent.node_ops.mknod. 이미 있으면(warm boot) 그대로 재사용한다.
 *  ② 위 부팅 모델에 따라 PlayerPrefs를 연다.
 * 반환값은 ②가 얻은 노드(없으면 null) — 훅이 심었는지를 그대로 관측한다.
 */
function simulateUnityBoot(b: BootResult, appDir: string, model: BootModel = 'readFirst'): FsNode | null {
  const hash = appDir.indexOf(MOUNT + '/') === 0 ? appDir.slice(MOUNT.length + 1) : appDir;
  let dir = childOf(b.root, hash);
  if (!dir) dir = b.root.node_ops.mknod(b.root, hash, DIR_MODE, 0);

  let effective = model;
  if (effective === 'readdirCheckThenRead') {
    // readdir = 디렉터리 contents 나열. 보이면 rb로 열어 읽는다.
    const visible = childOf(dir, 'PlayerPrefs');
    if (visible) {
      readFileViaStream(visible);
      return visible;
    }
    // 안 보이면 엔진은 새로 만든다 = 아래 write-first와 같은 경로다
    effective = 'dirCheckWriteFirst';
  }

  if (effective === 'dirCheckWriteFirst') {
    let node = lookupNode(dir, 'PlayerPrefs');
    if (!node) node = dir.node_ops.mknod(dir, 'PlayerPrefs', FILE_MODE, 0);
    truncateNode(node); // O_TRUNC — 반드시 setattr 경유
    return node;
  }

  return lookupNode(dir, 'PlayerPrefs');
}

interface BootOptions {
  storageSeed?: Record<string, string>;
  legacySource?: { readIdbfs: () => Promise<unknown> } | null;
  fsInit?: Record<string, FsEntry>;
  isProduction?: boolean;
  /** window.__AIT_PLAYERPREFS.legacyWatchMs (지연 임포트 감시 창) */
  legacyWatchMs?: number;
  /** 여러 부팅이 같은 MEMFS ops_table을 공유하게 한다 */
  nodeOps?: MemfsOps;
  /** 노드 그래프 없이 옛 `{ mount }` 모양만 주입한다 (훅 미설치 fail-open 검증용) */
  plainMountRoot?: boolean;
}

interface BootResult {
  // ait-playerprefs.js가 window에 동적으로 __AIT_PP 등을 붙이는 순수 JS 샌드박스라
  // 정적 타입을 걸기보다 any로 다루는 편이 테스트 의도를 더 잘 드러낸다.
  win: any;
  store: Map<string, string>;
  entries: FsView;
  mount: MountMock;
  idbfs: IdbfsMock;
  /** 마운트 루트 FSNode (= MOUNT) */
  root: FsNode;
  /** 이 파일시스템의 MEMFS 공유 ops_table */
  ops: MemfsOps;
  module: any;
  /** vm 샌드박스의 전역 객체 — 어댑터가 전역으로 참조하는 setTimeout 등을 갈아끼울 때 쓴다 */
  ctx: any;
}

/** ait-playerprefs.js를 node:vm 샌드박스에 로드하고 IDBFS 마운트까지 완료한 상태로 부팅한다. */
function boot(opts: BootOptions = {}): BootResult {
  const {
    storageSeed = {},
    legacySource = null,
    fsInit = {},
    isProduction = false,
    // 프로덕션 기본값은 20초라, 관측 없이 끝나는 케이스마다 그만큼의 실제 타이머가
    // 테스트 프로세스에 남는다. 여기서는 짧게 잡되(각 케이스의 wait보다는 충분히 길게)
    // 만료 자체를 보는 W1만 더 짧은 값을 명시한다.
    //
    // ⚠️ 이 값은 각 케이스의 누적 wait보다 **넉넉히** 커야 한다. 무장 이후 'deferred'를
    //    단언하는 케이스가 다수인데(C/C2/C3′/C4′/C5′/D3/W2/W4/W6/W7/W8/W11~W15),
    //    이 창이 먼저 만료되면 값이 'expired'로 바뀌어 로직과 무관하게 실패한다.
    //    1000ms일 때 부하가 걸린 머신(vitest 4개 동시 실행)에서 실제로 재현됐다 —
    //    케이스 최대 누적 wait(~400ms)의 7배 이상으로 잡는다.
    legacyWatchMs = 3000,
  } = opts;
  const store = new Map<string, string>(Object.entries(storageSeed));
  const ppConfig: any = { isProduction, enabled: true, legacyWatchMs };
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
    __AIT_PLAYERPREFS: ppConfig,
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

  const { idbfs, entries, root, ops } = makeFs(fsInit, opts.nodeOps);
  const mount: MountMock = { mountpoint: MOUNT, type: idbfs, root };
  root.mount = mount;
  const cfg: any = {};
  win.__AIT_PP.configure(cfg);
  const Module: any = {};
  for (const fn of cfg.preRun) fn(Module);
  // preRun이 심어둔 defineProperty 트랩(setter)을 통해 실제 마운트를 주입한다.
  // ⚠️ 주입되는 값은 FS.mount의 반환값, 즉 **마운트 루트 FSNode**다(name='/',
  //    parent=self, node_ops=MEMFS 공유 테이블, .mount=마운트 객체). 프로덕션
  //    onMountAssigned는 오늘 mountRoot.mount만 읽지만, 앱 디렉터리를 추측 대신
  //    관측하려면 이 노드 그래프가 있어야 한다.
  Module.__unityIdbfsMount = opts.plainMountRoot ? { mount } : root;
  return { win, store, entries, mount, idbfs, root, ops, module: Module, ctx };
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

// 이 레이어가 **쓰는** Storage 키는 정확히 2개다 (명세 결정 변경 2 / Storage 감사 원칙)
const MANIFEST_KEY = 'AITUnityFS_v1_manifest';
const STASH_KEY = 'AITUnityFS_v1_legacy'; // write-once — 레거시 덤프 보관 전용

function manifestFiles(raw: string | undefined | null): string[] | null {
  if (raw === undefined || raw === null) return null;
  return Object.keys(JSON.parse(JSON.parse(raw).inline).files);
}

/** 매니페스트의 inline 스냅샷 전체 (files/legacy/seq를 함께 봐야 하는 단언용) */
function manifestInline(raw: string | undefined | null): any {
  if (raw === undefined || raw === null) return null;
  return JSON.parse(JSON.parse(raw).inline);
}

/** 매니페스트의 이관 창 부기. 필드 자체가 없으면 undefined(= 미기록) */
function manifestLegacy(raw: string | undefined | null): any {
  const inline = manifestInline(raw);
  return inline ? inline.legacy : undefined;
}

/**
 * 매니페스트 문자열 빌더. 적대적/손상된 legacy 필드를 직접 심어야 형태 검증을
 * 고정할 수 있어서, 실제 push로 만든 매니페스트만으로는 부족하다.
 */
function makeManifest(opts: { files?: Record<string, unknown>; legacy?: unknown; seq?: number }): string {
  const seq = opts.seq === undefined ? 3 : opts.seq;
  const inline: Record<string, unknown> = { v: 1, seq, scope: 'playerprefs', files: opts.files || {} };
  if (opts.legacy !== undefined) inline.legacy = opts.legacy;
  return JSON.stringify({ v: 1, seq, ts: 1, inline: JSON.stringify(inline) });
}

/** 매니페스트 files 레코드 (serializeFiles의 {m,t,d} 표현) */
const fileRec = (text: string) => ({
  m: FILE_MODE,
  t: 1700000000000,
  d: Buffer.from(text).toString('base64'),
});
const dirRec = () => ({ m: DIR_MODE, t: 1 });

// 신규 스위트 공용: 옛 origin 덤프와 그것을 읽어주는 소스(조회 횟수를 셀 수 있다)
const LEGACY_TEXT = 'legacy-bytes';
const legacyDumpOf = (text: string = LEGACY_TEXT) => ({
  [OLD + '/PlayerPrefs']: {
    mode: FILE_MODE,
    timestamp: 5,
    contents: Array.from(Buffer.from(text)),
  },
});
const countingLegacySource = (counter: { n: number }) => ({
  readIdbfs: () => {
    counter.n++;
    return Promise.resolve(legacyDumpOf());
  },
});
const plainLegacySource = () => ({ readIdbfs: () => Promise.resolve(legacyDumpOf()) });

/** present + scoped 파일이 실린 매니페스트 (stash 경로의 전제 조건) */
const presentScopedManifest = (legacy?: unknown) =>
  makeManifest({
    files: { [APP]: dirRec(), [APP + '/PlayerPrefs']: fileRec('mine') },
    legacy,
  });
/** 위 매니페스트와 짝이 되는 로컬 상태 */
const presentScopedFs = (): Record<string, FsEntry> => ({
  ...bootedBefore,
  [APP + '/PlayerPrefs']: ppEntry('mine'),
});

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
    //    → 마운트포인트 오판(skip-mountpoint)이나 훅 미도달로 빠지지 않고, 덤프를 실제로
    //      읽어 후보 검증까지 통과해야 한다. (마운트포인트를 '/idbfs/<hash>'로 잘못 잡던
    //      이전 하니스에서는 항상 skip-mountpoint로 빠져 이 경로가 통과하지 못했다 —
    //      파일 헤더 설명 참조)
    //    심는 시점은 별개다: 심을 앱 디렉터리는 추측이 아니라 관측으로만 정해지므로
    //    부트 게이트에서는 후보를 park만 하고(deferred), Unity가 실제로 그 경로의
    //    PlayerPrefs를 여는 순간 옛 경로에서 현재 경로로 리매핑해 심는다.
    test('C) 옛 origin 해시의 레거시 덤프를 관측된 현재 앱 디렉터리로 리매핑해 심는다', async () => {
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

      // ① 게이트가 풀린 시점: 읽고 검증까지만 — 아직 어디에도 심지 않았고 창도 열려 있다
      const parked = b.win.__AIT_PP.status();
      expect(parked.legacyImport).toBe('deferred');
      expect(b.entries.has(APP + '/PlayerPrefs')).toBe(false);
      expect(b.store.has('AITUnityFS_v1_manifest')).toBe(false);

      // ② Unity가 PlayerPrefs를 연다(lookup 미스) → 그 parent가 곧 현재 앱 디렉터리다
      const node = simulateUnityBoot(b, APP);
      expect(node).not.toBeNull();
      await wait(100);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('imported');
      expect(s.legacyAppDir).toBe(APP);
      expect(s.legacyBytes).toBe('legacy-bytes'.length);
      expect(b.entries.has(APP + '/PlayerPrefs')).toBe(true);
      expect(b.entries.has(OLD + '/PlayerPrefs')).toBe(false); // 좌초 경로에는 쓰지 않는다
      expect(manifestFiles(b.store.get('AITUnityFS_v1_manifest'))).toContain(APP + '/PlayerPrefs');
      expect(s.mode).toBe('ait');
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
      // 창이 닫혀 있었다면 importThenPromote 자체가 안 불려 'none'으로 남는다.
      // 'deferred'는 "덤프를 읽고 후보를 park했다" = 창이 열려 있었다는 증거다.
      expect(s.legacyImport).toBe('deferred');
      expect(b.store.has('AITUnityFS_v1_manifest')).toBe(true); // 시드한 빈 매니페스트는 그대로
      expect(manifestFiles(b.store.get('AITUnityFS_v1_manifest'))).toHaveLength(0);

      // present-empty에서도 관측이 오면 그대로 심는다(창이 실제로 살아 있다)
      simulateUnityBoot(b, APP);
      await wait(100);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('imported');
      expect(manifestFiles(b.store.get('AITUnityFS_v1_manifest'))).toContain(APP + '/PlayerPrefs');
    });

    // ★ C3′) (A) cold boot: 앱 디렉터리가 아직 **없는** 최초 부팅.
    //    옛 규칙은 이 상태를 "심을 곳을 모른다"로 판정하고 포기했다(skip-unknown-appdir) —
    //    정작 이관이 가장 필요한 신규 origin에서 seam이 영영 발화하지 않는다는 뜻이었다.
    //    lookup 훅은 이 케이스를 **같은 부팅 안에서** 푼다: 디렉터리가 생기는 순간
    //    mknod로 우리 node_ops가 전파되고, 그 안의 PlayerPrefs lookup 미스가 앵커가 된다.
    test('C3′) 앱 디렉터리가 부팅 중 생겨도 같은 세션에서 정확한 경로에 심는다', async () => {
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
      const parked = b.win.__AIT_PP.status();
      expect(parked.legacyImport).toBe('deferred');
      expect([...b.entries.keys()].some((k) => /PlayerPrefs$/.test(k))).toBe(false);
      expect(b.store.has('AITUnityFS_v1_manifest')).toBe(false); // 아직 창은 열린 채

      simulateUnityBoot(b, APP); // 네이티브가 persistentDataPath를 만들고 PlayerPrefs를 연다
      await wait(100);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('imported');
      expect(s.legacyAppDir).toBe(APP);
      expect(b.entries.has(APP + '/PlayerPrefs')).toBe(true);
      expect(manifestFiles(b.store.get('AITUnityFS_v1_manifest'))).toContain(APP + '/PlayerPrefs');
      expect(s.mode).toBe('ait');
    });

    // C4′) 로컬에 앱 디렉터리 후보가 여러 개여도 상관없다 — 후보 목록으로 고르는 것이
    //      아니라 엔진이 건네준 parent 하나만 쓰기 때문이다. 옛 규칙은 여기서 포기했다.
    test('C4′) 앱 디렉터리 후보가 2개 이상이어도 관측된 쪽에만 심는다', async () => {
      const OTHER = '/idbfs/other_app_dir';
      const legacyDump = {
        [OLD + '/PlayerPrefs']: {
          mode: FILE_MODE,
          timestamp: 5,
          contents: Array.from(Buffer.from('abc')),
        },
      };
      const b = boot({
        fsInit: { [MOUNT]: dirEntry(), [APP]: dirEntry(), [OTHER]: dirEntry() },
        legacySource: { readIdbfs: () => Promise.resolve(legacyDump) },
      });
      await syncfs(b, true);
      await wait(300);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('deferred');

      simulateUnityBoot(b, APP);
      await wait(100);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('imported');
      expect(b.entries.has(APP + '/PlayerPrefs')).toBe(true);
      expect(b.entries.has(OTHER + '/PlayerPrefs')).toBe(false); // 관측되지 않은 후보는 무관
      expect(manifestFiles(b.store.get('AITUnityFS_v1_manifest'))).toContain(APP + '/PlayerPrefs');
      expect(s.mode).toBe('ait');
    });

    // C5′는 "유일 후보 = 현재 앱 디렉터리"라는 **추측을 코드에서 제거했다**는 사실을 고정한다.
    //
    // 재현 조건은 실사용에서 흔하다: 같은 origin에서 서빙 URL만 바뀌면(경로 버저닝
    // /app/v1 → /app/v2 등) 옛 URL의 /idbfs/<hashA>는 populate로 복원되지만 현재 빌드의
    // <hashB>는 아직 없다. Unity는 IDBFS를 /idbfs **자체**에 마운트하고(prejs/IdbFs.js의
    // FS.mkdir('/idbfs') → FS.mount(IDBFS, ..., '/idbfs')) <hash> 디렉터리는 네이티브가
    // main() 안에서 만들기 때문에, 부트 게이트 시점의 로컬 목록에는 stale 쪽만 보인다.
    // 옛 규칙(resolveAppDir)은 이 하나를 "유일 후보"로 채택해 좌초 경로에 심었고, 그것이
    // 매니페스트로 승격되면 다음 부팅부터 snapshotHasScopedFile이 true라 마이그레이션
    // 창이 **영구히** 닫혔다. (옛 디렉터리에 PlayerPrefs가 남아 있으면 skip-local-present로
    //  먼저 빠지므로, 위험한 조합은 "PlayerPrefs 없는 stale 디렉터리 1개"뿐이다.)
    //
    // 왜 이제 안전한가: resolveAppDir을 함수째 삭제했고, 심는 위치는 Emscripten이 직접
    // parent 노드를 건네주는 lookup 미스에서만 나온다(ait-playerprefs.js의 tryPlantAt).
    // FS.lookupNode는 nameTable 히트를 먼저 소비하고 미스일 때만 node_ops.lookup을
    // 부르며(library_fs.js:225-244, :614-616), MEMFS.node_ops.lookup은 무조건 ENOENT를
    // throw한다(library_memfs.js:183-185). 즉 훅의 발화 조건 자체가 "지금 그 이름이
    // 없다"라서 (a) stale 디렉터리는 Unity가 열지 않으니 영영 후보가 되지 않고
    // (b) 라이브 데이터를 덮어쓰는 것이 구조적으로 불가능하다.
    test("C5′) [회귀 고정] stale 디렉터리가 있어도 Unity가 실제로 연 경로에만 심는다", async () => {
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
        // 옛 규칙이라면 이 하나를 "유일 후보"로 채택해 좌초 경로에 심었을 상태다.
        fsInit: { [MOUNT]: dirEntry(), [STALE]: dirEntry(), [STALE + '/Sentry']: dirEntry() },
        legacySource: { readIdbfs: () => Promise.resolve(legacyDump) },
      });
      await syncfs(b, true);
      await wait(300);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('deferred');
      expect(b.entries.has(STALE + '/PlayerPrefs')).toBe(false); // 추측으로 좌초 경로에 심지 않는다
      expect(b.store.has('AITUnityFS_v1_manifest')).toBe(false); // 창이 닫히지 않는다

      // 현재 빌드의 앱 디렉터리는 <hashB>(=APP)다. Unity가 그쪽을 열면 그쪽에만 심긴다.
      simulateUnityBoot(b, APP);
      await wait(100);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('imported');
      expect(s.legacyAppDir).toBe(APP);
      expect(b.entries.has(APP + '/PlayerPrefs')).toBe(true);
      expect(b.entries.has(STALE + '/PlayerPrefs')).toBe(false); // 좌초 경로는 끝까지 무변경
      expect(manifestFiles(b.store.get('AITUnityFS_v1_manifest'))).toEqual([APP, APP + '/PlayerPrefs']);
      expect(s.mode).toBe('ait');
    });

    // 목이 Emscripten만큼 엄격한지도 함께 지킨다 — 부모가 파일이면 ENOTDIR이라
    // 아무것도 심히지 않고, 매니페스트도 남지 않아 창이 유지돼야 한다.
    // (파일 노드에는 node_ops.lookup 자체가 없어(MEMFS ops_table.file.node) 훅이 발화할
    //  일도 없다. 그래도 하니스의 ENOTDIR 엄격성은 유지한다 — 심는 경로가 조상 디렉터리를
    //  함께 만드는 방향으로 넓어지면 이 케이스가 다시 방어선이 된다.)
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

    // D2와 짝 — 같은 덤프 모양에서 크기만 상한 이내면 채택되고, 관측 시점에 그대로
    // 심긴다. 채택(deferred)과 심기(imported)가 2단계로 갈라진다는 것 자체도 함께 고정한다.
    test('D3) 상한 이내(1KB) → 채택된 뒤 관측 시점에 1024B가 심긴다', async () => {
      const legacyDump = {
        [OLD + '/PlayerPrefs']: { mode: FILE_MODE, timestamp: 0, contents: new Uint8Array(1024) },
      };
      const b = boot({ fsInit: bootedBefore, legacySource: { readIdbfs: () => Promise.resolve(legacyDump) } });
      await syncfs(b, true);
      await wait(300);
      const parked = b.win.__AIT_PP.status();
      expect(parked.legacyImport).toBe('deferred'); // 'empty'가 아니다 = 후보로 채택됐다
      expect(parked.lastError).toBeNull();
      expect(parked.legacyBytes).toBe(0); // 아직 아무것도 심지 않았다

      simulateUnityBoot(b, APP);
      await wait(100);
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

  /**
   * 앱 디렉터리 관측(lookup 훅) 자체의 계약.
   *
   * 이 훅은 Emscripten의 내부 구조(node_ops 테이블, FSNode 그래프)에 손을 대는 유일한
   * 부분이라, "무엇을 하는가"만큼 "무엇을 **하지 않는가**"가 중요하다. 아래 케이스는
   * 후자를 기계적으로 고정한다: 훅이 없는 부팅에서 엔진 객체를 건드리지 않는 것(W10),
   * 전역 공유 테이블을 오염시키지 않는 것(W7), 우리 자신의 FS 접근에 반응하지 않는 것
   * (W2/W4), 그리고 이미 있는 파일을 절대 건드리지 않는 것(W3).
   */
  describe('앱 디렉터리 관측 (lookup 훅)', () => {
    const legacyDump = () => ({
      [OLD + '/PlayerPrefs']: {
        mode: FILE_MODE,
        timestamp: 5,
        contents: Array.from(Buffer.from('legacy-bytes')),
      },
    });
    const legacySource = () => ({ readIdbfs: () => Promise.resolve(legacyDump()) });

    test('W1) 이 세션에서 PlayerPrefs를 한 번도 열지 않으면 창을 놓아준다 (expired)', async () => {
      const b = boot({ fsInit: bootedBefore, legacyWatchMs: 120, legacySource: legacySource() });
      await syncfs(b, true);
      await wait(400);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('expired');
      expect(s.mode).toBe('ait');
      // 매니페스트를 남기지 않아야 다음 부팅에서 다시 시도할 수 있다
      expect(b.store.has('AITUnityFS_v1_manifest')).toBe(false);

      // 만료 뒤에 늦게 접근이 와도 심지 않는다 (park한 페이로드는 이미 놓아줬다)
      simulateUnityBoot(b, APP);
      await wait(50);
      expect(b.entries.has(APP + '/PlayerPrefs')).toBe(false);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('expired');
    });

    // 우리가 심는 행위 자체가 FS.writeFile → FS.open → lookup 미스를 한 번 더 만든다.
    // inSelfFs 가드가 없으면 그 미스가 훅을 다시 발화시켜 재귀로 번진다.
    // 관측 가능한 증거: 안쪽 lookup이 **원본으로 위임**되어 ENOENT가 나야 목이 mknod를
    // 부르므로 mknod 카운트가 정확히 1이다 (안쪽이 또 심었다면 노드를 반환해 0이 된다).
    test('W2) 심는 도중의 훅 재진입이 재귀·중복 심기로 번지지 않는다', async () => {
      const ops = makeMemfsOps();
      const baseMknod = ops.dir.mknod;
      let ppMknod = 0;
      ops.dir.mknod = function (parent: FsNode, name: string, mode: number, dev: number): FsNode {
        if (name === 'PlayerPrefs') ppMknod++;
        return baseMknod.call(this, parent, name, mode, dev);
      };

      const b = boot({ nodeOps: ops, fsInit: bootedBefore, legacySource: legacySource() });
      await syncfs(b, true);
      await wait(300);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('deferred');

      // 설치된 클론의 lookup을 감싸 중첩 깊이를 잰다
      const ours = b.root.node_ops;
      expect(ours).not.toBe(ops.dir);
      const hookLookup = ours.lookup;
      let depth = 0;
      let maxDepth = 0;
      ours.lookup = function (parent: FsNode, name: string): FsNode {
        depth++;
        maxDepth = Math.max(maxDepth, depth);
        try {
          return hookLookup.apply(this, [parent, name] as never);
        } finally {
          depth--;
        }
      };

      simulateUnityBoot(b, APP);
      await wait(100);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('imported');
      expect(ppMknod).toBe(1); // PlayerPrefs 노드는 정확히 한 번만 생긴다
      expect(maxDepth).toBe(2); // 우리 쓰기가 만든 재진입 1단계에서 멈춘다(재귀 없음)
      expect(s.legacyBytes).toBe('legacy-bytes'.length); // 두 번 심었다면 값이 어긋난다
      expect([...b.entries.keys()].filter((k) => /\/PlayerPrefs$/.test(k))).toHaveLength(1);
    });

    test('W3) 심은 뒤 DeleteAll → 훅이 레거시를 되살리지 않는다', async () => {
      const b = boot({ fsInit: bootedBefore, legacySource: legacySource() });
      await syncfs(b, true);
      await wait(300);
      simulateUnityBoot(b, APP);
      await wait(100);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('imported');
      expect(manifestFiles(b.store.get('AITUnityFS_v1_manifest'))).toContain(APP + '/PlayerPrefs');

      b.entries.delete(APP + '/PlayerPrefs'); // PlayerPrefs.DeleteAll 모사
      // 게임이 삭제 직후 PlayerPrefs를 다시 연다 = 또 한 번의 lookup 미스
      expect(simulateUnityBoot(b, APP)).toBeNull(); // 재삽입 없음
      expect(b.entries.has(APP + '/PlayerPrefs')).toBe(false);

      await syncfs(b, false);
      await wait(200);
      // E와 같은 결론: DeleteAll은 빈 files push로 그대로 반영된다
      expect(manifestFiles(b.store.get('AITUnityFS_v1_manifest'))).toHaveLength(0);
    });

    // 무장 중에도 우리 레이어는 계속 FS를 훑는다(persist마다 collectScoped/logFirstPersist).
    // 그 트래픽이 관측으로 오인되면 아무도 열지 않은 경로에 심게 된다.
    // (심기 자체는 tryPlantAt의 즉시 disarm이 먼저 막고, inSelfFs가 그 뒤를 받친다 —
    //  실제 IDBFS는 목보다 훨씬 넓은 lookup 트래픽을 만들기 때문이다.)
    test('W4) 무장 상태에서 우리 자신의 FS 접근은 심기를 촉발하지 않는다', async () => {
      const b = boot({ fsInit: bootedBefore, legacySource: legacySource() });
      await syncfs(b, true);
      await wait(300);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('deferred');

      for (let i = 0; i < 3; i++) {
        await syncfs(b, false); // collectScoped/logFirstPersist가 FS를 훑는다
        await wait(20);
      }
      expect(b.win.__AIT_PP.status().legacyImport).toBe('deferred'); // 여전히 park 상태
      expect([...b.entries.keys()].some((k) => /PlayerPrefs$/.test(k))).toBe(false);
      expect(b.store.has('AITUnityFS_v1_manifest')).toBe(false);
    });

    // 셀프체크(node_ops.lookup/mknod) 실패는 오늘 거동으로의 후퇴여야 한다 — 절대
    // throw하지 않고, 레이어 전체를 vanilla로 떨구지도 않는다(REQUIRED_IDBFS_FNS에
    // lookup을 넣지 않는 이유와 같다).
    test('W5) 노드 그래프가 없으면 훅 없이 오늘 거동으로 후퇴한다 (fail-open)', async () => {
      const b = boot({ plainMountRoot: true, fsInit: bootedBefore, legacySource: legacySource() });
      await syncfs(b, true);
      await wait(300);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('skip-no-watcher');
      expect(s.mode).toBe('ait'); // 본 기능(영속화)은 그대로 살아 있다
      expect([...b.entries.keys()].some((k) => /PlayerPrefs$/.test(k))).toBe(false);
      expect(b.store.has('AITUnityFS_v1_manifest')).toBe(false); // 창 유지
    });

    // 앱 디렉터리는 항상 마운트 루트 바로 아래(depth 1)다. 하위 디렉터리에서 온
    // PlayerPrefs lookup은 우리 것이 아니므로 원본 ENOENT로 그대로 통과시킨다.
    test('W6) 하위 디렉터리(depth 2)의 PlayerPrefs lookup은 무시한다', async () => {
      const b = boot({ fsInit: bootedBefore, legacySource: legacySource() });
      await syncfs(b, true);
      await wait(300);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('deferred');

      // mknod 전파 확인도 겸한다: APP 아래 새 디렉터리에도 우리 클론이 붙는다
      const app = b.entries.node(APP)!;
      const sub = app.node_ops.mknod(app, 'sub', DIR_MODE, 0);
      expect(sub.node_ops).toBe(b.root.node_ops);

      expect(lookupNode(sub, 'PlayerPrefs')).toBeNull(); // 원본 ENOENT 그대로
      await wait(50);
      expect(b.entries.has(APP + '/sub/PlayerPrefs')).toBe(false);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('deferred'); // 여전히 park 상태
    });

    // MEMFS의 node_ops 테이블은 파일시스템 전역에서 공유된다(library_memfs.js:20-32).
    // in-place로 고치면 /tmp 등 우리와 무관한 디렉터리까지 훅이 걸린다.
    test('W7) 클론 설치가 전역 MEMFS node_ops 테이블을 오염시키지 않는다', async () => {
      const ops = makeMemfsOps();
      const origLookup = ops.dir.lookup;
      const origMknod = ops.dir.mknod;
      const other = makeFs({ [MOUNT]: dirEntry() }, ops); // 같은 테이블을 쓰는 다른 마운트

      const b = boot({ nodeOps: ops, fsInit: bootedBefore, legacySource: legacySource() });
      await syncfs(b, true);
      await wait(300);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('deferred'); // 훅이 실제로 설치됐다
      expect(b.root.node_ops).not.toBe(ops.dir); // 우리 마운트만 클론으로 교체된다

      expect(ops.dir.lookup).toBe(origLookup); // 전역 테이블은 참조까지 그대로
      expect(ops.dir.mknod).toBe(origMknod);
      expect(other.root.node_ops).toBe(ops.dir); // 다른 마운트 루트도 원본 그대로
      expect(() => other.root.node_ops.lookup(other.root, 'PlayerPrefs')).toThrow();
      expect(other.entries.has(MOUNT + '/PlayerPrefs')).toBe(false);
    });

    // warm boot: 앱 디렉터리가 populate로 이미 복원돼 있으면 mknod를 거치지 않으므로
    // 전파 기회가 없다. 설치 시점의 backfill이 이 구멍을 메운다.
    test('W8) populate로 이미 존재하는 앱 디렉터리에도 훅이 붙는다 (backfill)', async () => {
      const b = boot({ fsInit: bootedBefore, legacySource: legacySource() }); // APP이 이미 존재
      await syncfs(b, true);
      await wait(300);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('deferred');

      const app = b.entries.node(APP)!;
      expect(app.node_ops).toBe(b.root.node_ops); // backfill로 우리 클론이 붙었다

      // mknod 없이 lookup만으로 발화한다
      expect(lookupNode(app, 'PlayerPrefs')).not.toBeNull();
      await wait(100);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('imported');
      expect(s.legacyAppDir).toBe(APP);
      expect(b.entries.has(APP + '/PlayerPrefs')).toBe(true);
    });

    // ★ 순서 불변식: armAppDirWatch는 반드시 부트 게이트의 finish()보다 앞선다.
    //   Unity는 callback(null) 직후 곧바로 MEMFS를 읽으므로(populatePath 주석 참조),
    //   무장이 finish 뒤로 밀리면 첫 PlayerPrefs open이 훅보다 앞서 지나가 임포트가
    //   조용히 누락된다. 아래는 그 "직후"를 그대로 재현한다 — 순서가 뒤집히면 실패한다.
    test('W9) [순서 불변식] 감시자 무장이 부트 게이트 finish보다 앞선다', async () => {
      const b = boot({ fsInit: bootedBefore, legacySource: legacySource() });
      let opened: FsNode | null = null;
      await new Promise<void>((resolve) => {
        b.idbfs.syncfs(b.mount, true, () => {
          opened = simulateUnityBoot(b, APP); // Unity가 게이트 해제 직후 MEMFS를 읽는 순간
          resolve();
        });
      });
      expect(opened).not.toBeNull(); // 무장이 finish보다 늦었다면 여기서 null
      expect(b.win.__AIT_PP.status().legacyImport).toBe('imported');
      await wait(100);
      expect(manifestFiles(b.store.get('AITUnityFS_v1_manifest'))).toContain(APP + '/PlayerPrefs');
    });

    // ★ 블라스트 반경 0: 레거시 소스가 없는 오늘의 전 배포에서 엔진 객체를 **참조
    //   동일성까지** 그대로 둔다. 훅을 onMountAssigned가 아니라 armAppDirWatch에서
    //   지연 설치하는 유일한 이유이며, "훅 없는 다수 경로 회귀 0"의 기계적 증명이다.
    test('W10) 레거시 소스가 없으면 엔진 node_ops를 아예 건드리지 않는다', async () => {
      const ops = makeMemfsOps();
      const origLookup = ops.dir.lookup;
      const origMknod = ops.dir.mknod;
      const b = boot({ nodeOps: ops, fsInit: bootedBefore, legacySource: null });
      const before = b.root.node_ops;

      await syncfs(b, true);
      await wait(300);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('none');

      expect(b.root.node_ops).toBe(before);
      expect(b.root.node_ops).toBe(ops.dir);
      expect(ops.dir.lookup).toBe(origLookup);
      expect(ops.dir.mknod).toBe(origMknod);
      expect(b.entries.node(APP)!.node_ops).toBe(ops.dir); // backfill도 일어나지 않는다
    });

    // ★ 1회성 불변식: tryPlantAt은 성패와 무관하게 **진입 즉시** disarm한다.
    //   이것이 "심기는 세션당 한 번"의 1차 방어선이다(inSelfFs는 2차). W3은 push가
    //   먼저 완료돼 pushScoped 쪽 disarm이 대신 막아주므로 이 방어선을 검증하지 못한다
    //   — 승격 push는 setTimeout(0)이라, 그 사이의 DeleteAll+재접근이 진짜 재현 조건이다.
    test('W11) [1회성] 승격 push가 반영되기 전의 DeleteAll+재접근에도 다시 심지 않는다', async () => {
      const b = boot({ fsInit: bootedBefore, legacySource: legacySource() });
      await syncfs(b, true);
      await wait(300);
      expect(simulateUnityBoot(b, APP)).not.toBeNull();
      expect(b.win.__AIT_PP.status().legacyImport).toBe('imported');

      // 같은 tick — scheduleImmediatePush(setTimeout 0)가 아직 돌지 않았으므로
      // pushScoped 쪽 disarm은 아직 걸리지 않았다. 여기서 되살아나면 tryPlantAt의
      // 즉시 disarm이 사라진 것이다.
      b.entries.delete(APP + '/PlayerPrefs'); // PlayerPrefs.DeleteAll 모사
      expect(simulateUnityBoot(b, APP)).toBeNull();
      expect(b.entries.has(APP + '/PlayerPrefs')).toBe(false);

      await wait(200);
      expect(b.entries.has(APP + '/PlayerPrefs')).toBe(false);
      // 지운 상태가 그대로 반영된다 — 레거시가 매니페스트로 부활하지 않는다
      expect(b.store.has('AITUnityFS_v1_manifest')).toBe(false);
    });

    // ★ 라이브 데이터 보호: 이 설계 전체가 "덮어쓰지 않는다"에 걸려 있고, 그 마지막
    //   방어선이 tryPlantAt의 `parent.contents['PlayerPrefs']` 검사다.
    //   FS.lookupNode는 nameTable 히트를 먼저 소비하므로(library_fs.js:225-244) 실제
    //   Emscripten에서는 도달하지 않는 조건이지만, "도달하면 어떻게 되는가"가 곧 이
    //   설계의 안전성 주장이라 훅을 직접 호출해 계약으로 고정한다.
    test('W12) 대상 디렉터리에 PlayerPrefs가 이미 있으면 훅은 절대 덮어쓰지 않는다', async () => {
      const b = boot({ fsInit: bootedBefore, legacySource: legacySource() });
      await syncfs(b, true);
      await wait(300);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('deferred');

      // 라이브 게임 데이터를 훅을 거치지 않고 트리에 직접 만든다(= nameTable 히트 상태)
      const app = b.entries.node(APP)!;
      const live = app.node_ops.mknod(app, 'PlayerPrefs', FILE_MODE, 0);
      live.contents = new Uint8Array(Buffer.from('live-game-data'));

      // 훅을 직접 호출한다 — 심지 않고 원본으로 위임해야 하므로 ENOENT가 그대로 난다
      expect(() => b.root.node_ops.lookup(app, 'PlayerPrefs')).toThrow();
      await wait(100);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('deferred'); // 심지 않았다
      expect(s.legacyBytes).toBe(0);
      // 라이브 바이트가 레거시로 바뀌지 않았다
      expect(new TextDecoder().decode(live.contents as Uint8Array)).toBe('live-game-data');
    });

    // pickLegacyTarget ③: 정확일치가 없고 후보가 2개 이상이면 어느 origin 것인지 가릴
    // 근거가 없다 — 추측해서 심지 않는다. 이 창은 다음 부팅에도 열려 있어야 한다.
    test('W13) 후보 2개 + 정확일치 없음 → 심지 않고 skip-ambiguous로 물러난다', async () => {
      const dump = {
        '/idbfs/aaaaaaaaaaaa/PlayerPrefs': {
          mode: FILE_MODE,
          timestamp: 5,
          contents: Array.from(Buffer.from('from-a')),
        },
        '/idbfs/bbbbbbbbbbbb/PlayerPrefs': {
          mode: FILE_MODE,
          timestamp: 5,
          contents: Array.from(Buffer.from('from-b')),
        },
      };
      const b = boot({ fsInit: bootedBefore, legacySource: { readIdbfs: () => Promise.resolve(dump) } });
      await syncfs(b, true);
      await wait(300);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('deferred'); // 후보 자체는 채택됐다

      expect(simulateUnityBoot(b, APP)).toBeNull(); // 원본 ENOENT 그대로
      await wait(100);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('skip-ambiguous');
      expect(s.legacyBytes).toBe(0);
      expect([...b.entries.keys()].some((k) => /PlayerPrefs$/.test(k))).toBe(false);
      expect(b.store.has('AITUnityFS_v1_manifest')).toBe(false); // 창 유지
    });

    // pickLegacyTarget ①: 같은 해시(=같은 origin)의 후보가 있으면 그것이 정본이다.
    // 리매핑은 정확일치가 없을 때만 쓰는 후퇴 규칙이라, 순서가 뒤집히면 다른 origin의
    // 세이브가 현재 앱 디렉터리로 올라간다.
    test('W14) 정확일치 후보가 있으면 리매핑 후보보다 우선한다', async () => {
      const dump = {
        [APP + '/PlayerPrefs']: {
          mode: FILE_MODE,
          timestamp: 5,
          contents: Array.from(Buffer.from('exact')),
        },
        [OLD + '/PlayerPrefs']: {
          mode: FILE_MODE,
          timestamp: 5,
          contents: Array.from(Buffer.from('remapped-other-origin')),
        },
      };
      const b = boot({ fsInit: bootedBefore, legacySource: { readIdbfs: () => Promise.resolve(dump) } });
      await syncfs(b, true);
      await wait(300);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('deferred');

      simulateUnityBoot(b, APP);
      await wait(100);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('imported');
      expect(s.legacyBytes).toBe('exact'.length); // 리매핑 후보였다면 21B가 된다
      expect(new TextDecoder().decode(b.entries.get(APP + '/PlayerPrefs')!.contents as Uint8Array)).toBe(
        'exact',
      );
    });

    // 로컬에 PlayerPrefs가 이미 있으면 임포트는 시작조차 하지 않는다. 이 early-return이
    // 없으면 감시자가 무장하고 훅까지 설치돼, 이후 DeleteAll이 만드는 lookup 미스에서
    // 사용자가 방금 지운 값이 레거시로 되살아난다(pushScoped의 disarm 주석 참조).
    test('W15) 로컬에 PlayerPrefs가 있으면 skip-local-present로 물러나고 훅도 설치하지 않는다', async () => {
      const ops = makeMemfsOps();
      const origLookup = ops.dir.lookup;
      const b = boot({
        nodeOps: ops,
        fsInit: { ...bootedBefore, [APP + '/PlayerPrefs']: ppEntry('mine') },
        legacySource: legacySource(),
      });
      await syncfs(b, true);
      await wait(300);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('skip-local-present');
      expect(s.legacyBytes).toBe(0);
      expect(b.root.node_ops).toBe(ops.dir); // 엔진 객체 무접촉
      expect(ops.dir.lookup).toBe(origLookup);
      // 기존 로컬 데이터가 그대로 승격된다(레거시가 끼어들지 않는다)
      expect(new TextDecoder().decode(b.entries.get(APP + '/PlayerPrefs')!.contents as Uint8Array)).toBe(
        'mine',
      );
      expect(manifestFiles(b.store.get('AITUnityFS_v1_manifest'))).toContain(APP + '/PlayerPrefs');
    });

    // 심기 성공 뒤의 뒤처리(로그 / 승격 push 예약)에서 예외가 나면 ours.lookup의 catch가
    // 그것을 삼키고 원본 lookup으로 위임하는데, 그 원본은 ENOENT를 던진다 — 방금 심어
    // parent.contents와 nameTable에 올라간 파일에 "없음"을 통보하는 셈이다. 그러면 Unity의
    // FS.open이 이어 부르는 FS.mknod → FS.mayCreate가 이번엔 nameTable 히트로 EEXIST를
    // 던져(library_fs.js:618-634) fopen 자체가 실패한다. 예외가 새는 게 아니라 **삼킨
    // 결과가 FS 실제 상태와 어긋나서** 다음 FS 호출이 죽는 형태의 부트 사고다.
    test('W16) 심기 뒤처리에서 예외가 나도 심은 노드를 반드시 돌려준다', async () => {
      const b = boot({ fsInit: bootedBefore, legacySource: legacySource() });
      await syncfs(b, true);
      await wait(300);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('deferred');

      // scheduleImmediatePush의 setTimeout(fn, 0) **한 번만** 실패시킨다.
      // 어댑터는 setTimeout을 전역으로 참조하므로 샌드박스 전역을 갈아끼우면 된다.
      const realSetTimeout = b.ctx.setTimeout;
      let injected = false;
      b.ctx.setTimeout = (fn: () => void, ms: number) => {
        if (!injected && ms === 0) {
          injected = true;
          throw new Error('injected: 승격 push 예약 실패');
        }
        return realSetTimeout(fn, ms);
      };
      let node: FsNode | null;
      try {
        node = simulateUnityBoot(b, APP);
      } finally {
        b.ctx.setTimeout = realSetTimeout;
      }

      expect(injected, '주입이 실제로 발화해야 이 케이스가 의미를 갖는다').toBe(true);
      // 핵심: null(= 원본 ENOENT 위임)을 돌려주면 Unity의 다음 FS 호출이 EEXIST로 죽는다
      expect(node, '뒤처리 예외를 삼키더라도 lookup은 심은 노드를 돌려줘야 한다').not.toBeNull();
      expect(new TextDecoder().decode(node!.contents as Uint8Array)).toBe('legacy-bytes');
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('imported');
      expect(s.lastError, '뒤처리 실패는 삼키되 관측 가능해야 한다').toContain('레거시 심기 뒤처리');
    });

    // 플랫폼 덤프가 타입 비트 없는 mode(0o644 등)를 주면 IDBFS.storeLocalEntry가
    // 'node type not supported'로 실패한다. 그 실패는 심는 시점에야 드러나는데 tryPlantAt은
    // 진입 즉시 disarm하므로, 이 세션의 유일한 관측 기회를 태우고 진단은 'empty'("가져올
    // 내용 없음")라고 잘못 말하게 된다. 정규화 단계에서 걸러 무장 자체를 하지 않아야 한다.
    test('W17) 정규 파일이 아닌 mode의 후보는 무장 전에 걸러진다', async () => {
      const ops = makeMemfsOps();
      const dump = {
        [OLD + '/PlayerPrefs']: {
          mode: 0o644, // S_IFREG 비트가 없다
          timestamp: 5,
          contents: Array.from(Buffer.from('legacy-bytes')),
        },
      };
      const b = boot({
        nodeOps: ops,
        fsInit: bootedBefore,
        legacySource: { readIdbfs: () => Promise.resolve(dump) },
      });
      await syncfs(b, true);
      await wait(300);

      expect(b.win.__AIT_PP.status().legacyImport).toBe('empty');
      expect(b.root.node_ops, '무장하지 않았으므로 훅도 설치되지 않는다').toBe(ops.dir);
      // 매니페스트를 남기지 않아야 다음 부팅에서 제대로 된 덤프로 재시도할 수 있다
      expect(b.store.has('AITUnityFS_v1_manifest')).toBe(false);
      simulateUnityBoot(b, APP);
      await wait(50);
      expect(b.entries.has(APP + '/PlayerPrefs')).toBe(false);
    });

    // backfill 실패를 훅 설치 실패로 묶으면 (a) 엔진 객체는 이미 우리 클론으로 바뀐 상태인데
    // 진단은 'skip-no-watcher'(= 미설치)라 어긋나고, (b) 루프가 중단돼 뒤 순번인 앱
    // 디렉터리에 훅이 못 붙어 관측이 통째로 죽는다.
    test('W18) 한 디렉터리의 backfill 실패가 나머지 관측을 죽이지 않는다', async () => {
      const ops = makeMemfsOps();
      const b = boot({
        nodeOps: ops,
        // '/idbfs/poisoned'가 앱 디렉터리보다 **먼저** 순회되도록 앞에 둔다
        fsInit: { [MOUNT]: dirEntry(), '/idbfs/poisoned': dirEntry(), ...bootedBefore },
        legacySource: legacySource(),
      });
      const poisoned = b.entries.node('/idbfs/poisoned')!;
      Object.defineProperty(poisoned, 'node_ops', {
        get: () => ops.dir,
        set: () => {
          throw new Error('injected: node_ops 대입 거부');
        },
        configurable: true,
      });

      await syncfs(b, true);
      await wait(300);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('deferred');
      expect(b.root.node_ops, '훅 자체는 설치돼 있다').not.toBe(ops.dir);
      expect(b.entries.node(APP)!.node_ops, '뒤 순번 디렉터리도 backfill된다').toBe(b.root.node_ops);

      simulateUnityBoot(b, APP);
      await wait(100);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('imported');
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

  /**
   * mkdir-plant 앵커 (명세 수정 A).
   *
   * lookup 앵커는 "첫 접근이 read-open"을 전제하는데 2021.3에서 그 전제가 거짓임이
   * 실측됐다. 앱 디렉터리가 **막 생긴 직후**(= 비어 있는 것이 보장된 순간) 미리 심어
   * 두면 그 전제 없이도 이관이 성립할 수 있다. 아래는 발화 조건을 좁게 고정한다 —
   * 이 앵커는 엔진 내부 이벤트에 직접 올라타므로 오발화 반경이 그대로 위험이다.
   */
  describe('mkdir-plant 앵커 (수정 A)', () => {
    test("M1) 앱 디렉터리 mkdir 직후 심고 plantedBy='mkdir'로 기록한다", async () => {
      const b = boot({ fsInit: { [MOUNT]: dirEntry() }, legacySource: plainLegacySource() });
      await syncfs(b, true);
      await wait(300);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('deferred');

      // Unity 네이티브가 persistentDataPath를 만드는 순간 = mknod 한 번.
      // 이 시점에 PlayerPrefs lookup은 아직 한 번도 일어나지 않았다.
      const dir = b.root.node_ops.mknod(b.root, APP.slice(MOUNT.length + 1), DIR_MODE, 0);
      const planted = childOf(dir, 'PlayerPrefs');
      expect(planted, 'lookup 미스 없이도 mkdir 앵커가 심어야 한다').not.toBeNull();
      expect(decodeBytes(planted!.contents)).toBe(LEGACY_TEXT);

      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('imported');
      expect(s.plantedBy).toBe('mkdir');
      expect(s.legacyAppDir).toBe(APP);
      expect(s.legacyBytes).toBe(LEGACY_TEXT.length);
      await wait(200);
      expect(manifestFiles(b.store.get(MANIFEST_KEY))).toContain(APP + '/PlayerPrefs');
    });

    // 앱 디렉터리는 항상 마운트 루트 바로 아래(depth 1)다. 그 아래에서 생기는
    // 디렉터리(/idbfs/<hash>/sub 등)는 우리 대상이 아니다 — 심으면 Unity가 절대
    // 읽지 않는 좌초 경로에 데이터를 만들고 창까지 소모한다.
    test('M2) depth-2 mkdir에서는 발화하지 않고 창도 소모하지 않는다', async () => {
      const b = boot({ fsInit: bootedBefore, legacySource: plainLegacySource() });
      await syncfs(b, true);
      await wait(300);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('deferred');

      const app = b.entries.node(APP)!;
      const sub = app.node_ops.mknod(app, 'sub', DIR_MODE, 0); // parent !== 마운트 루트
      expect(childOf(sub, 'PlayerPrefs')).toBeNull();
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('deferred'); // 창은 그대로 열려 있다
      expect(s.plantedBy).toBeNull();

      // 반대 증거: 같은 조건에서 depth-1이면 심는다(= 앵커 자체가 죽은 게 아니다)
      const d1 = b.root.node_ops.mknod(b.root, 'aaaabbbbcccc', DIR_MODE, 0);
      expect(childOf(d1, 'PlayerPrefs')).not.toBeNull();
      expect(b.win.__AIT_PP.status().plantedBy).toBe('mkdir');
    });

    // 마운트 루트 바로 아래라도 디렉터리가 아니면 대상이 아니다. SCOPE_DIR_RE는
    // '/idbfs/<한 세그먼트>'라 파일 이름도 그대로 통과하므로, 실제 방어선은
    // isDirMode(mode) 하나뿐이다. (마운트 루트 자신은 FS.mount가 직접 만들어
    //  mknod를 거치지 않으므로 재현 대상이 아니다.)
    test('M3) 마운트 루트 아래라도 디렉터리가 아닌 mknod에서는 발화하지 않는다', async () => {
      const b = boot({ fsInit: { [MOUNT]: dirEntry() }, legacySource: plainLegacySource() });
      await syncfs(b, true);
      await wait(300);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('deferred');

      const f = b.root.node_ops.mknod(b.root, 'save.dat', FILE_MODE, 0);
      expect(isDirMode(f.mode)).toBe(false);
      expect([...b.entries.keys()].some((k) => /PlayerPrefs$/.test(k))).toBe(false);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('deferred');
      expect(s.plantedBy).toBeNull();
      expect(b.store.has(MANIFEST_KEY)).toBe(false); // 창 유지
    });

    // 늦은(게임 유발) syncfs(true) reconcile의 FS.mkdirTree는 좌초 디렉터리를 복원한다.
    // 그 mkdir을 "Unity가 persistentDataPath를 만들었다"로 오인하면 아무도 읽지 않을
    // 경로에 심고 관측 기회를 태운다 — inEnginePopulate 플래그가 그 경로를 끊는다.
    test('M4) 엔진 populate 구간의 mkdir(좌초 디렉터리 복원)은 앵커를 오발화시키지 않는다', async () => {
      const b = boot({ fsInit: { [MOUNT]: dirEntry() }, legacySource: plainLegacySource() });
      await syncfs(b, true);
      await wait(300);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('deferred');

      b.idbfs.onOrigSyncfs = (populate) => {
        if (populate && !childOf(b.root, 'stranded1234')) {
          b.root.node_ops.mknod(b.root, 'stranded1234', DIR_MODE, 0);
        }
      };
      await syncfs(b, true); // 늦은 reconcile
      await wait(200);

      const stranded = childOf(b.root, 'stranded1234');
      expect(stranded, '테스트 전제: 복원이 실제로 일어났다').not.toBeNull();
      expect(childOf(stranded!, 'PlayerPrefs'), '엔진 복원 구간에서는 심지 않는다').toBeNull();
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('deferred');
      expect(s.plantedBy).toBeNull();

      // 반대 증거: 같은 mkdir이라도 populate 밖이면 심는다(= 플래그가 창을 죽인 게 아니다)
      b.idbfs.onOrigSyncfs = undefined;
      const live = b.root.node_ops.mknod(b.root, 'livehash1234', DIR_MODE, 0);
      expect(childOf(live, 'PlayerPrefs')).not.toBeNull();
      expect(b.win.__AIT_PP.status().plantedBy).toBe('mkdir');
    });
  });

  /**
   * 모델 판별 (명세 결정 변경 1).
   *
   * 2021.3의 "PlayerPrefs 존재 판정"이 디렉터리 유무(i-a)인지 readdir 내용(i-b)인지는
   * 아직 가려지지 않았다. 아래 두 케이스는 **각 모델에서 무엇이 일어나야 하는가**를
   * 고정한다 — 라운드 7의 E2E 관측이 어느 쪽으로 나오든 유닛은 그 해석의 기준이 된다.
   */
  describe('부팅 모델 판별 (i-a / i-b)', () => {
    test('MD1) 모델 i-b(readdir → rb) → 심은 값이 살아남고 plantSeenRead가 선다', async () => {
      const b = boot({ fsInit: { [MOUNT]: dirEntry() }, legacySource: plainLegacySource() });
      await syncfs(b, true);
      await wait(300);

      const node = simulateUnityBoot(b, APP, 'readdirCheckThenRead');
      expect(node).not.toBeNull();
      expect(decodeBytes(node!.contents), '값이 생존한다 = 이관 성립').toBe(LEGACY_TEXT);

      const s = b.win.__AIT_PP.status();
      expect(s.plantedBy).toBe('mkdir');
      expect(s.plantSeenRead).toBe(true);
      expect(s.legacyImport).toBe('imported');
      expect(s.truncatedAtMs).toBeNull();

      await wait(200); // 승격 push — 읽기 관측 뒤라 창 부기가 실린다
      expect(manifestFiles(b.store.get(MANIFEST_KEY))).toContain(APP + '/PlayerPrefs');
      expect(manifestLegacy(b.store.get(MANIFEST_KEY))).toMatchObject({
        checked: true,
        result: 'imported',
      });
    });

    // ⚠️ 이 케이스의 기대 동작은 "이관 실패"다. 모델 i-a가 확정되면 mkdir-plant는
    //    §1-5를 해소하지 못한다는 뜻이고, 그때 지켜야 할 것은 이관이 아니라
    //    **창을 닫지 않는 것**(→ 다음 부팅 stash로 보존)이다.
    test('MD2) 모델 i-a(곧장 wb=O_TRUNC) → skip-truncated + 승격 push 억제 + 창 유지', async () => {
      const b = boot({ fsInit: { [MOUNT]: dirEntry() }, legacySource: plainLegacySource() });
      await syncfs(b, true);
      await wait(300);

      const node = simulateUnityBoot(b, APP, 'dirCheckWriteFirst');
      expect(node).not.toBeNull();
      expect((node!.contents as Uint8Array).length, '실제로 잘렸다').toBe(0);

      const s = b.win.__AIT_PP.status();
      expect(s.plantedBy).toBe('mkdir');
      expect(s.plantSeenRead).toBe(false);
      expect(s.legacyImport).toBe('skip-truncated');
      expect(typeof s.truncatedAtMs).toBe('number');

      await wait(200);
      expect(b.store.has(MANIFEST_KEY), '잘린 0바이트를 정본으로 올리지 않는다').toBe(false);
      expect(b.win.__AIT_PP.status().legacyChecked, '창은 열린 채 남는다').toBeNull();
    });
  });

  /** 잘림 파수꾼 (명세 수정 C) */
  describe('잘림 파수꾼 (수정 C)', () => {
    test('S1) 심기 자신의 O_TRUNC 쓰기는 파수꾼을 발화시키지 않는다 (순서 불변식)', async () => {
      const b = boot({ fsInit: bootedBefore, legacySource: plainLegacySource() });
      await syncfs(b, true);
      await wait(300);

      // applyLegacyFiles는 FS.writeFile(= O_TRUNC) 경유라 setattr(size:0)을 반드시
      // 한 번 부른다. 파수꾼이 그보다 **먼저** 설치되면 심자마자 skip-truncated가 된다.
      const node = simulateUnityBoot(b, APP)!;
      expect(node).not.toBeNull();
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('imported');
      expect(s.truncatedAtMs).toBeNull();
      expect(decodeBytes(node.contents)).toBe(LEGACY_TEXT);

      // 위 단언이 "파수꾼 부재"로 통과한 것이 아님을 같은 케이스에서 증명한다
      expect(node.node_ops, '파수꾼이 실제로 설치돼 있다').not.toBe(b.ops.file);
      truncateNode(node);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('skip-truncated');
    });

    // read 없이 게임이 곧바로 DeleteAll/Save하면 오탐이지만, 읽은 뒤의 잘림은
    // 정상적인 게임 쓰기다 — 여기서 창을 닫지 않으면 이미 이관된 데이터를 두고
    // 매 부팅 레거시 소스를 다시 훑게 된다.
    test('S2) Unity가 읽은 뒤의 잘림은 파수꾼을 발화시키지 않는다', async () => {
      const b = boot({ fsInit: bootedBefore, legacySource: plainLegacySource() });
      await syncfs(b, true);
      await wait(300);

      const node = simulateUnityBoot(b, APP)!;
      expect(decodeBytes(readFileViaStream(node))).toBe(LEGACY_TEXT);
      expect(b.win.__AIT_PP.status().plantSeenRead).toBe(true);

      truncateNode(node); // 게임의 DeleteAll/Save
      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport).toBe('imported');
      expect(s.truncatedAtMs).toBeNull();
    });

    test('S3) 파수꾼은 공유 MEMFS 테이블을 오염시키지 않는다 (무회귀 계약 5)', async () => {
      const ops = makeMemfsOps();
      const origSetattr = ops.file.setattr;
      const origRead = ops.fileStream.read;
      const b = boot({ nodeOps: ops, fsInit: bootedBefore, legacySource: plainLegacySource() });
      await syncfs(b, true);
      await wait(300);

      const node = simulateUnityBoot(b, APP)!;
      expect(node.node_ops).not.toBe(ops.file); // 노드에만 클론이 붙는다
      expect(node.stream_ops).not.toBe(ops.fileStream);
      expect(ops.file.setattr).toBe(origSetattr); // 전역 테이블은 참조까지 그대로
      expect(ops.fileStream.read).toBe(origRead);

      // 무관한 파일은 원본 테이블 그대로 → 그 파일의 잘림은 관측되지 않는다
      const other = b.entries.node(APP)!.node_ops.mknod(b.entries.node(APP)!, 'other.dat', FILE_MODE, 0);
      expect(other.node_ops).toBe(ops.file);
      expect(other.stream_ops).toBe(ops.fileStream);
      truncateNode(other);
      expect(b.win.__AIT_PP.status().legacyImport).toBe('imported');
    });

    // 돌연변이 검증: setattr 래퍼의 `!inSelfFs` 조건을 지우면(즉 조건이
    // `attr.size===0 && !plantSeenRead && legacyImport!=='skip-truncated'`만 남으면)
    // 아래 단언이 깨진다 — collectScoped(→loadEntrySync)가 심어진 노드를 다시 읽는
    // 구간은 어댑터 자신의 enterSelfFs() 안이라, 그 안에서 발생한 setattr(size:0)은
    // (실제 엔진 write가 아니라) overlayScoped류 우리 자신의 재기록으로 취급돼야
    // 하기 때문이다. Emscripten의 실제 loadLocalEntry는 setattr을 부르지 않지만,
    // "우리 구간 안에서 setattr이 불리면 무조건 면제돼야 한다"는 것이 이 가드의
    // 안전성 주장 그 자체이므로 C7(로컬 수집 훅 재정의)과 같은 방식으로 하니스가
    // loadLocalEntry에 개입해 그 경계 조건을 직접 고정한다.
    test('S4) 우리 자신의 selfFs 구간(collectScoped) 안에서 발생한 setattr은 파수꾼을 발화시키지 않는다 (재진입 방어)', async () => {
      const b = boot({ fsInit: bootedBefore, legacySource: plainLegacySource() });
      await syncfs(b, true);
      await wait(300);

      const node = simulateUnityBoot(b, APP)!; // 심기 성공 — 파수꾼 설치 완료, 아직 읽지 않음
      expect(node).not.toBeNull();
      expect(b.win.__AIT_PP.status().legacyImport).toBe('imported');
      expect(b.win.__AIT_PP.status().plantSeenRead, '읽기 전이라 !inSelfFs가 유일한 방어선이다').toBe(false);

      const targetPath = APP + '/PlayerPrefs';
      const origLoad = b.idbfs.loadLocalEntry;
      b.idbfs.loadLocalEntry = (p: string, cb: any) => {
        if (p === targetPath) truncateNode(node); // enterSelfFs() 구간 안에서 O_TRUNC 재현
        return origLoad(p, cb);
      };
      try {
        await wait(300); // scheduleImmediatePush → pushScoped → collectScoped가 위 훅을 통과
      } finally {
        b.idbfs.loadLocalEntry = origLoad;
      }

      expect(b.win.__AIT_PP.status().legacyImport, '우리 자신의 재진입은 잘림으로 오판되면 안 된다').toBe(
        'imported',
      );
    });
  });

  /**
   * 이관 창 부기 (명세 수정 B).
   *
   * 이 필드 하나가 "레거시 소스를 다시 훑을 것인가"를 영구히 결정하므로, 잘못 쓰면
   * 이관이 영영 실패하고(위양성) 안 쓰면 매 부팅 비용을 낸다(위음성). 아래는 기록
   * 조건과 읽기 조건을 양방향으로 못박는다.
   */
  describe('이관 창 부기 (legacyChecked, 수정 B)', () => {
    test('K1) legacy 필드가 없는 매니페스트는 창을 닫지 않는다 (grandfather)', async () => {
      const counter = { n: 0 };
      const b = boot({
        storageSeed: { [MANIFEST_KEY]: presentScopedManifest() },
        fsInit: presentScopedFs(),
        legacySource: countingLegacySource(counter),
      });
      await syncfs(b, true);
      await wait(400);
      // 이 레이어가 나가기 전에 쓰인 매니페스트에는 이 필드가 없다 — 부재를
      // "이관 완료"로 읽으면 오늘의 전 사용자가 창을 잃는다.
      expect(counter.n, '창이 열려 있으므로 레거시 소스를 다시 훑는다').toBe(1);
      expect(b.win.__AIT_PP.status().legacyChecked).toMatchObject({ checked: true, result: 'stashed' });
    });

    test('K2) 형태가 깨진 legacy 필드는 부재로 취급한다', async () => {
      // 적대적/손상된 매니페스트가 마이그레이션 창을 영구히 닫지 못하게 하는 방어선
      const malformed: unknown[] = [
        true,
        'imported',
        { checked: false, result: 'imported' },
        { checked: true, result: 123 },
        { checked: true },
      ];
      for (const legacy of malformed) {
        const counter = { n: 0 };
        const b = boot({
          storageSeed: { [MANIFEST_KEY]: presentScopedManifest(legacy) },
          fsInit: presentScopedFs(),
          legacySource: countingLegacySource(counter),
        });
        await syncfs(b, true);
        await wait(400);
        expect(counter.n, JSON.stringify(legacy) + '는 부재로 취급돼야 한다').toBe(1);
      }
    }, 20000);

    test('K3) 레거시 소스가 없는 부팅은 창 부기를 만들지도 싣지도 않는다 (무회귀 계약 1)', async () => {
      const b = boot({
        storageSeed: { [MANIFEST_KEY]: presentScopedManifest() },
        fsInit: presentScopedFs(),
        legacySource: null,
      });
      await syncfs(b, true);
      await wait(300);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyChecked).toBeNull();
      expect(s.legacyImport).toBe('none');
      expect(s.legacyStashState).toBeNull();
      expect(b.store.has(STASH_KEY)).toBe(false);

      // 게임이 저장하면 push는 일어나되 페이로드는 오늘과 완전히 같아야 한다
      b.entries.node(APP + '/PlayerPrefs')!.contents = new Uint8Array(Buffer.from('changed'));
      await syncfs(b, false);
      await wait(200);
      const raw = b.store.get(MANIFEST_KEY)!;
      expect(manifestFiles(raw)).toContain(APP + '/PlayerPrefs');
      expect(manifestLegacy(raw)).toBeUndefined();
      expect(JSON.parse(raw).inline, 'legacy 필드 자체가 직렬화되지 않는다').not.toContain('legacy');
    });

    // 심기→즉시 push→(다음 프레임)잘림 레이스에서 창이 닫힌 채 0바이트가 정본이 되는
    // 위음성을 차단한다. 창 부기는 "Unity가 심은 바이트를 실제로 읽었다" 이후에만.
    test('K4) imported 부기는 Unity가 심은 바이트를 읽은 뒤의 push에서만 실린다', async () => {
      const b = boot({ fsInit: bootedBefore, legacySource: plainLegacySource() });
      await syncfs(b, true);
      await wait(300);

      const node = simulateUnityBoot(b, APP)!; // 심기만 — 아직 읽지 않았다
      await wait(200); // 승격 push 완료
      const first = b.store.get(MANIFEST_KEY);
      expect(manifestFiles(first)).toContain(APP + '/PlayerPrefs');
      expect(manifestLegacy(first), '읽기 전에는 창을 닫지 않는다').toBeUndefined();
      expect(b.win.__AIT_PP.status().legacyChecked).toBeNull();

      readFileViaStream(node); // Unity가 실제로 읽었다
      expect(b.win.__AIT_PP.status().plantSeenRead).toBe(true);
      // files도 함께 바꿔 push 자체는 해시 계약과 무관하게 성립시킨다(H1과 회귀 분리)
      node.contents = new Uint8Array(Buffer.from('game-save'));
      await syncfs(b, false); // persistPath 경유 push
      await wait(200);
      const second = b.store.get(MANIFEST_KEY);
      expect(manifestLegacy(second)).toMatchObject({ checked: true, result: 'imported' });
      expect(b.win.__AIT_PP.status().legacyChecked).toMatchObject({ result: 'imported' });
    });

    test('K5) 매니페스트에 실린 창 부기는 다음 부팅에서 창을 닫고 그대로 다시 실린다', async () => {
      // ① 부팅 A: 심고 읽어서 legacy.checked=imported를 매니페스트에 올린다
      const a = boot({ fsInit: bootedBefore, legacySource: plainLegacySource() });
      await syncfs(a, true);
      await wait(300);
      const node = simulateUnityBoot(a, APP)!;
      readFileViaStream(node);
      await syncfs(a, false);
      await wait(200);
      const seeded = a.store.get(MANIFEST_KEY)!;
      expect(manifestLegacy(seeded)).toMatchObject({ checked: true, result: 'imported' });

      // ② 부팅 B: 같은 매니페스트 → 레거시 소스를 **아예 조회하지 않는다**
      const counter = { n: 0 };
      const b = boot({
        storageSeed: { [MANIFEST_KEY]: seeded },
        fsInit: { ...bootedBefore, [APP + '/PlayerPrefs']: ppEntry(LEGACY_TEXT) },
        legacySource: countingLegacySource(counter),
      });
      await syncfs(b, true);
      await wait(300);
      expect(counter.n).toBe(0);
      expect(b.win.__AIT_PP.status().legacyChecked).toMatchObject({ checked: true, result: 'imported' });
      expect(b.win.__AIT_PP.status().legacyImport).toBe('none'); // 조회 자체가 없었다

      // ③ 왕복 보존: 이후 push에도 그대로 다시 실린다
      b.entries.node(APP + '/PlayerPrefs')!.contents = new Uint8Array(Buffer.from('next'));
      await syncfs(b, false);
      await wait(200);
      expect(manifestLegacy(b.store.get(MANIFEST_KEY))).toMatchObject({
        checked: true,
        result: 'imported',
      });
    });

    // 이관이 끝난 뒤 DeleteAll로 files가 비어도 창은 닫힌 채여야 한다 —
    // 여기서 부기가 떨어지면 다음 부팅에 레거시가 되살아난다(부활 사고).
    test('K6) DeleteAll로 files가 비어도 창 부기는 매니페스트에 남는다', async () => {
      const b = boot({
        storageSeed: {
          [MANIFEST_KEY]: presentScopedManifest({ checked: true, result: 'imported', ts: 111 }),
        },
        fsInit: presentScopedFs(),
      });
      await syncfs(b, true);
      await wait(200);
      b.entries.delete(APP + '/PlayerPrefs'); // PlayerPrefs.DeleteAll 모사
      await syncfs(b, false);
      await wait(200);
      const raw = b.store.get(MANIFEST_KEY);
      expect(manifestFiles(raw)).toHaveLength(0);
      expect(manifestLegacy(raw)).toMatchObject({ checked: true, result: 'imported', ts: 111 });
    });

    // 늦은(게임 유발) syncfs(true) reconcile은 populatePath를 재진입시키지만,
    // snapshotPromise는 스크립트 로드 시 1회 메모이제이션이라 res.snapshot은 이번
    // 부팅의 stash **이전** 원본(legacy 필드 없음)이다. 여기서 부기를 무조건 덮어쓰면
    // 세션 중 닫은 창이 다시 열리고, 직후 push가 legacy 없는 매니페스트를 써서
    // 매 부팅 레거시 재조회+재stash가 무한 반복된다.
    test('K7) 늦은 syncfs(true) reconcile이 세션 중 세운 창 부기를 지우지 않는다', async () => {
      const counter = { n: 0 };
      const b = boot({
        storageSeed: { [MANIFEST_KEY]: presentScopedManifest() },
        fsInit: presentScopedFs(),
        legacySource: countingLegacySource(counter),
      });
      await syncfs(b, true);
      await wait(400);
      expect(b.win.__AIT_PP.status().legacyChecked).toMatchObject({
        checked: true,
        result: 'stashed',
      });
      expect(manifestLegacy(b.store.get(MANIFEST_KEY))).toMatchObject({ result: 'stashed' });

      await syncfs(b, true); // 늦은 reconcile (같은 세션, 같은 스냅샷)
      await wait(400);
      expect(b.win.__AIT_PP.status().legacyChecked, '세션 중 세운 부기가 살아남는다').toMatchObject({
        checked: true,
        result: 'stashed',
      });
      expect(counter.n, '창은 닫힌 채다 — 레거시 소스 재조회 없음').toBe(1);

      // 직후 게임 세이브가 legacy 없는 매니페스트를 덮어쓰지 않는다
      b.entries.node(APP + '/PlayerPrefs')!.contents = new Uint8Array(Buffer.from('game-save'));
      await syncfs(b, false);
      await wait(200);
      expect(manifestLegacy(b.store.get(MANIFEST_KEY))).toMatchObject({
        checked: true,
        result: 'stashed',
      });
    });

    // ★ 변경 감지 해시 계약: files가 그대로이고 legacy 부기만 새로 생긴 push가
    //   "변경 없음"으로 스킵되면 창이 영영 닫히지 않는다(= stash 성공했는데 다음
    //   부팅에서 또 stash를 시도하고, write-once라 영원히 'existing'만 반복).
    test('H1) files가 한 바이트도 안 바뀌어도 창 부기만 생기면 push가 일어난다', async () => {
      // 씨앗 매니페스트는 실제 push로 만든다 — files 표현이 완전히 일치해야
      // "files 무변경"이라는 전제가 성립한다.
      const seedFs = presentScopedFs();
      const b0 = boot({ fsInit: seedFs });
      await syncfs(b0, true);
      await wait(300);
      const seeded = b0.store.get(MANIFEST_KEY)!;
      expect(manifestLegacy(seeded)).toBeUndefined();

      const b = boot({
        storageSeed: { [MANIFEST_KEY]: seeded },
        fsInit: { ...seedFs },
        legacySource: plainLegacySource(),
      });
      await syncfs(b, true);
      await wait(400);
      const after = b.store.get(MANIFEST_KEY)!;
      expect(manifestInline(after).files, 'files는 한 바이트도 바뀌지 않았다').toEqual(
        manifestInline(seeded).files,
      );
      expect(manifestInline(after).seq, '해시에 legacy가 빠지면 push가 통째로 스킵된다').toBe(
        manifestInline(seeded).seq + 1,
      );
      expect(manifestLegacy(after)).toMatchObject({ checked: true, result: 'stashed' });
    });
  });

  /**
   * 레거시 stash (명세 수정 D).
   *
   * 이관 창은 열려 있는데 로컬에 이미 정본 PlayerPrefs가 있어 **심을 수 없는** 부팅.
   * 심으면 라이브 데이터를 덮으므로(무회귀 계약 3), 옛 origin 덤프를 별도 write-once
   * 키에 한 번 보관만 한다. 잘림으로 이관에 실패한 다음 부팅이 정확히 여기로 온다.
   */
  describe('레거시 stash (수정 D)', () => {
    test('T1) 로컬에 정본이 있어도 보관한다 (skip-local-present 관문 우회)', async () => {
      const b = boot({
        storageSeed: { [MANIFEST_KEY]: presentScopedManifest() },
        fsInit: presentScopedFs(),
        legacySource: plainLegacySource(),
      });
      await syncfs(b, true);
      await wait(400);

      const s = b.win.__AIT_PP.status();
      expect(s.legacyImport, 'tryLegacyImport를 재사용했다면 skip-local-present로 즉사한다').toBe('stashed');
      expect(s.legacyStashState).toBe('written');

      const stash = JSON.parse(b.store.get(STASH_KEY)!);
      // 키는 **레거시 원본 경로** 그대로 — 리매핑은 심을 때만 하는 일이다
      expect(Object.keys(stash.files)).toEqual([OLD + '/PlayerPrefs']);
      expect(Buffer.from(stash.files[OLD + '/PlayerPrefs'].d, 'base64').toString()).toBe(LEGACY_TEXT);

      // 라이브 데이터는 한 바이트도 건드리지 않는다
      expect(decodeBytes(b.entries.get(APP + '/PlayerPrefs')!.contents)).toBe('mine');
      expect(manifestLegacy(b.store.get(MANIFEST_KEY))).toMatchObject({
        checked: true,
        result: 'stashed',
      });
    });

    // W15의 대칭 케이스. stash 경로가 armAppDirWatch를 부르면 훅이 설치되고, 이후
    // DeleteAll이 만드는 lookup 미스에서 사용자가 방금 지운 값이 레거시로 되살아난다.
    test('T2) stash 경로는 엔진 node_ops를 아예 건드리지 않는다 (무회귀 계약 6)', async () => {
      const ops = makeMemfsOps();
      const origLookup = ops.dir.lookup;
      const origMknod = ops.dir.mknod;
      const b = boot({
        nodeOps: ops,
        storageSeed: { [MANIFEST_KEY]: presentScopedManifest() },
        fsInit: presentScopedFs(),
        legacySource: plainLegacySource(),
      });
      await syncfs(b, true);
      await wait(400);

      expect(b.win.__AIT_PP.status().legacyImport).toBe('stashed'); // 보관은 됐는데
      expect(b.root.node_ops).toBe(ops.dir); // 훅은 붙지 않았다
      expect(ops.dir.lookup).toBe(origLookup);
      expect(ops.dir.mknod).toBe(origMknod);
      expect(b.entries.node(APP)!.node_ops, 'backfill도 일어나지 않는다').toBe(ops.dir);

      // 감시자가 무장하지 않았으므로 DeleteAll 후 재접근이 레거시를 되살리지 않는다
      b.entries.delete(APP + '/PlayerPrefs');
      expect(simulateUnityBoot(b, APP)).toBeNull();
    });

    test('T3) STASH_KEY가 이미 있으면 setItem을 부르지 않는다 (write-once)', async () => {
      const existing = JSON.stringify({
        v: 1,
        ts: 1,
        files: { [OLD + '/PlayerPrefs']: { m: FILE_MODE, t: 5, d: 'b2xk' } },
      });
      const b = boot({
        storageSeed: { [MANIFEST_KEY]: presentScopedManifest(), [STASH_KEY]: existing },
        fsInit: presentScopedFs(),
        legacySource: plainLegacySource(),
      });
      const calls: string[] = [];
      const st = b.win.__AIT_PLAYERPREFS_STORAGE__;
      const origSet = st.setItem;
      st.setItem = (k: string, v: string) => {
        calls.push(k);
        return origSet(k, v);
      };

      await syncfs(b, true);
      await wait(400);
      const s = b.win.__AIT_PP.status();
      expect(s.legacyStashState).toBe('existing');
      expect(s.legacyImport).toBe('stashed'); // 창은 닫는다(이미 보관돼 있으므로)
      expect(calls, '먼저 보관된 덤프가 정본이다 — 덮지 않는다').not.toContain(STASH_KEY);
      expect(b.store.get(STASH_KEY)).toBe(existing);
      expect(manifestLegacy(b.store.get(MANIFEST_KEY))).toMatchObject({ result: 'stashed' });
    });

    // 읽기 실패/타임아웃/예산 미달/게이트 발화는 전부 **재시도군**이다 — 기록을
    // 남기면 읽지도 못한 덤프에 대해 창이 닫힌다.
    // (onGateFired는 LEGACY_GATE_RESERVE_MS 때문에 레거시 타이머가 항상 부트 게이트보다
    //  먼저 발화해 이 하니스에서는 구조적으로 도달할 수 없다 — 같은 종점을 공유하는
    //  onTimeout으로 대표해 고정한다.)
    test('T4) stash 읽기가 타임아웃하면 아무것도 기록하지 않는다', async () => {
      const b = boot({
        storageSeed: { [MANIFEST_KEY]: presentScopedManifest() },
        fsInit: presentScopedFs(),
        legacySource: { readIdbfs: () => new Promise(() => {}) },
      });
      await syncfs(b, true);
      await wait(1600);
      const s = b.win.__AIT_PP.status();
      expect(s.mode, '부팅은 막히지 않는다').toBe('ait');
      expect(s.legacyChecked).toBeNull(); // 창 유지
      expect(s.legacyStashState).toBeNull();
      expect(b.store.has(STASH_KEY)).toBe(false);
      expect(manifestLegacy(b.store.get(MANIFEST_KEY))).toBeUndefined();
    });

    // ★ 데이터 안전성 사슬 전체. 이 수정의 안전성 근거는 승격 push 게이트가 아니라
    //   "skip-truncated면 창 미기록 → 다음 부팅 present+scoped+미체크 → stash로 보존"
    //   이라는 연결이다. 한 케이스로 끝까지 붙여 고정한다.
    test('T5) 잘림으로 이관에 실패해도 다음 부팅의 stash로 수렴한다', async () => {
      // ① 모델 i-a 부팅: mkdir-plant → 곧바로 잘림
      const b1 = boot({ fsInit: { [MOUNT]: dirEntry() }, legacySource: plainLegacySource() });
      await syncfs(b1, true);
      await wait(300);
      simulateUnityBoot(b1, APP, 'dirCheckWriteFirst');
      await wait(200);
      expect(b1.win.__AIT_PP.status().legacyImport).toBe('skip-truncated');
      expect(b1.store.has(MANIFEST_KEY), '승격 push는 억제된다').toBe(false);

      // 그 뒤 게임이 진짜 세이브를 한다 — persistPath는 게이트되지 않는다(무회귀 계약 7)
      b1.entries.node(APP + '/PlayerPrefs')!.contents = new Uint8Array(Buffer.from('real-save'));
      await syncfs(b1, false);
      await wait(200);
      const m1 = b1.store.get(MANIFEST_KEY);
      expect(manifestFiles(m1), '게임 쓰기는 정본으로 올라간다').toContain(APP + '/PlayerPrefs');
      expect(manifestLegacy(m1), '창은 열린 채 남아야 다음 부팅에서 보존된다').toBeUndefined();

      // ② 다음 부팅: present + scoped 존재 + 미체크 → stash 경로
      const b2 = boot({
        storageSeed: Object.fromEntries(b1.store),
        fsInit: { ...bootedBefore, [APP + '/PlayerPrefs']: ppEntry('real-save') },
        legacySource: plainLegacySource(),
      });
      await syncfs(b2, true);
      await wait(400);
      const s2 = b2.win.__AIT_PP.status();
      expect(s2.legacyImport).toBe('stashed');
      expect(s2.legacyStashState).toBe('written');
      expect(b2.store.has(STASH_KEY), '레거시 덤프가 보존됐다').toBe(true);
      expect(decodeBytes(b2.entries.get(APP + '/PlayerPrefs')!.contents), '라이브 세이브 무변경').toBe(
        'real-save',
      );
      expect(manifestLegacy(b2.store.get(MANIFEST_KEY))).toMatchObject({
        checked: true,
        result: 'stashed',
      });
    });
  });
});
