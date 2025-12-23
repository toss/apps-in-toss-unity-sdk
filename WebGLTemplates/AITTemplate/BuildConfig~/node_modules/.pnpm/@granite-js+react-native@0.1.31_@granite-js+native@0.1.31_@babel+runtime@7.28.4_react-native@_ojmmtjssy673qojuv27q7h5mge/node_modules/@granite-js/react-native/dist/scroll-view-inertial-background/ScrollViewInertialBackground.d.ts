interface ScrollViewInertialBackgroundProps {
    topColor?: string;
    bottomColor?: string;
    spacer?: number;
}
/**
 * @public
 * @category UI
 * @name ScrollViewInertialBackground
 * @description
 * Adds background colors to the top and bottom spaces of iOS `ScrollView` content to provide a natural visual effect when scrolling.
 * In iOS, when scrolling reaches the end, a slight bouncing effect occurs, known as the [Bounce effect](https://medium.com/@wcandillon/ios-bounce-list-effect-with-react-native-5102e3a83999). Setting background colors in the spaces above and below the content can provide a more consistent user experience.
 *
 * @param {object} [props] - `props` object passed to the component.
 * @param {string} [props.topColor] - Background color to apply to the top area of the scroll. Default is `adaptive.background` which is automatically applied based on the system theme.
 * @param {string} [props.bottomColor] - Background color to apply to the bottom area of the scroll. Default is `adaptive.background` which is automatically applied based on the system theme.
 * @param {number} [props.spacer] - Specifies the size of the space between the top and bottom areas where the background color is applied. Default uses the screen height obtained from [`useWindowDimensions`](https://reactnative.dev/docs/next/usewindowdimensions).
 *
 * @example
 *
 * ### Adding background colors to the top and bottom of a scroll view
 *
 * Adds red background color to the top and blue to the bottom of the scroll view. The background color is applied to areas outside the scroll.
 *
 * ```tsx
 * import { ScrollView, View, Text } from 'react-native';
 * import { ScrollViewInertialBackground } from '@granite-js/react-native';
 *
 * const dummies = Array.from({ length: 20 }, (_, i) => i);
 *
 * export function InertialBackgroundExample() {
 *   return (
 *     <ScrollView>
 *       <ScrollViewInertialBackground topColor="red" bottomColor="blue" />
 *       {dummies.map((i) => (
 *         <View
 *           key={`dummy-${i}`}
 *           style={{ width: '100%', height: 100, borderBottomColor: 'black', borderBottomWidth: 1 }}
 *         >
 *           <Text>Try scrolling.</Text>
 *         </View>
 *       ))}
 *     </ScrollView>
 *   );
 * }
 * ```
 */
export declare function ScrollViewInertialBackground({ topColor, bottomColor, spacer: _spacer, }: ScrollViewInertialBackgroundProps): import("react/jsx-runtime").JSX.Element;
export {};
