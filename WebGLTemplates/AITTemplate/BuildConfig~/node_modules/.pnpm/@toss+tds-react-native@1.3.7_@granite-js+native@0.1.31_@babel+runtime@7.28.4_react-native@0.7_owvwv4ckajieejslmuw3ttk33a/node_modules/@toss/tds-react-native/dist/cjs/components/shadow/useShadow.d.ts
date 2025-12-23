import type { Shadow } from './types';
export type UseShadowProps = Shadow;
/**
 * @description 플랫폼에 따른 shadow style을 생성합니다.
 * @returns iOS: { shadowColor, shadowOffset, shadowOpacity, shadowRadius }, Android: { elevation, shadowColor }
 */
export declare const useShadow: (shadow: UseShadowProps) => Pick<import("react-native").ViewStyle, "shadowColor" | "shadowOffset" | "shadowOpacity" | "shadowRadius"> | Pick<import("react-native").ViewStyle, "elevation" | "shadowColor">;
