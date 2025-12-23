export interface TransformStepArgs {
    path: string;
}
export interface TransformStepContext {
    key: string;
    mtimeMs: number;
}
export interface TransformStepResult {
    code: string;
    done?: boolean;
}
interface TransformStep<TransformResult> {
    (code: string, args: TransformStepArgs, context: TransformStepContext): TransformResult;
    name?: string;
}
interface TransformStepConfig {
    /**
     * step 을 실행하기 위한 조건 (기본값: 항상 실행)
     */
    conditions?: Array<(code: string, path: string) => boolean>;
    /**
     * 현재 step 이 실행된 경우 done 처리 여부 (기본값: false)
     */
    skipOtherSteps?: boolean;
}
export type AsyncTransformStep = TransformStep<Promise<TransformStepResult>>;
export declare abstract class TransformPipeline<Step extends TransformStep<unknown>> {
    protected _beforeStep?: Step;
    protected _afterStep?: Step;
    protected steps: Array<[Step, TransformStepConfig | null]>;
    beforeStep(step: Step): this;
    afterStep(step: Step): this;
    addStep(step: Step, config?: TransformStepConfig): this;
    getStepContext(args: TransformStepArgs): Promise<TransformStepContext>;
    abstract transform(code: string, args: TransformStepArgs): ReturnType<Step>;
}
export {};
