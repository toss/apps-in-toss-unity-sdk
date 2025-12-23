import { type PropsWithChildren } from 'react';
import { InitialProps } from '../../initial-props';
export declare const InitialPropsContext: import("react").Context<InitialProps | null>;
export declare function InitialPropsProvider({ children, initialProps }: PropsWithChildren<{
    initialProps: InitialProps;
}>): import("react/jsx-runtime").JSX.Element;
/**
 * @public
 * @name useInitialProps
 * @category Core
 * @description Provides initial data passed from the native platform (Android or iOS) when entering a specific screen in React Native apps. This data can be used to immediately apply themes or user settings right after app launch. For example, you can receive dark mode settings from the native platform and apply dark mode immediately when the React Native app starts.
 * @returns {InitialProps} Initial data for the app
 * @example
 *
 * ### Checking dark mode status with initial data
 *
 * ```tsx
 * import { useInitialProps } from '@granite-js/react-native';
 *
 * function Page() {
 *   const initialProps = useInitialProps();
 *   // 'light' or 'dark'
 *   console.log(initialProps.initialColorPreference);
 *   return <></>;
 * }
 * ```
 */
export declare function useInitialProps<T extends InitialProps>(): T;
