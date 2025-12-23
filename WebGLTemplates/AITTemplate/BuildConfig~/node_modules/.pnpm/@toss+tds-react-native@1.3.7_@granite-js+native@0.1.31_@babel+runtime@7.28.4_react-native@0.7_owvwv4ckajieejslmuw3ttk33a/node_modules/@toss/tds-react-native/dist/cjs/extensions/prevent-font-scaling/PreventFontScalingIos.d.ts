import type { ReactNode } from 'react';
interface Props {
    limit: number;
    children: ReactNode;
}
/**
 * @name PreventFontScaling
 * @desciption 더 큰텍스트의 최대 값을 지정합니다.
 */
export declare function PreventFontScalingIos({ limit, children }: Props): import("react/jsx-runtime").JSX.Element;
export {};
