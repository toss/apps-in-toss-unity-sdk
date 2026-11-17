/**
 * Unity WebGL jslib 런타임 타입 정의
 *
 * Unity Emscripten 환경에서 제공하는 글로벌 함수들의 타입 선언입니다.
 * 이 파일은 생성되는 TypeScript 브릿지 코드에서 참조됩니다.
 */

// =============================================================================
// Emscripten 문자열 변환 함수
// =============================================================================

/**
 * WebAssembly 메모리의 포인터를 UTF-8 문자열로 변환
 * @param ptr - WebAssembly 메모리 포인터 (C#에서 전달된 문자열)
 */
declare function UTF8ToString(ptr: number): string;

/**
 * JavaScript 문자열을 WebAssembly 메모리에 할당하고 포인터 반환
 * @param str - 할당할 문자열
 * @returns WebAssembly 메모리 포인터
 */
declare function stringToNewUTF8(str: string): number;

/**
 * 문자열의 UTF-8 인코딩 바이트 길이 계산
 * @param str - 길이를 계산할 문자열
 */
declare function lengthBytesUTF8(str: string): number;

/**
 * JavaScript 문자열을 WebAssembly 메모리 버퍼에 UTF-8로 복사
 * @param str - 복사할 문자열
 * @param outPtr - 대상 메모리 포인터
 * @param maxBytesToWrite - 최대 바이트 수
 */
declare function stringToUTF8(str: string, outPtr: number, maxBytesToWrite: number): void;

// =============================================================================
// Unity 통신 함수
// =============================================================================

/**
 * Unity GameObject에 메시지 전송
 * @param objectName - 대상 GameObject 이름
 * @param methodName - 호출할 메서드 이름
 * @param param - 전달할 문자열 파라미터
 */
declare function SendMessage(objectName: string, methodName: string, param: string): void;

// =============================================================================
// Emscripten 메모리 관리 함수
// =============================================================================

/**
 * WebAssembly 힙에 메모리 할당
 * @param size - 할당할 바이트 수
 * @returns 할당된 메모리 포인터
 */
declare function _malloc(size: number): number;

/**
 * WebAssembly 힙 메모리 해제
 * @param ptr - 해제할 메모리 포인터
 */
declare function _free(ptr: number): void;

// =============================================================================
// Unity jslib 라이브러리 시스템
// =============================================================================

/**
 * Unity jslib 라이브러리 매니저
 * jslib 파일에서 정의한 함수들을 WebAssembly에 병합할 때 사용
 */
declare const LibraryManager: {
  library: Record<string, unknown>;
};

/**
 * JavaScript 함수들을 Unity 라이브러리에 병합
 * 각 jslib 파일에서 함수를 등록할 때 사용
 * @param library - LibraryManager.library 객체
 * @param functions - 병합할 함수 객체
 */
declare function mergeInto(
  library: Record<string, unknown>,
  functions: Record<string, Function>
): void;

// =============================================================================
// 글로벌 window 확장 (web-framework)
// =============================================================================

/**
 * AppsInToss SDK가 주입하는 글로벌 객체
 * web-framework에서 제공하는 API들을 포함
 */
interface AppsInTossGlobal {
  // web-framework에서 동적으로 정의됨
  [key: string]: unknown;
}

declare global {
  interface Window {
    AppsInToss: AppsInTossGlobal;
    /**
     * AIT 이벤트 구독 저장소
     * 이벤트 구독 ID를 키로, unsubscribe 함수를 값으로 저장
     */
    __AIT_SUBSCRIPTIONS?: Record<string, () => void>;
  }
}

export {};
