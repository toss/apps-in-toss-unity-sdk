import type { Layout } from '../../../types';
import type { Length } from '../../../types/motion-input/motion-input-values';
export declare function getPerspective(_value: Length | ((width: number) => number), { layout, beforeValue }: {
    layout: Layout;
    beforeValue: number;
}): number;
