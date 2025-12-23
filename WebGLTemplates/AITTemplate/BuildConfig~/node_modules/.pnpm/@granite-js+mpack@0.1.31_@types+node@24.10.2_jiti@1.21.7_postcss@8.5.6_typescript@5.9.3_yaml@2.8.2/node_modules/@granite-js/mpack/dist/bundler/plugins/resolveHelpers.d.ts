import type { OnResolveArgs, PluginBuild, ResolveOptions } from 'esbuild';
export declare function createNonRecursiveResolver(build: PluginBuild): (args: OnResolveArgs, options: ResolveOptions) => Promise<import("esbuild").ResolveResult> | null;
export declare function isResolved(args: OnResolveArgs): any;
