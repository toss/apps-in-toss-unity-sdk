import { type ComponentProps } from 'react';
import { ScrollView } from 'react-native';
import { RootMargin } from './IntersectionObserver';
export interface IOComponentProps {
    rootMargin?: RootMargin;
}
/**
 * @category Functions
 * @kind function
 * @name withIO
 * @description
 * A Higher-Order Component (HoC) that wraps a component with `IOContext` to enable Intersection Observer functionality.
 *
 * @argument
 * @param {React.ComponentType} [BaseComponent] - Callback function that is called when the component is mounted.
 * @param {string[]} [methods] - List of event handler names from BaseComponent to be bound.
 * @returns {React.ComponentType} - Returns a wrapped component that can use Intersection Observer functionality.
 */
declare function withIO<CompProps extends Pick<ComponentProps<typeof ScrollView>, 'horizontal' | 'scrollEventThrottle' | 'onContentSizeChange' | 'onLayout' | 'onScroll'>>(BaseComponent: React.ComponentType<CompProps>, methods: string[]): typeof BaseComponent;
export default withIO;
