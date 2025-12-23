import { AsyncTransformStep } from '../../../../transformer/TransformPipeline';
interface CacheStepConfig {
    id: string;
    enabled: boolean;
}
export declare function createCacheSteps(config: CacheStepConfig): {
    beforeTransform: AsyncTransformStep;
    afterTransform: AsyncTransformStep;
};
export {};
