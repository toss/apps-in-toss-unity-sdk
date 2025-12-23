import type { ColorPreference } from '../../core';
import type { ViewStyle } from 'react-native';
import type { Shadow } from './types';
type ShadowStyle = Pick<ViewStyle, 'shadowColor' | 'shadowOffset' | 'shadowOpacity' | 'shadowRadius'> | Pick<ViewStyle, 'shadowColor' | 'elevation'>;
/**
 * @description 플랫폼에 따른 shadow style을 생성합니다.
 * @returns iOS: { shadowColor, shadowOffset, shadowOpacity, shadowRadius }, Android: { shadowColor, elevation }
 */
export declare const generateShadowStyle: ({ offset, opacity, radius, colorPreference, ...colorProps }: Shadow & {
    colorPreference?: ColorPreference;
}) => ShadowStyle;
export {};
