/**
 * 비동기 브릿지 함수 생성
 * @param method 호출할 메서드 이름
 * @returns args를 받아 Promise를 반환하는 함수
 * @typeParam T: 인자로 전달할 데이터 타입
 * @typeParam R: resolve 시 반환될 데이터 타입 (기본 any)
 * @example
 * // 카메라 열기 브릿지 함수 생성
 * const openCamera = createAsyncBridge<{base64: boolean}, { dataUri: string }>('openCamera');
 *
 * // 사용 예시
 * async function takePicture() {
 *   try {
 *     const result = await openCamera();
 *     console.log('카메라로 찍은 이미지 URL:', result.dataUri);
 *   } catch (error) {
 *     console.error('카메라 오류:', error);
 *   }
 * }
 */
declare function createAsyncBridge<T extends any[], R = any>(method: string): (...args: T) => Promise<R>;

/**
 * 이벤트 브릿지 함수 생성
 * @param method 호출할 이벤트 핸들러 이름
 * @returns 이벤트 및 에러 핸들러를 포함한 객체를 받아 리스너 해제 함수를 반환하는 함수
 * @typeParam T: 이벤트 관련 추가 옵션 등의 타입
 * @example
 * // 위치 추적 이벤트 브릿지 생성
 * const startUpdatingLocation = createEventBridge<{ interval: number }>('startUpdatingLocation');
 *
 * // 사용 예시
 * const unsubscribe = startUpdatingLocation({
 *   options: { interval: 1000 }, // 1초마다 위치 업데이트
 *   onEvent: (location) => {
 *     console.log('현재 위치:', location.latitude, location.longitude);
 *   },
 *   onError: (error) => {
 *     console.error('위치 추적 오류:', error);
 *   }
 * });
 *
 * // 위치 추적 중단
 * unsubscribe();
 */
declare function createEventBridge<TOptions, TData>(method: string): (args: {
    onEvent: (data: TData) => void;
    onError: (error: Error) => void;
    options?: TOptions;
}) => () => void;

/**
 * 상수 브릿지 함수 생성
 * @param method 호출할 상수 핸들러 이름
 * @returns 상수 값을 반환하는 함수
 * @typeParam T: 상수 값의 타입
 * @example
 * // 디바이스 ID를 가져오는 상수 브릿지 생성
 * const getDeviceId = createConstantBridge<string>('getDeviceId');
 *
 * // 사용 예시
 * function displayDeviceInfo() {
 *   const deviceId = getDeviceId();
 *   console.log('디바이스 ID:', deviceId);
 * }
 */
declare function createConstantBridge<T>(method: string): () => T;

export { createAsyncBridge, createConstantBridge, createEventBridge };
