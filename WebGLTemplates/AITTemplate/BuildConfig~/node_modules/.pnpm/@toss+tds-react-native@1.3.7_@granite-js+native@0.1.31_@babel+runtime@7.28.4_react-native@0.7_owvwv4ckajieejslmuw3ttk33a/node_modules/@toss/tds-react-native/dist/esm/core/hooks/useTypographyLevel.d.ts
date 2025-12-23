import { type TypographyLevel } from '../../constants';
/**
 * @private
 * @name useTypographyLevel
 * @description
 * 시스템의 `fontScale` 를 `TypographyLevel` 값으로 변환해요.
 * @returns {TypographyLevel} - iOS 혹은 Android 폰트 사이즈를 반환해요.
 */
export declare function useTypographyLevel(): TypographyLevel;
export declare function fontScaleToTypographyLevel(fontScale?: number): TypographyLevel;
/**
 * @name capTypographyLevel
 * @description
 * TypographyLevel을 최대값으로 제한해요.
 * iOS와 Android 모두 동일한 기준(normalize된 fontScale)으로 제한해요.
 * @param typographyLevel - 제한할 TypographyLevel
 * @param limit - 최대값 (기본값: 135, xxxLarge에 해당)
 * @returns 제한된 TypographyLevel
 * @example
 * ```ts
 * const typographyLevel = useTypographyLevel();
 * const cappedLevel = capTypographyLevel(typographyLevel, 135); // xxxLarge까지만 허용
 * ```
 */
export declare function capTypographyLevel(typographyLevel: TypographyLevel, limit?: number): TypographyLevel;
