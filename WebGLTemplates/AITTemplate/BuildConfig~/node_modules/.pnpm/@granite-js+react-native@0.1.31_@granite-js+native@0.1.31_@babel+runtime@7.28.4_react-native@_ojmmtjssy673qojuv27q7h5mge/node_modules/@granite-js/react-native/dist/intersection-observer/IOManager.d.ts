import IntersectionObserver, { IntersectionObserverOptions, IntersectionObserverEntry, Element } from './IntersectionObserver';
export type ObserverInstanceCallback = (inView: boolean, intersectionRatio: number) => void;
export interface ObserverInstance {
    readonly callback: ObserverInstanceCallback;
    readonly element: Element;
    readonly observerId: number;
    readonly observer: IntersectionObserver;
}
/**
 * @kind class
 * @name IOManager
 * @description A class that tracks the visibility of DOM elements using `IntersectionObserver` instances and executes callbacks when elements enter or leave the viewport.
 * This class makes it easy to manage multiple elements and execute custom logic based on the visibility status of each element.
 */
declare class IOManager {
    io: IntersectionObserver;
    observerId: number;
    instanceMap: Map<Element, ObserverInstance>;
    constructor(options: IntersectionObserverOptions);
    handleChange: (entries: IntersectionObserverEntry[]) => void;
    observe(element: Element, callback: ObserverInstanceCallback): ObserverInstance;
    unobserve(element: any): void;
}
export default IOManager;
