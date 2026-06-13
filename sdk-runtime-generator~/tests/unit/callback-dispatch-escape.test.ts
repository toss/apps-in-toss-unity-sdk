import { describe, test, expect } from 'vitest';
import { loadAllTemplates } from '../../src/generators/csharp/templates.js';

/**
 * 회귀 방지: csharp-core.hbs의 콜백 디스패치 switch는 C# 타입명을
 * triple-stache({{{this}}})로 렌더해야 한다.
 *
 * double-stache({{this}})를 쓰면 Handlebars가 기본 HTML escape를 적용해
 * `Dictionary<K, V>` 같은 제네릭 타입의 '<'/'>'가 '&lt;'/'&gt;'로 깨진다.
 *   - 제네릭 인자: `TryGetCallback<Dictionary&lt;...&gt;>` → C# 컴파일 에러(CS1026 등)
 *   - case 라벨: call-site(csharp-api.hbs의 "{{{callbackType}}}", raw)와 불일치 → 런타임 디스패치 실패
 *
 * 실제 사례: web-bridge 2.7.0 `getConsentedUserData`가
 * `Dictionary<ConsentedUserDataKey, string>`를 반환하면서 이 버그가 표면화됨.
 */
describe('콜백 디스패치 템플릿 HTML escape 회귀 (csharp-core.hbs)', () => {
  const DICT = 'Dictionary<ConsentedUserDataKey, string>';

  test('Dictionary<K,V> 콜백 타입이 case 라벨/제네릭/예외 메시지에 raw angle bracket으로 렌더된다', async () => {
    const cache = await loadAllTemplates();
    expect(cache.coreTemplate).toBeDefined();

    const rendered = cache.coreTemplate!({
      callbackTypes: [DICT],
      eventDataTypes: [DICT],
      enumCallbackTypes: [],
    });

    // 디스패치 "코드"에 escape가 일어나면 안 됨 (컴파일/매칭 둘 다 깨짐).
    // `///` XML doc 주석에는 의도적으로 escape된 정적 텍스트(예: Func&lt;T, bool&gt;)가
    // 들어있고 이는 컴파일과 무관하므로 검사에서 제외한다.
    const codeLines = rendered
      .split('\n')
      .filter((l) => !l.trim().startsWith('///'));
    const escapedCode = codeLines.filter(
      (l) => l.includes('&lt;') || l.includes('&gt;'),
    );
    expect(escapedCode).toEqual([]);

    // case 라벨은 call-site 등록명(raw)과 글자 그대로 일치해야 디스패치됨
    expect(rendered).toContain(`case "${DICT}":`);

    // 제네릭 인자가 raw여야 C# 컴파일 가능
    expect(rendered).toContain(`TryGetCallback<${DICT}>`);
    expect(rendered).toContain(`JsonConvert.DeserializeObject<${DICT}>`);

    // 에러 콜백 AITException 메시지의 타입명도 raw
    expect(rendered).toContain(`new AITException("${DICT}", apiResponse.error)`);

    // eventDataTypes(구독 이벤트) 경로도 동일하게 raw
    expect(rendered).toContain(`(rawCallback as Action<${DICT}>)`);
  });

  test('단순 타입명(UserInfo)은 triple-stache 전환 후에도 동일하게 렌더된다 (회귀 없음)', async () => {
    const cache = await loadAllTemplates();
    const rendered = cache.coreTemplate!({
      callbackTypes: ['UserInfo'],
      eventDataTypes: [],
      enumCallbackTypes: [],
    });

    expect(rendered).toContain('case "UserInfo":');
    expect(rendered).toContain('TryGetCallback<UserInfo>');
    expect(rendered).not.toContain('&lt;');
  });
});
