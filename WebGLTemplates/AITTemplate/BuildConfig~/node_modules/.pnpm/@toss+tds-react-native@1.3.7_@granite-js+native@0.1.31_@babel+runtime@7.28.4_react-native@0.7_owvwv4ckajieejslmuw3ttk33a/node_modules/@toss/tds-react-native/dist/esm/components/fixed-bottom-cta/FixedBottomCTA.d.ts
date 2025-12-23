import type { BottomCTADoubleProps, BottomCTASingleProps } from '../bottom-cta';
export type FixedBottomCTASingleProps = Omit<BottomCTASingleProps, 'display' | 'keyboardStyleEnabled'>;
export type FixedBottomCTADoubleProps = Omit<BottomCTADoubleProps, 'keyboardStyleEnabled'>;
/**
 * FixdBottomCTAConsumer 없이 쓰고 싶은 경우 사용합니다.
 * 예를들어, FixedBottomCTAConsumer 내에 다른 요소를 넣고 싶은 경우입니다.
 */
export declare function FixedBottomCTAWithoutConsumer(props: BottomCTASingleProps): import("react/jsx-runtime").JSX.Element;
/**
 * FixdBottomCTAConsumer 없이 쓰고 싶은 경우 사용합니다.
 * 예를들어, FixedBottomCTAConsumer 내에 다른 요소를 넣고 싶은 경우입니다.
 */
export declare function FixedBottomCTADoubleWithoutConsumer(props: BottomCTADoubleProps): import("react/jsx-runtime").JSX.Element;
export declare function FixedBottomCTA(props: FixedBottomCTASingleProps): import("react/jsx-runtime").JSX.Element;
export declare namespace FixedBottomCTA {
    var Double: typeof FixedBottomCTADouble;
}
declare function FixedBottomCTADouble(props: BottomCTADoubleProps): import("react/jsx-runtime").JSX.Element;
export {};
