import { describe, test, expect } from 'vitest';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadAllTemplates } from '../../src/generators/csharp/templates.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SDK_DIR = path.resolve(__dirname, '../../..', 'Runtime/SDK');

/**
 * ProcessProductGrant는 nullable이라 지정하지 않아도 컴파일되지만, jslib은 콜백을
 * 무조건 플랫폼에 주입하므로 C#에 등록된 핸들러가 없으면 SDK가 자동으로 false를 응답한다.
 * 즉 필드를 몰라서 안 쓴 개발자는 모든 결제가 조용히 지급 실패한다.
 *
 * 이 경로에서 개발자가 보는 유일한 신호가 미등록 진단 로그다. 예전 문구
 * ("Unknown nested callback: {key}")는 결제가 실패했다는 사실을 말해주지 않아
 * 원인을 짐작할 수 없었다. 아래는 그 문구가 결과와 대처를 잃지 않도록 고정한다.
 */
describe('미등록 nested callback 진단 (csharp-core.hbs)', () => {
  async function renderCore(): Promise<string> {
    const cache = await loadAllTemplates();
    expect(cache.coreTemplate).toBeDefined();
    return cache.coreTemplate!({
      callbackTypes: [],
      eventDataTypes: [],
      enumCallbackTypes: [],
    });
  }

  test('미등록은 경고가 아니라 에러로 보고한다', async () => {
    const rendered = await renderCore();

    // 결제 한 건이 거절되는 상황이라 LogWarning으로는 눈에 띄지 않는다.
    expect(rendered).toContain('Nested callback \'{request.CallbackName}\' is not registered');
    expect(rendered).not.toContain('Unknown nested callback');
    expect(rendered).toMatch(/Debug\.LogError\(\s*\$"\[AITCore\] Nested callback/);
  });

  test('processProductGrant일 때는 결제 실패라는 결과를 말해준다', async () => {
    const rendered = await renderCore();

    // 키 이름만으로는 "결제가 방금 실패했다"를 알 수 없다.
    expect(rendered).toContain('request.CallbackName == "processProductGrant"');
    expect(rendered).toContain('The payment already succeeded');
    expect(rendered).toContain('will NOT be granted');
    // 대처가 없으면 진단이 막다른 길이 된다. 동기 bool 계약이므로 즉시 승인 예시를 준다.
    expect(rendered).toContain('_ => true');
    // 검증·지급을 어디서 하는지(onEvent)까지 짚어 다음 스텝을 잃지 않게 한다.
    expect(rendered).toContain('verify and deliver later in onEvent');
  });

  test('dispatch는 동기다 — 등록·실행 어느 쪽에도 Task 경로가 없다', async () => {
    const rendered = await renderCore();

    // 등록: 동기 Func<TParam, TResult>. Task<...> 래퍼가 되살아나면 async 람다가
    // 다시 컴파일되어 오버레이 교착이 재발한다. (타입 매핑은 function-mapping.test.ts가,
    // 템플릿 쪽 계약은 여기가 고정한다 — hbs만 단독으로 되돌아가는 회귀 차단)
    expect(rendered).toContain(
      'public void RegisterNestedCallback<TParam, TResult>(string subscriptionId, string callbackName, Func<TParam, TResult> callback)',
    );
    expect(rendered).toContain('rawCallback is Func<string, bool> callback');
    // 실행: 응답은 SendMessage와 같은 스택에서 나가야 한다. fire-and-forget 비동기
    // 디스패치로 되돌아가면 오버레이 정지 중 continuation이 재개되지 않는다.
    expect(rendered).not.toContain('DispatchNestedCallbackAsync');
    expect(rendered).not.toContain('Func<string, Task<bool>>');
  });

  test('진단을 추가해도 정확히 1회 false 응답은 유지된다', async () => {
    const rendered = await renderCore();

    // 응답이 유실되면 JS Promise가 영원히 pending으로 남는다.
    expect(rendered).toContain('RespondToNestedCallback(request.RequestId, false);');
  });

  test('생성된 AITCore.cs에도 반영되어 있어야 함', async () => {
    const source = fs.readFileSync(path.join(SDK_DIR, 'AITCore.cs'), 'utf-8');

    expect(source).toContain('is not registered (id: {request.CallbackId})');
    expect(source).not.toContain('Unknown nested callback');
  });
});
