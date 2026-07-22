import { describe, test, expect } from 'vitest';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { generateFieldDoc } from '../../src/generators/csharp/field-docs.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SDK_DIR = path.resolve(__dirname, '../../..', 'Runtime/SDK');

// processProductGrant는 SDK에서 가장 위험한 필드다. 이 콜백 안에서 await를 쓰면
// 네이티브 오버레이 구간에 player loop가 멈춰 continuation이 재개되지 않고,
// 오버레이는 이 콜백의 응답을 기다리므로 순환 교착이 된다 (실측 115초 정지 후 환불 페이지).
// 업스트림 JSDoc은 웹 기준이라 이 제약을 담지 못하므로 SDK가 직접 문서를 넣는다.
// 재생성 때 조용히 사라지면 개발자가 다시 같은 함정에 빠지므로 여기서 고정한다.
// XML 주석 접두사와 줄바꿈을 걷어내 문장이 줄에 걸쳐 있어도 검사할 수 있게 만든다.
// 이게 없으면 문구를 다듬어 줄바꿈 위치가 바뀔 때마다 테스트가 엉뚱하게 깨진다.
function flatten(doc: string): string {
  return doc.replace(/^\s*\/\/\/ ?/gm, '').replace(/\s+/g, ' ').trim();
}

describe('필드 XML 문서 오버라이드', () => {
  test('processProductGrant에 동기 반환 규칙이 문서화되어야 함', () => {
    const doc = flatten(generateFieldDoc('processProductGrant'));

    expect(doc).toContain('반드시 동기로 값을 반환해야 한다');
    expect(doc).toContain('<c>await</c>를 쓰면 결제가 완료되지 않는다');
    // 경고만 있고 대안이 없으면 막다른 길이 된다.
    expect(doc).toContain('Task.FromResult(true)');
  });

  test('즉시 승인이 안전해지려면 대사 단계가 함께 문서화되어야 함', () => {
    const doc = flatten(generateFieldDoc('processProductGrant'));

    // true는 비가역이라 승인 직후 앱이 죽으면 pending에도 안 남는다.
    // 회수 창구를 같이 알려주지 않으면 즉시 승인 권고가 오히려 위험해진다.
    expect(doc).toContain('IAPGetCompletedOrRefundedOrders');
    expect(doc).toMatch(/되돌리는\s*API가 없어/);
    // 로컬 기록은 재설치·기기 변경에서 사라지므로 대사 기준이 될 수 없다.
    expect(doc).toContain('PlayerPrefs');
  });

  test('false는 도피구가 아니라 예외로 문서화되어야 함', () => {
    const doc = flatten(generateFieldDoc('processProductGrant'));

    // "확신 없으면 false"로 읽히면 매 결제마다 환불 안내가 뜨는 앱이 된다.
    expect(doc).toContain('정말로 이 상품을 줄 수 없을 때만');
    expect(doc).toContain('환불 안내');
  });

  test('오버라이드는 업스트림 description을 이긴다', () => {
    const doc = generateFieldDoc('processProductGrant', '업스트림 설명');

    expect(doc).not.toContain('업스트림 설명');
    expect(flatten(doc)).toContain('반드시 동기로 값을 반환해야 한다');
  });

  test('오버라이드가 없는 필드는 업스트림 description을 한 줄 summary로 쓴다', () => {
    expect(generateFieldDoc('sku', '상품 SKU'))
      .toBe('        /// <summary>상품 SKU</summary>\n');
    expect(generateFieldDoc('sku')).toBe('');
  });

  test('생성된 C#의 일회성/구독 결제 양쪽 필드에 문서가 붙어야 함', () => {
    const source = fs.readFileSync(path.join(SDK_DIR, 'AIT.Types.IAP.cs'), 'utf-8');

    // 필드 선언 바로 위에 </remarks>가 오는지로 "붙어 있음"을 확인한다.
    const attached = source.match(
      /\/\/\/ <\/remarks>\s*\n\s*\[JsonIgnore\]\s*\n\s*public System\.Func<[^>]*ProcessProductGrantParam[^;]*ProcessProductGrant;/g,
    );

    expect(attached).not.toBeNull();
    expect(attached!.length).toBe(2);
  });
});
