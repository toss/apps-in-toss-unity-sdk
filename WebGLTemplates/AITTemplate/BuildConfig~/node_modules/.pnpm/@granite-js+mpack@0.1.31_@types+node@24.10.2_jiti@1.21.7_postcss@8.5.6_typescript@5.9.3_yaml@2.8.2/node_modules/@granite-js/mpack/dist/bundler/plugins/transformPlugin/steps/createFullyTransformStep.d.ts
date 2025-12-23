import * as babel from '@babel/core';
import { AsyncTransformStep } from '../../../../transformer/TransformPipeline';
interface FullyTransformStepConfig {
    dev: boolean;
    additionalBabelOptions?: babel.TransformOptions;
}
export declare function createFullyTransformStep({ dev, additionalBabelOptions, }: FullyTransformStepConfig): AsyncTransformStep;
export {};
