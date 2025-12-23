import { FC, ReactNode } from 'react';
import { RouteScreen } from '../types/RouteScreen';
export type LayoutScreen = FC<{
    children: ReactNode;
}>;
/**
 * Starting from the layout mapped to a specific path, traverses up to parent layouts,
 * creates an array in the order of "innermost → outermost",
 * then builds up the final wrapper component using reduce.
 *
 * Example) map:
 *   './item/detail' => { component: ItemLayout, path: './item/_layout' }
 *   './item/_layout' => { component: RootLayout, path: './_layout' }
 *
 * => layoutChain = [ItemLayout, RootLayout]
 * => Final rendering result: <RootLayout><ItemLayout>children</ItemLayout></RootLayout>
 */
export declare function mergeParentLayoutScreen(screens: Map<string, RouteScreen>, path: string): LayoutScreen;
