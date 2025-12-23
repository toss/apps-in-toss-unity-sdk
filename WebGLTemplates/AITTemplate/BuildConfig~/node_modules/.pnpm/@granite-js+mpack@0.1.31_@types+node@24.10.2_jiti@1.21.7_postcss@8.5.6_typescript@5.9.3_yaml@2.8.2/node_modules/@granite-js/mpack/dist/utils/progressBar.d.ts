import type { BuildResult } from '@granite-js/plugin-core';
export interface BuildStatusProgressBar {
    start: (moduleCount: number, totalModuleCount: number) => void;
    update: (moduleCount: number, totalModuleCount?: number) => void;
    done: (result: BuildResult) => void;
    hide: () => void;
    remove: () => void;
}
export declare function createProgressBar(label: string): BuildStatusProgressBar;
export declare function cleanup(): void;
