/**
 * @name toInternationalAge
 * @description
 * 기준일을 기점으로 `만 나이`를 계산해 줍니다.
 * ```typescript
 * toInternationalAge(
 *   // 생년월일 (e.g. '1998-01-01')
 *   date: string,
 *   // 기준일 (e.g. '2022-09-21')
 *   baseDate?: string
 * ): number // => 만 나이
 * ```
 */
export declare const toInternationalAge: (dateOfBirth: string, baseDate?: string) => number;
