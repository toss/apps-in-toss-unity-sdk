import { AsyncTransformStep } from '../../../../transformer/TransformPipeline';
interface StripFlowStepConfig {
    dev: boolean;
}
export declare function createStripFlowStep(config: StripFlowStepConfig): AsyncTransformStep;
export {};
