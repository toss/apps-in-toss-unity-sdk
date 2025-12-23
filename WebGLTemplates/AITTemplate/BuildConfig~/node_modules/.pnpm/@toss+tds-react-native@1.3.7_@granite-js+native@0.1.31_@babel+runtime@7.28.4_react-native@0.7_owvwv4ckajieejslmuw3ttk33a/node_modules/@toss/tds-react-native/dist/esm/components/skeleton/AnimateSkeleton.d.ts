import type { PropsWithChildren } from 'react';
interface AnimateSkeletonProps extends PropsWithChildren<{}> {
    /**
     * @description 단위는 ms입니다. 500으로 지정시 스켈레톤을 미노출하여 화면전환을 최소화합니다
     */
    delay: 0 | 500;
    withGradient: boolean;
    withShimmer: boolean;
}
declare function AnimateSkeleton({ children, delay, withGradient, withShimmer }: AnimateSkeletonProps): import("react/jsx-runtime").JSX.Element;
export default AnimateSkeleton;
