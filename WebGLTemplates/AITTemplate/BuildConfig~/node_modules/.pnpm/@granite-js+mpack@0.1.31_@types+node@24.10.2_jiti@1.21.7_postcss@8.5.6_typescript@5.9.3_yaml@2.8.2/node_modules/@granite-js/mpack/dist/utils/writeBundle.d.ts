import type { BundleData } from '@granite-js/plugin-core';
import type { Metafile } from 'esbuild';
export declare function writeBundle(outputPath: string, { source, sourcemap }: BundleData): Promise<void>;
export declare function writeMetafile(outputPath: string, metafile: Metafile): Promise<void>;
