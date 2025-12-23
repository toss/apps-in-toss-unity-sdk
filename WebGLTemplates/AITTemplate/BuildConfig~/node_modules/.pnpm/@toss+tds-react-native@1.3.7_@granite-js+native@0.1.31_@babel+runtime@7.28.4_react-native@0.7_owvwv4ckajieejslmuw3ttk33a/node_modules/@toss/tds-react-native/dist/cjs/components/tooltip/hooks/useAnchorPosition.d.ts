import type { Size } from '../types';
interface AnchorPositionProps {
    anchorWidth: number;
    tooltipWidth?: number;
    anchorPositionByRatio?: number;
    defaultPosition?: number;
    size?: Size;
}
export declare function useAnchorPosition({ anchorWidth, tooltipWidth, anchorPositionByRatio, defaultPosition, size, }: AnchorPositionProps): number;
export {};
