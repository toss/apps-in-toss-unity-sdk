import type { ReactElement } from 'react';
import type { TxtProps } from '../txt';
export interface Texts1RowProps {
    /**
     * @description 가급적 string 타입을 사용해주시고, 커스텀이 필요할 경우 topProps를 사용해주세요.
     * */
    top: ReactElement | string | undefined | null;
    topProps?: Omit<TxtProps, 'children'>;
}
export interface Texts2RowProps extends Texts1RowProps {
    /**
     * @description 가급적 string 타입을 사용해주시고, 커스텀이 필요할 경우 bottomProps를 사용해주세요.
     * */
    bottom: ReactElement | string | undefined | null;
    bottomProps?: Omit<TxtProps, 'children'>;
}
export interface Texts3RowProps extends Texts2RowProps {
    /**
     * @description 가급적 string 타입을 사용해주시고, 커스텀이 필요할 경우 middleProps를 사용해주세요.
     * */
    middle: ReactElement | string | undefined | null;
    middleProps?: Omit<TxtProps, 'children'>;
}
