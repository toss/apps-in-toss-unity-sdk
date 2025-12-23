import { LayoutRectangle, NativeScrollEvent } from 'react-native';
export interface Root {
    /**
     * NodeHandle of the target component
     */
    node: any;
    /**
     * Whether horizontal scroll is enabled
     */
    horizontal: boolean;
    /**
     * Scroll event of the target component
     */
    current: NativeScrollEvent;
    onLayout?: () => void;
    onScroll?: (event: NativeScrollEvent) => void;
}
export interface Element {
    inView: boolean;
    intersectionRatio: number;
    layout: LayoutRectangle;
    measureLayout: (node: any, callback: (x: number, y: number, width: number, height: number) => void) => void;
    onLayout?: () => void;
}
export interface IntersectionObserverEntry {
    target: Element;
    isIntersecting: boolean;
    intersectionRatio: number;
}
export interface RootMargin {
    left?: number;
    right?: number;
    top?: number;
    bottom?: number;
}
export interface IntersectionObserverOptions {
    /**
     * Information about the component that wraps the element
     */
    root: Root;
    rootMargin?: RootMargin;
    threshold?: number | number[];
}
export type IntersectionObserverCallback = (entries: IntersectionObserverEntry[]) => void;
export declare const defaultRootMargin: RootMargin;
export declare const defaultThreshold = 0;
/**
 * @kind class
 * @name IntersectionObserver
 * @description
 * IntersectionObserver implemented for React Native environment.
 *
 * @param {IntersectionObserverCallback} callback - Callback function that is called when the visibility state of the target element changes.
 * @param {IntersectionObserverOptions} options - Options object that controls the behavior of IntersectionObserver.
 */
declare class IntersectionObserver {
    protected callback: IntersectionObserverCallback;
    protected options: IntersectionObserverOptions;
    protected targets: Element[];
    constructor(callback: IntersectionObserverCallback, options: IntersectionObserverOptions);
    protected measureTarget: (target: Element) => void;
    protected handleLayout: () => void;
    protected handleScroll: () => void;
    observe(target: Element): void;
    unobserve(target: Element): void;
}
export default IntersectionObserver;
