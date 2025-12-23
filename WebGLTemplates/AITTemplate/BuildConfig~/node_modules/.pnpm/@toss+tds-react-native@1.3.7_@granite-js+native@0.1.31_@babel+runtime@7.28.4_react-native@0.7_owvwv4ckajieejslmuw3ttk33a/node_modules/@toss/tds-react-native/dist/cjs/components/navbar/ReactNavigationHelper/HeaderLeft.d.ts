import type { PropsWithChildren } from 'react';
type HeaderLeftProps = PropsWithChildren<{
    /**
     * @description
     * 지정하지 않으면 각 OS별 기본값이 적용됩니다.
     * 10: IOS 기본 margin,
     * 16: Android 기본 margin,
     * 24: 공통 margin
     */
    marginLeft?: 10 | 16 | 24;
}>;
declare function HeaderLeft(props: HeaderLeftProps): JSX.Element;
export { HeaderLeft };
