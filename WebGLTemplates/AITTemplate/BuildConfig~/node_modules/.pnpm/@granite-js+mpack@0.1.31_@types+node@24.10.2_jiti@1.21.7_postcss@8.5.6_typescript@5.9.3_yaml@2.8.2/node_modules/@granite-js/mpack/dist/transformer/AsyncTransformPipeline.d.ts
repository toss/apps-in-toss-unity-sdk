import { TransformPipeline, TransformStepArgs, AsyncTransformStep, TransformStepResult } from './TransformPipeline';
export declare class AsyncTransformPipeline extends TransformPipeline<AsyncTransformStep> {
    transform(code: string, args: TransformStepArgs): Promise<TransformStepResult>;
}
