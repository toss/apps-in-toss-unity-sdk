/**
 * 옛 string 반환 멤버(OpenPermissionDialog, GetUserKeyForGame)가 이전 SDK와 같은 문자열을 받는지 확인한다.
 *
 * 이전 SDK는 jslib가 `data: JSON.stringify(result)`를 보내고 AITCore의 "string" 처리가 따옴표를 벗겨 문자열을 만들었다.
 * 새 jslib를 typeName "string"으로 실행해 같은 payload가 나오는지 보고, AITCore의 string 처리를 같은 규칙으로 옮겨
 * 케이스별 최종 문자열을 이전 SDK의 기대값과 비교한다.
 */
import { describe, test, expect } from 'vitest';
import * as fs from 'node:fs';
import * as path from 'node:path';
import * as vm from 'node:vm';
import { fileURLToPath } from 'node:url';
import { RETURN_TYPE_COMPAT, hasLegacyStringReturn } from '../../src/generators/csharp/compat.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SDK_DIR = path.resolve(__dirname, '../../..', 'Runtime/SDK');

/** AITCore "string" 처리의 옮김: 따옴표로 감싼 JSON 문자열이면 벗기고, 아니면 data 그대로 */
function coreStringPath(data: string): string {
  if (data.startsWith('"') && data.endsWith('"') && data.length >= 2) return JSON.parse(data) as string;
  return data;
}

interface Sent {
  TypeName: string;
  response: { success: boolean; data?: string; error: string };
}

/** jslib 함수를 가짜 환경에서 실행하고 AITCore로 간 메시지를 모은다 */
async function runJslib(
  file: string,
  fn: string,
  jsApi: (...a: unknown[]) => unknown,
  apiName: string,
  typeName: string,
  args: string[],
): Promise<Sent> {
  const code = fs.readFileSync(path.join(SDK_DIR, 'Plugins', file), 'utf-8');
  let library: Record<string, (...a: string[]) => void> = {};
  let sent: Sent | undefined;
  const ctx = {
    LibraryManager: { library: {} as Record<string, unknown> },
    mergeInto: (_lib: unknown, fns: Record<string, (...a: string[]) => void>) => {
      library = fns;
    },
    UTF8ToString: (s: string) => s,
    SendMessage: (_o: string, _m: string, payload: string) => {
      const p = JSON.parse(payload) as { TypeName: string; Result: string };
      sent = { TypeName: p.TypeName, response: JSON.parse(p.Result) };
    },
    window: { AppsInToss: { [apiName]: jsApi }, __AIT_VERBOSE: false },
    console,
    setTimeout: (f: () => void) => f(),
  };
  vm.runInNewContext(code, ctx);
  library[fn](...args, 'cb1', typeName);
  for (let i = 0; i < 5 && !sent; i++) await new Promise(r => setTimeout(r, 0));
  if (!sent) throw new Error('AITCore로 메시지가 가지 않았습니다');
  return sent;
}

describe('옛 string 반환 멤버: 이전 SDK와 같은 문자열', () => {
  test('표의 두 API 모두 jslib 쪽 옛 경로가 켜져 있다', () => {
    for (const { api } of RETURN_TYPE_COMPAT) {
      expect(hasLegacyStringReturn(api)).toBe(true);
      expect(hasLegacyStringReturn(api.charAt(0).toLowerCase() + api.slice(1))).toBe(true);
    }
    expect(hasLegacyStringReturn('GetDeviceId')).toBe(false);
  });

  test('AITCore의 string 처리는 따옴표를 벗기는 규칙 그대로다', () => {
    const core = fs.readFileSync(path.join(SDK_DIR, 'AITCore.cs'), 'utf-8');
    const start = core.indexOf('case "string":');
    const block = core.slice(start, core.indexOf('case "bool":', start));
    expect(block).toContain('stringData.StartsWith("\\"") && stringData.EndsWith("\\"") && stringData.Length >= 2');
    expect(block).toContain('stringData = JsonConvert.DeserializeObject<string>(stringData);');
    expect(block).toContain('stringCallback(stringData);');
  });

  // [웹 결과, 이전 SDK가 돌려주던 문자열]. data 키가 빠지는 undefined 는 APIResponse.data 기본값 ""
  const permissionCases: Array<[string, unknown, string]> = [
    ['allowed', 'allowed', 'allowed'],
    ['denied', 'denied', 'denied'],
    ['notDetermined', 'notDetermined', 'notDetermined'],
  ];
  const userKeyCases: Array<[string, unknown, string]> = [
    ['HASH 객체', { type: 'HASH', hash: 'abc123' }, '{"type":"HASH","hash":"abc123"}'],
    ['INVALID_CATEGORY', 'INVALID_CATEGORY', 'INVALID_CATEGORY'],
    ['ERROR', 'ERROR', 'ERROR'],
    ['따옴표를 품은 문자열', 'a"b', 'a"b'],
  ];

  test.each(permissionCases)('OpenPermissionDialog %s', async (_n, webResult, expected) => {
    const sent = await runJslib('AppsInToss-Permission.jslib', '__openPermissionDialog_Internal', async () => webResult, 'openPermissionDialog', 'string', [
      '{"name":"camera","access":"access"}',
    ]);
    expect(sent.response.success).toBe(true);
    expect(sent.response.data).toBe(JSON.stringify(webResult));
    expect(coreStringPath(sent.response.data ?? '')).toBe(expected);
  });

  test.each(userKeyCases)('GetUserKeyForGame %s', async (_n, webResult, expected) => {
    const sent = await runJslib('AppsInToss-GameCenter.jslib', '__getUserKeyForGame_Internal', async () => webResult, 'getUserKeyForGame', 'string', []);
    expect(sent.response.success).toBe(true);
    expect(sent.response.data).toBe(JSON.stringify(webResult));
    expect(coreStringPath(sent.response.data ?? '')).toBe(expected);
  });

  test.each([
    ['OpenPermissionDialog', 'AppsInToss-Permission.jslib', '__openPermissionDialog_Internal', 'openPermissionDialog', ['{"name":"camera","access":"access"}']],
    ['GetUserKeyForGame', 'AppsInToss-GameCenter.jslib', '__getUserKeyForGame_Internal', 'getUserKeyForGame', []],
  ] as const)('%s: undefined 는 data 키가 빠져 빈 문자열이 된다', async (_n, file, fn, api, args) => {
    const sent = await runJslib(file, fn, async () => undefined, api, 'string', [...args]);
    expect(sent.response.success).toBe(true);
    // JSON.stringify(undefined) 는 undefined 라서 직렬화된 메시지에 data 키가 없다 (C# 쪽 기본값 "")
    expect('data' in sent.response).toBe(false);
  });

  test.each([
    ['AppsInToss-GameCenter.jslib', '__getUserKeyForGame_Internal', 'getUserKeyForGame', [] as string[]],
  ] as const)('거부는 이전 SDK처럼 success:false + 메시지다 (%s)', async (file, fn, api, args) => {
    const sent = await runJslib(file, fn, async () => { throw new Error('boom'); }, api, 'string', [...args]);
    expect(sent.response.success).toBe(false);
    expect(sent.response.error).toBe('boom');
  });

  test('새 Typed 경로(typeName != string)는 합집합 포장을 그대로 쓴다', async () => {
    const sent = await runJslib('AppsInToss-GameCenter.jslib', '__getUserKeyForGame_Internal', async () => ({ type: 'HASH', hash: 'h' }), 'getUserKeyForGame', 'GetUserKeyForGameResult', []);
    expect(JSON.parse(sent.response.data ?? '{}')._type).toBe('success');
  });
});
