import { TabItem } from './TabItem';
import type { BaseTabProps } from './type';
export type TabProps = {
    /**
     * 이 값이 `true`일 때 각 아이템의 너비가 글자 수에 맞춰져요. 아이템의 전체 크기가 `Tab`의 컨테이너를 넘어가면 가로 스크롤이 생겨요. `false`라면 최대 4개의 아이템 사용을 권장해요.
     *
     * @default false
     */
    fluid?: boolean;
} & BaseTabProps;
export declare function Tab({ fluid, ...tabProps }: TabProps): import("react/jsx-runtime").JSX.Element;
export declare namespace Tab {
    var Item: typeof TabItem;
}
