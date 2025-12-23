import type { BuildConfig } from '@granite-js/plugin-core';
import { AsyncTransformStep } from '../../../../transformer/TransformPipeline';
interface TransformToHermesSyntaxStepConfig {
    dev: boolean;
    additionalSwcOptions?: BuildConfig['swc'];
}
export declare function createTransformToHermesSyntaxStep({ dev, additionalSwcOptions, }: TransformToHermesSyntaxStepConfig): AsyncTransformStep;
export {};
