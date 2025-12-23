/**
 * x, y 에 기준점을 주면 그에 맞는 translateX, translateY 가 반환됩니다.
 * react-native 에는 transformOrigin 이 없어 우회하여 구현합니다.
 *
 * 예를들어, x=100, y=100인 요소의 x=0.5, y=1 을 기준점으로 갖게하는 translateX, translateY 반환
 * transform property 순서는 [Y0, X0, <Transform>, X1, Y1]
 * @example
 * ```ts
 * const {translateX, translateY} = transformOriginXY({ x: 0.5, y: 1, width: 100, height: 100 });
 *
 *
 *  <View
 *    style={{
 *        transform: [
 *          { translateY: translateY[0] },
 *          { translateX: translateX[0] },
 *          { scale: 1 },
 *          { translateX: translateX[1] },
 *          { translateY: translateY[1] }
 *        ]
 *    }}
 *  />
 * ```
 * @returns
 */
export declare function transformOriginXY({ x, y, width, height }: {
    x: number;
    y: number;
    width: number;
    height: number;
}): {
    translateX: [number, number];
    translateY: [number, number];
};
