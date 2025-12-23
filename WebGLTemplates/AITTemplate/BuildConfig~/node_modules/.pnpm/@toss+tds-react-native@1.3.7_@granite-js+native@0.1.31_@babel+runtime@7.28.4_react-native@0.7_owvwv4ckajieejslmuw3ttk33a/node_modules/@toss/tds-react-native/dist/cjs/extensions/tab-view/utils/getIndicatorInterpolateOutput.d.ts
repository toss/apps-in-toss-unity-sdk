/**
 * 탭의 길이들을 이용해 indicator 의 변화량을 구합니다.
 * - 첫번째 탭은 TAB_LIST_PADDING 을 왼쪽에 한 개 갖습니다.
 * - 첫번째와 두번째 탭 사이에는 TAB_ITEM_MARGIN 두 개 갖습니다.
 *
 * ```plaintext
 * `|`: 탭 구분
 * `^`: tabListPadding
 * `o`: tabItemMargin
 *
 * |o^TAB1^|^TAB2^|
 *
 * - TAB1의 indciator 위치: `o` + `^`
 * - TAB2의 indciator 위치: <TAB1의 indicator 위치> + `<TAB1의 width>` + `^` + `^`
 * - ...
 *
 * ```
 */
export declare function getIndicatorInterpolateOutput(tabWidths: number[], fluid: boolean): number[];
