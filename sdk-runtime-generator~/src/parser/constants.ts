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
 * 네임스페이스 -> 카테고리 특수 매핑 (기본값: 네임스페이스명을 PascalCase로)
 * 새 네임스페이스가 추가되어도 자동으로 처리됨
 */
export const NAMESPACE_CATEGORY_OVERRIDES: Record<string, string> = {
  GoogleAdMob: 'Advertising',
  SafeAreaInsets: 'SafeArea',
  env: 'Environment',
};
