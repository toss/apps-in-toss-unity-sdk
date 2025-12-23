import type { ViewStyle } from 'react-native';
interface Props {
    type: 'single' | 'double';
    keyboardStyleEnabled: boolean;
}
/**
 * @name useBottomCTAContainerStyle
 * @description BottomCTA 의 하단 스타일
 */
export declare function useBottomCTAContainerStyle({ type, keyboardStyleEnabled }: Props): ViewStyle;
export {};
