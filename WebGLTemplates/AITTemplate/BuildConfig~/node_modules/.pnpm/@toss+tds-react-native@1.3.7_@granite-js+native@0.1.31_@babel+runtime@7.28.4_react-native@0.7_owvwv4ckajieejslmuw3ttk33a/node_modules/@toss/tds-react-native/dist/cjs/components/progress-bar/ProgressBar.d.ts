import type { ViewProps } from 'react-native';
type Size = 'light' | 'normal' | 'bold';
export interface ProgressBarProps extends ViewProps {
    /**
     * @description 막대를 얼마나 채울지 정합니다. (0 ~ 100)
     */
    progress: number;
    /**
     * @description `ProgressBar`의 크기를 정합니다.
     */
    size: Size;
    /**
     * @description `ProgressBar`의 트랙에 채워지는 색상을 바꿉니다.
     */
    color?: string;
    /**
     * @description 애니메이션을 사용할지 여부를 결정합니다.
     * @default false
     */
    withAnimation?: boolean;
}
declare function ProgressBar({ progress, size, color, withAnimation, style, ...props }: ProgressBarProps): import("react/jsx-runtime").JSX.Element;
export default ProgressBar;
