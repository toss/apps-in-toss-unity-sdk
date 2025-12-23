import { TouchableOpacityProps } from 'react-native';
interface BackButtonProps extends TouchableOpacityProps {
    tintColor?: string;
    onPress?: () => void;
}
/**
 * @public
 * @category UI
 * @name BackButton
 * @description
 * A back button component. This component is primarily used to implement functionality for returning to the previous screen in navigation headers or custom screen tops. If no `onPress` handler is set, it won't perform any action.
 *
 * @param {string} [props.tintColor] - The color of the button icon. You can use CSS color strings.
 * @param {() => void} [props.onPress] - A function to execute when the button is pressed. For example, you can add a function to return to the previous screen.
 *
 * @returns {ReactElement} Returns a back button component.
 * @example
 *
 * ### Example of directly passing a handler to a back button and setting a function to close the view
 *
 * ```tsx
 * import { createNavigationContainerRef, useNavigation } from '@granite-js/native/@react-navigation/native';
 * import { BackButton, useRouterBackHandler } from '@granite-js/react-native';
 * import { useEffect } from 'react';
 *
 * const navigationContainerRef = createNavigationContainerRef();
 *
 * function MyBackButton() {
 *   const navigation = useNavigation();
 *
 *   const { handler } = useRouterBackHandler({
 *     navigationContainerRef,
 *     onClose: () => {
 *       // close the view
 *     },
 *   });
 *
 *   useEffect(() => {
 *     navigation.setOptions({
 *       headerLeft: () => <BackButton onPress={handler} />,
 *     });
 *   }, [handler, navigation]);
 *
 *   return <></>;
 * }
 */
declare function BackButton({ tintColor, onPress }: BackButtonProps): import("react/jsx-runtime").JSX.Element;
export { BackButton };
