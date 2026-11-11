/**
 * 하네스 API 카탈로그 생성기 검증.
 *
 * 카탈로그(Tests~/E2E/SharedScripts/Runtime/AITApiCatalog.g.cs)는 Runtime/SDK의 C# 본문에서 만든다.
 * 그래서 web-framework 버전이 바뀌어도 커밋된 Runtime/SDK와 항상 맞아야 한다.
 */
import { describe, test, expect } from 'vitest';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  API_CATALOG_RELATIVE_PATH,
  collectApiCategories,
  collectEnums,
  extractApiMethodNames,
  extractEnums,
  generateApiCatalog,
  orderCategories,
} from '../../src/generators/api-catalog.js';
import { CATEGORY_ORDER, NAMESPACE_CATEGORIES } from '../../src/categories.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(__dirname, '../../..');
const SDK_DIR = path.join(REPO_ROOT, 'Runtime/SDK');

function readSdkFiles(pattern: RegExp): Map<string, string> {
  const files = new Map<string, string>();
  for (const file of fs.readdirSync(SDK_DIR).filter(f => pattern.test(f)).sort()) {
    files.set(file, fs.readFileSync(path.join(SDK_DIR, file), 'utf-8'));
  }
  return files;
}

const CATEGORY_FILE_RE = /^AIT\.(?!Types\.|Screen\.)[A-Za-z]+\.cs$/;
const TYPE_FILE_RE = /^AIT\.Types\..+\.cs$/;

function committedInput() {
  return {
    categoryFiles: readSdkFiles(CATEGORY_FILE_RE),
    manualFiles: new Map([
      ['AIT.Screen.cs', { category: NAMESPACE_CATEGORIES.Screen, content: fs.readFileSync(path.join(SDK_DIR, 'AIT.Screen.cs'), 'utf-8') }],
    ]),
    typeFiles: readSdkFiles(TYPE_FILE_RE),
  };
}

describe('extractApiMethodNames', () => {
  test('Task, Awaitable, Action을 돌려주는 public static 메서드만 모은다', () => {
    const source = `
        public static async Task<bool> FooIsSupported(int timeoutMs = 0)
        public static async Awaitable<Dictionary<ConsentedUserDataKey, string>> GetData(
        public static Action SubscribeBar(
        public static Awaitable<System.Action> SubscribeBaz(string id)
        public static double GetDevicePixelRatio()
        private static async Task Hidden()
        public async Task Instance()
        /// public static async Task Documented()
        // public static async Task Commented()
`;
    expect(extractApiMethodNames(source).sort()).toEqual(['FooIsSupported', 'GetData', 'SubscribeBar', 'SubscribeBaz']);
  });

  test('Task/Awaitable 두 변형이 같은 이름이면 한 번만 센다', () => {
    const source = `
#if UNITY_6000_0_OR_NEWER
        public static async Awaitable<bool> Foo(int timeoutMs = 0)
#else
        public static async Task<bool> Foo(int timeoutMs = 0)
#endif
`;
    expect(extractApiMethodNames(source)).toEqual(['Foo']);
  });
});

describe('extractEnums', () => {
  test('멤버를 선언 순서대로 모으고 어트리뷰트·주석·값 대입을 걷어 낸다', () => {
    const source = `
    /// <summary>설명 (A, B)</summary>
    public enum Sample
    {
        [EnumMember(Value = "a]b,c")]
        First,
        // 한 줄 주석, 쉼표 포함
        [EnumMember(Value = "2G")]
        _2G = 5,
        [Obsolete("old")]
        @class,
        Last
    }

    public class NotEnum
    {
        public enum Nested { X }
    }
`;
    expect(extractEnums(source)).toEqual([{ name: 'Sample', members: ['First', '_2G', 'class', 'Last'] }]);
  });
});

describe('orderCategories', () => {
  test('CATEGORY_ORDER를 먼저, 그 밖은 알파벳 순으로 붙인다', () => {
    expect(orderCategories(['Zeta', 'Device', 'Alpha', 'Authentication'])).toEqual(['Authentication', 'Device', 'Alpha', 'Zeta']);
  });
});

