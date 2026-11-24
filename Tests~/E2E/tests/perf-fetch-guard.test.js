// @ts-check
// dev server 의 네이티브 fetch 가드(vite.config.ts aitNativeFetchGuardPlugin)가 하위 fetch 를 잡는 래퍼와 순환하지 않는지 확인한다.
// 회귀: 가드 getter 가 돌려준 stableFetch 를 databuf 래퍼가 하위 fetch 로 잡고 window.fetch 에 대입하면 delegate 가 그 래퍼가 되어,
// .wasm.br 같은 압축 확장자(상대 경로)는 Build 판정에서 빠져 stableFetch → 래퍼 → stableFetch 로 무한 재귀(Maximum call stack size exceeded)가 났다.
import { test, expect, chromium } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

const CONFIG = fs.readFileSync(path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../WebGLTemplates/AITTemplate/BuildConfig~/vite.config.ts'), 'utf8');

/** vite.config.ts 의 가드 스크립트 문자열 배열을 그대로 실행 가능한 JS 로 복원한다. */
function guardScript() {
  const m = CONFIG.match(/function aitNativeFetchGuardPlugin[\s\S]*?const script = (\[[\s\S]*?\])\.join\('\\n'\);/);
  if (!m) throw new Error('가드 스크립트를 찾지 못함');
  return /** @type {string[]} */ (new Function(`return ${m[1]};`)()).join('\n');
}

test.describe('native fetch 가드', () => {
  for (const asset of ['Build/a.wasm.br', 'Build/a.data.br', 'Build/a.framework.js.br', 'http://localhost/Build/a.wasm', '/other/path']) {
    test(`databuf 식 래퍼(하위 fetch = window.fetch 를 잡고 자신을 대입)와 순환하지 않는다: ${asset}`, async () => {
      const browser = await chromium.launch();
      try {
        const page = await browser.newPage();
        await page.route('**/*', (route) => route.fulfill({ status: 200, body: 'ok', contentType: 'text/html' }));
        await page.goto('http://localhost/blank.html');
        const out = await page.evaluate(async ({ script, asset }) => {
          new Function(script)();
          const prior = window.fetch; // 가드 getter 가 돌려주는 stableFetch
          window.fetch = function () { return prior.apply(this, arguments); }; // ait-databuf.js 의 래퍼와 같은 모양
          try {
            const r = await fetch(asset);
            return { ok: r.ok };
          } catch (e) {
            return { error: String(e && e.message || e) };
          }
        }, { script: guardScript(), asset });
        expect(out, '순환 없이 응답을 받는다').toEqual({ ok: true });
      } finally {
        await browser.close();
      }
    });
  }
});
