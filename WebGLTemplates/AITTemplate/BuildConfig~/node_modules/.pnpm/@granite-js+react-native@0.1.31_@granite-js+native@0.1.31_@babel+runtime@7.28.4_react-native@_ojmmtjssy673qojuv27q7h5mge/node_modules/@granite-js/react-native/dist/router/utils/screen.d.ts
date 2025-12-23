import { RequireContext, RouteScreen } from '../types';
/**
 * @kind function
 * @name getRouteScreens
 * @description
 * Gets screens from the pages folder.
 *
 * @param {RequireContext} context - Object containing information about screens in Router
 * @returns {RouteScreen[]} screens - Returns a list of screens that can be navigated to.
 *
 * @example
 * ```tsx
 * import { getRouteScreens } from '@granite-js/react-native';
 *
 * const context = require.context('../pages');
 * const screens = getRouteScreens(context);
 * ```
 */
export declare function getRouteScreens(context: RequireContext): RouteScreen[];
/**
 * @kind function
 * @name getScreenPathMapConfig
 * @description Maps paths of screens.
 *
 * @param {RouteScreen[]} routeScreens - List of screens that can be navigated to
 */
export declare function getScreenPathMapConfig(routeScreens: RouteScreen[]): ScreenPath;
/**
 * @name ScreenPath
 * @description
 * Type representing screen paths.
 *
 * @typedef {Record<string, { path?: string }>} ScreenPath
 */
export type ScreenPath = Record<string, {
    path?: string;
}>;