describe('collectApiCategories', () => {
  test('카테고리는 메서드가 들어간 파일이 정한다', () => {
    const map = collectApiCategories({
      categoryFiles: new Map([
        ['AIT.Device.cs', 'public static async Task<bool> A()'],
        ['AIT.Navigation.cs', 'public static Action B('],
      ]),
      typeFiles: new Map(),
    });
    expect(Object.fromEntries(map)).toEqual({ A: 'Device', B: 'Navigation' });
  });

  test('같은 이름이 두 카테고리에 있으면 실패한다', () => {
    expect(() =>
      collectApiCategories({
        categoryFiles: new Map([
          ['AIT.Device.cs', 'public static async Task A()'],
          ['AIT.Media.cs', 'public static async Task A()'],
        ]),
        typeFiles: new Map(),
      }),
    ).toThrow(/두 카테고리/);
  });

  test('수동 파일은 지정한 카테고리를 쓴다', () => {
    const map = collectApiCategories({
      categoryFiles: new Map(),
      manualFiles: new Map([['AIT.Screen.cs', { category: 'Device', content: 'public static async Task S()' }]]),
      typeFiles: new Map(),
    });
    expect(map.get('S')).toBe('Device');
  });
});

describe('collectEnums', () => {
  test('같은 enum이 두 번 선언되면 실패한다', () => {
    const code = '    public enum Dup\n    {\n        A\n    }\n';
    expect(() => collectEnums({ categoryFiles: new Map(), typeFiles: new Map([['AIT.Types.A.cs', code], ['AIT.Types.B.cs', code]]) })).toThrow(/Dup/);
  });
});

describe('커밋된 Runtime/SDK 기준 카탈로그', () => {
  const input = committedInput();
  const generated = generateApiCatalog(input);

  test('커밋된 카탈로그 파일과 같다', () => {
    const committed = fs.readFileSync(path.join(REPO_ROOT, API_CATALOG_RELATIVE_PATH), 'utf-8');
    expect(generated).toBe(committed);
  });

  test('카탈로그 .meta가 커밋돼 있다', () => {
    expect(fs.existsSync(path.join(REPO_ROOT, API_CATALOG_RELATIVE_PATH + '.meta'))).toBe(true);
  });

  test('카테고리 파일의 모든 API 메서드가 카탈로그에 있다', () => {
    const map = collectApiCategories(input);
    expect(map.size).toBeGreaterThan(100);
    for (const [fileName, content] of input.categoryFiles) {
      const category = /^AIT\.(.+)\.cs$/.exec(fileName)![1];
      for (const name of extractApiMethodNames(content)) {
        expect(map.get(name), `${fileName}: ${name}`).toBe(category);
      }
    }
  });

  test('카테고리 순서는 쓰인 카테고리만, CATEGORY_ORDER 규약을 따른다', () => {
    const map = collectApiCategories(input);
    const used = new Set(map.values());
    const ordered = orderCategories(used);
    expect(new Set(ordered)).toEqual(used);
    const known = ordered.filter(c => CATEGORY_ORDER.includes(c));
    expect(known).toEqual(CATEGORY_ORDER.filter(c => used.has(c)));
    expect(generated).toContain(`"${ordered[0]}",`);
  });

  test('Runtime/SDK의 모든 최상위 public enum이 표에 있다', () => {
    const declared: string[] = [];
    for (const content of input.typeFiles.values()) {
      for (const m of content.matchAll(/^ {4}public enum (\w+)/gm)) declared.push(m[1]);
    }
    expect(declared.length).toBeGreaterThan(0);
    expect(collectEnums(input).map(e => e.name).sort()).toEqual([...declared].sort());
    for (const name of declared) expect(generated).toContain(`typeof(AppsInToss.${name})`);
  });

  test('모든 enum은 멤버가 하나 이상이다', () => {
    for (const e of collectEnums(input)) expect(e.members.length, e.name).toBeGreaterThan(0);
  });
});
