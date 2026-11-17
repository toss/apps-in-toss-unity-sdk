/**
 * DOM/브라우저 전용 타입 목록
 * 이 타입들은 Unity에서 사용할 수 없으므로 'object'로 처리
 * 순환 참조가 많아 스택 오버플로우를 유발할 수 있음
 */
export const DOM_TYPES = new Set([
  'HTMLElement',
  'Element',
  'Node',
  'Document',
  'Window',
  'Event',
  'EventTarget',
  'HTMLDivElement',
  'HTMLSpanElement',
  'HTMLInputElement',
  'HTMLButtonElement',
  'HTMLAnchorElement',
  'HTMLImageElement',
  'HTMLCanvasElement',
  'HTMLVideoElement',
  'HTMLAudioElement',
  'HTMLFormElement',
  'HTMLSelectElement',
  'HTMLTextAreaElement',
  'HTMLTableElement',
  'HTMLIFrameElement',
  'SVGElement',
  'SVGSVGElement',
  'DocumentFragment',
  'ShadowRoot',
  'Text',
  'Comment',
  'Attr',
  'NamedNodeMap',
  'NodeList',
  'HTMLCollection',
  'DOMTokenList',
  'CSSStyleDeclaration',
  'DOMRect',
  'DOMRectReadOnly',
  'TouchEvent',
  'MouseEvent',
  'KeyboardEvent',
  'PointerEvent',
  'FocusEvent',
  'WheelEvent',
  'DragEvent',
  'ClipboardEvent',
  'AnimationEvent',
  'TransitionEvent',
]);

/**
 * C#에서 값의 구조를 표현할 수 없는 DOM 유니온 타입.
 * 재귀 정의(IDBValidKey = number | string | Date | BufferSource | IDBValidKey[])라 풀어서
 * 파싱하면 스택 오버플로우가 나므로 `object`로 처리한다. (JSON으로 직렬화되는 값 그대로 받는다)
 */
export const OPAQUE_OBJECT_TYPES = new Set(['IDBValidKey']);
