import type { ReactNode } from 'react';
import { CarouselItem } from './CarouselItem';
interface Props {
    children: ReactNode[];
    /** 캐러셀 아이템의 너비 */
    itemWidth?: number;
    /** 캐러셀 아이템 사이의 간격 */
    itemGap?: number;
    /** 캐러셀 아이템을 둘러싸는 패딩 */
    padding?: number;
    renderIndicators?: (data: {
        activeIndex: number;
        itemsCount: number;
    }) => ReactNode;
}
/**
 *
 * @example
 * <Carousel itemWidth={320}>
 *   <Carousel.Item>
 *     <View style={{ width: 320, height: 200, backgroundColor: 'red' }} />
 *   </Carousel.Item>
 *   <Carousel.Item>
 *     <View style={{ width: 320, height: 200, backgroundColor: 'blue' }} />
 *   </Carousel.Item>
 * </Carousel>
 * @note GestureHandlerRootView를 앱 최상위에 넣어야 안드로이드에서 정상적으로 동작합니다
 */
export declare function Carousel({ children, itemWidth, itemGap, padding, renderIndicators }: Props): import("react/jsx-runtime").JSX.Element;
export declare namespace Carousel {
    var Item: typeof CarouselItem;
}
export {};
