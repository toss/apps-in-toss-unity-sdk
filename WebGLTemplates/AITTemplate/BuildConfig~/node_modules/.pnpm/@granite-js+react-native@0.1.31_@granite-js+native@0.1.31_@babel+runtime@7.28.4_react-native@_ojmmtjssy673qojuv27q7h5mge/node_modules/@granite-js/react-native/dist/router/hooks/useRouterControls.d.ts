import { type ComponentType, type PropsWithChildren } from 'react';
import { RequireContext } from '../types/RequireContext';
export interface RouterControlsConfig {
    prefix: string;
    initialScheme: string;
    context: RequireContext;
    getInitialUrl?: (initialScheme: string) => string | undefined | Promise<string | undefined>;
    screenContainer?: ComponentType<PropsWithChildren<any>>;
}
export declare function useRouterControls({ prefix, context, screenContainer: ScreenContainer, getInitialUrl, initialScheme, }: RouterControlsConfig): {
    Screens: import("react/jsx-runtime").JSX.Element[];
    linkingOptions: import("@react-navigation/native").LinkingOptions<{}>;
};
