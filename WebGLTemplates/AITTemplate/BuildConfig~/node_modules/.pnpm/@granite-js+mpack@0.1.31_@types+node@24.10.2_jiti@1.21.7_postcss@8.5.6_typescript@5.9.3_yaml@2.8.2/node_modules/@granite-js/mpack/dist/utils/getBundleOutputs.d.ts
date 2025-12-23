import * as esbuild from 'esbuild';
export declare function getBundleOutputs(outputFile: string, buildResult: esbuild.BuildResult): {
    source: esbuild.OutputFile | undefined;
    sourcemap: esbuild.OutputFile | undefined;
};
