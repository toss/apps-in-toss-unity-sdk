import { ComponentType, PropsWithChildren } from 'react';
import type { InitialProps } from '../initial-props';
import type { RouterProps, RequireContext } from '../router';
export interface GraniteProps {
    /**
     * @description
     * The name of the app.
     */
    appName: string;
    /**
     * @description
     * The context of the app.
     *
     * @TODO Hide context
     */
    context: RequireContext;
    /**
     * @description
     * Configuration object to be passed to the router.
     */
    router?: RouterProps;
    /**
     * @description
     * The initial scheme of the app.
     */
    initialScheme?: string;
    /**
     * @description
     * The function to set the iOS swipe gesture enabled.
     */
    setIosSwipeGestureEnabled?: ({ isEnabled }: {
        isEnabled: boolean;
    }) => void;
    /**
     * @description
     * The function to provide the initial URL to router.
     * @param initialScheme The initial scheme of the app.
     * @returns
     */
    getInitialUrl?: (initialScheme: string) => string | undefined | Promise<string | undefined>;
}
/**
 * @public
 * @category Core
 * @name Granite
 * @description
 *
 * @property {RegisterService} registerApp - This function sets up the basic environment for your service and helps you start service development quickly without needing complex configuration. By just passing `appName`, you can immediately use various features such as file-based routing, query parameter handling, and back navigation control.
 *
 * The features provided by the `Granite.registerApp` function are as follows:
 * - Routing: URLs are automatically mapped according to file paths. It works similarly to Next.js's file-based routing. For example, the `/my-service/pages/index.ts` file can be accessed at `scheme://my-service`, and the `/my-service/pages/home.ts` file can be accessed at `scheme://my-service/home`.
 * - Query Parameters: You can easily use query parameters received through URL schemes. For example, you can receive a `referrer` parameter and log it.
 * - Back Navigation Control: You can control back navigation events. For example, when a user presses back on a screen, you can show a dialog or close the screen.
 * - Screen Visibility: You can determine whether a screen is visible or hidden from the user. For example, you can use this value to handle specific actions when a user leaves for the home screen.
 *
 * @example
 *
 * ### Example of creating with the `Granite` component
 *
 * ```tsx
 * import { PropsWithChildren } from 'react';
 * import { Granite, InitialProps } from '@granite-js/react-native';
 * import { context } from '../require.context';
 *
 * function AppContainer({ children }: PropsWithChildren<InitialProps>) {
 *  return <>{children}</>;
 * }
 *
 * export default Granite.registerApp(AppContainer, {
 *  appName: 'my-app',
 *  context,
 * });
 *
 * ```
 */
export declare const Granite: {
    registerApp(AppContainer: ComponentType<PropsWithChildren<InitialProps>>, { appName, context, router, initialScheme, setIosSwipeGestureEnabled, getInitialUrl }: GraniteProps): (initialProps: InitialProps) => JSX.Element;
    registerHostApp(AppContainer: ComponentType<PropsWithChildren<InitialProps>>, { appName }: Pick<GraniteProps, "appName">): (initialProps: InitialProps) => JSX.Element;
    readonly appName: string;
};
