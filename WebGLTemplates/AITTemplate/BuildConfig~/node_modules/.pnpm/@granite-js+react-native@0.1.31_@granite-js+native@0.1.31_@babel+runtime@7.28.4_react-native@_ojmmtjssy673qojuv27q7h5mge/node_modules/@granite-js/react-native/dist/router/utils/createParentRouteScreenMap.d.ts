import type { RouteScreen } from '../types/RouteScreen';
/**
 * @name createParentRouteScreenMap
 * @description
 *   Finds a specific keyword in a given list of files and
 *   maps each page/layout to use its "nearest parent layout"
 */
export declare function createParentRouteScreenMap(routeScreens: RouteScreen[], keyword: string): Map<string, RouteScreen>;
