import type { CreateOverlayElement } from './types';
interface Props {
    overlayElement: CreateOverlayElement;
    onExit: () => void;
}
export interface OverlayControlRef {
    close: () => void;
}
export declare const OverlayController: import("react").ForwardRefExoticComponent<Props & import("react").RefAttributes<OverlayControlRef>>;
export {};
