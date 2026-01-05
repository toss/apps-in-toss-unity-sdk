/**
 * example.ts
 *
 * 커스텀 브릿지 예제 파일
 * 이 파일의 export 함수들은 Unity C# + jslib로 자동 변환됩니다.
 *
 * 사용 방법:
 * 1. 이 폴더에 TypeScript 파일 작성
 * 2. Unity 메뉴 > Apps in Toss > Custom Bridges > Generate Bridges 실행
 * 3. Unity에서 AppsInToss.Bridges.Example 클래스로 호출
 *
 * Unity 사용 예:
 * ```csharp
 * using AppsInToss.Bridges;
 *
 * var result = await Example.Add(1, 2);
 * Debug.Log($"Result: {result}"); // 3
 *
 * var greeting = await Example.Greet("Unity");
 * Debug.Log(greeting); // "Hello, Unity!"
 * ```
 */

/**
 * 두 숫자를 더합니다.
 * @param a 첫 번째 숫자
 * @param b 두 번째 숫자
 * @returns 합계
 */
export function add(a: number, b: number): number {
  return a + b;
}

/**
 * 인사 메시지를 생성합니다.
 * @param name 이름
 * @returns 인사 메시지
 */
export function greet(name: string): string {
  return `Hello, ${name}!`;
}

/**
 * 비동기 작업 예제 (Promise 반환)
 * @param ms 대기 시간 (밀리초)
 * @returns 완료 메시지
 */
export async function delay(ms: number): Promise<string> {
  return new Promise((resolve) => {
    setTimeout(() => {
      resolve(`Waited ${ms}ms`);
    }, ms);
  });
}

/**
 * 객체를 받아서 처리하는 예제
 * @param data 키-값 데이터
 * @returns 처리 결과
 */
export function processData(data: Record<string, string>): boolean {
  console.log('[Example] Processing data:', data);
  return Object.keys(data).length > 0;
}

/**
 * 선택적 파라미터 예제
 * @param message 메시지
 * @param count 반복 횟수 (선택적, 기본값 1)
 * @returns 반복된 메시지
 */
export function repeat(message: string, count?: number): string {
  const times = count ?? 1;
  return message.repeat(times);
}
