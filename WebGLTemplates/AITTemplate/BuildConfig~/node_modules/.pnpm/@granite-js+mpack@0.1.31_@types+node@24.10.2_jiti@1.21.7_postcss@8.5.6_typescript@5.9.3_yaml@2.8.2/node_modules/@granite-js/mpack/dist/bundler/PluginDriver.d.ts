import type { Hooks, INTERNAL__Id, Plugin, PluginHooks } from '../types';
export declare class PluginDriver {
    private id;
    private plugins;
    constructor(id: INTERNAL__Id);
    addPlugin(plugin: Plugin): void;
    hookSync<H extends Hooks>(hookName: H, parameters: Parameters<PluginHooks[H]>): void;
    private runHook;
}
