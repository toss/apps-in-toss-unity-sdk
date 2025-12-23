import { OnLoadArgs, PluginBuild } from 'esbuild';
/**
 * Register `onResolve` callback to mark the module as entry point module
 */
export declare function registerEntryPointMarker(build: PluginBuild): void;
/**
 * Register `onResolve` callback to resolve the virtual prelude script path
 *
 * This callback is used to resolve the virtual prelude script path without tree shaking.
 *
 * ```ts
 * import 'prelude:foo';
 * import 'prelude:bar';
 * import 'prelude:baz';
 * ```
 */
export declare function registerPreludeScriptResolver(build: PluginBuild): void;
export declare function isEntryPoint(args: OnLoadArgs): boolean;
/**
 * Returns string that inject prelude script at the top of the code
 *
 * ```ts
 * import 'prelude:foo';
 * import 'prelude:bar';
 * import 'prelude:baz';
 * // ...
 * ```
 */
export declare function injectPreludeScript(code: string, { preludeScriptPaths, }: {
    preludeScriptPaths: string[];
}): string;
