import { type ReactNode } from 'react';
/**
 * @category Constants
 * @name Children
 * @description
 * `Children` provides various components for controlling layouts related to child elements.
 *
 * @property {ChildrenGap} Gap - `Children.Gap` is a component used to add a specified component (gap) between multiple child elements when rendering.
 */
export declare const Children: {
    /**
     * @name Children.Gap
     * @description Renders a gap component between ReactNode children.
     */
    Gap: typeof ChildrenGap;
};
interface ChildrenGapProps {
    children: ReactNode;
    gap: ReactNode;
}
/**
 * @category Components
 * @name ChildrenGap
 * @description
 * `Children.Gap` is a useful component for maintaining consistent layouts by adding spacing (gap) between multiple child elements.
 * Using this component allows you to apply consistent spacing between child elements, resulting in a cleaner and more consistent UI design.
 *
 * @param {object} props - The `props` object passed to the component.
 * @param {ReactNode} props.children - The child components to be rendered.
 * @param {ReactNode} props.gap - The component to be placed between child elements. `gap` must be a valid React component.
 *
 * @throws {Error} An error occurs when `gap` is not a valid React component. `gap` must use `React.Element`.
 *
 * @example
 * ## When you want to add 16px spacing between child elements
 *
 * ```tsx
 * import { Children, Spacing } from '@granite-js/react-native';
 * import { Text } from 'react-native';
 *
 * function Component() {
 *   return (
 *     <Children.Gap gap={<Spacing size={16} />}>
 *       <Text>First</Text>
 *       <Text>Second</Text>
 *       <Text>Third</Text>
 *     </Children.Gap>
 *   );
 * }
 * ```
 */
declare function ChildrenGap({ children, gap }: ChildrenGapProps): import("react/jsx-runtime").JSX.Element;
export {};
