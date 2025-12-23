import { NavigationContainer, NavigationContainerRefWithCurrent, ParamListBase, RouteProp } from '@granite-js/native/@react-navigation/native';
import { NativeStackNavigationOptions } from '@granite-js/native/@react-navigation/native-stack';
import { ComponentProps, ComponentType, PropsWithChildren, ReactElement } from 'react';
export type RouterProps = StackNavigatorProps & NavigationContainerProps;
interface StackNavigatorProps {
    /**
     * @name navigationContainerRef
     * @description
     * You can create and pass a NavigationContainerRef from @react-navigation/native externally. This allows external control of the router.
     */
    navigationContainerRef?: NavigationContainerRefWithCurrent<any>;
    /**
     * @name defaultScreenOption
     * @description
     * Default options for screens. You can set options to be applied commonly to screens, such as title or headerStyle.
     */
    defaultScreenOption?: NativeStackNavigationOptions | ((props: {
        route: RouteProp<ParamListBase>;
        navigation: any;
    }) => NativeStackNavigationOptions);
    /**
     * @name screenContainer
     * @description
     * Container component that wraps each Screen component.
     */
    screenContainer?: ComponentType<PropsWithChildren<any>>;
}
type NavigationContainerProps = Pick<ComponentProps<typeof NavigationContainer>, 'ref' | 'documentTitle' | 'fallback' | 'onReady' | 'onUnhandledAction' | 'onStateChange' | 'initialState'>;
/**
 * @category Components
 * @kind function
 * @name Router
 * @description
 * Router component for page navigation in React Native environment.
 * Automatically assigns appropriate paths to screens based on file naming rules in pages/*.
 * Using this component, you can manage screens in a way similar to Next.js's file-based routing.
 *
 * @param {string} prefix Prefix to use when the scheme is executed. For example, to enter 'scheme://my-service/intro', you need to set 'scheme://my-service' as the prefix.
 * @param {RequireContext} context Object containing information about screens for file-based routing.
 * @param {NavigationContainerRefWithCurrent<any>} [navigationContainerRef] You can create and pass a NavigationContainerRef from @react-navigation/native externally. This allows external control of the router.
 * @param {NativeStackNavigationOptions | ((props: { route: RouteProp<ParamListBase>; navigation: any }) => NativeStackNavigationOptions)} [defaultScreenOption] Default options for screens. You can set options to be applied commonly to screens, such as title or headerStyle.
 * @param {boolean} [canGoBack=true] Whether navigation back is possible. Default is true, and when set to true, you can use the back gesture or back button from @react-navigation/native.
 * @param {() => void} [onBack] Callback function called when the user presses the back button or uses the back gesture. For example, you can set it to log when the user presses the back button.
 * @param {ComponentType<{ children: ReactNode }>} [container=Fragment] Container component that wraps the Navigator from @react-navigation/native.
 * @param {NavigationContainerProps} [navigationContainerProps] - You can set props to be passed to NavigationContainer from @react-navigation/native.
 *
 * @returns {ReactElement} - Returns the router component.
 * @example
 * ```tsx
 * import { Router } from '@granite-js/react-native';
 * import { context } from '../require.context';
 *
 * function App() {
 *  return <Router context={context} prefix={'scheme://testbench'} />;
 * }
 * ```
 */
export declare function Router({ prefix, context, container: Container, initialProps, initialScheme, navigationContainerRef, defaultScreenOption, screenContainer, setIosSwipeGestureEnabled, getInitialUrl, ...navigationContainerProps }: InternalRouterProps & RouterProps): ReactElement;
export {};
