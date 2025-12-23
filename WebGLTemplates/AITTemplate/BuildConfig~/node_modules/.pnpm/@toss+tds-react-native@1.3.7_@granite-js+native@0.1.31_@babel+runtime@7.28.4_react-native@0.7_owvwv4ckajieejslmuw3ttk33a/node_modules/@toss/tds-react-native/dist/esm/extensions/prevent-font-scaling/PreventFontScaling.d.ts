import type { ReactNode } from 'react';
interface Props {
    /**
     * @description 제한하고싶은 fontScale 값
     */
    limit: number;
    children: ReactNode;
}
/**
 * @name PreventFontScaling
 * @description FontScale이 커지는것을 {limit} 이상 제한합니다.
 * @example
 * ```tsx
 * // 160% 이상 커지지 않습니다.
 * <PreventFontScaling limit={160}>
 *  <Txt>Typography</Txt>
 * </PreventFontScaling>
 * ```
 */
export declare function PreventFontScaling({ limit, children }: Props): import("react/jsx-runtime").JSX.Element;
export {};
