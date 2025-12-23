import type { BuildConfig } from '@granite-js/plugin-core';
import execa from 'execa';
export type FixtureTestContext = Awaited<ReturnType<typeof initializeFixture>>;
export declare function initializeFixture(): Promise<{
    dir: string;
    $: (command: string, args?: string[], options?: execa.Options) => Promise<execa.ExecaReturnValue<string>>;
    installDependencies: (...deps: string[]) => Promise<void>;
    readFile: (relativePath: string) => Promise<string>;
    writeFile: (relativePath: string, content: string) => Promise<void>;
    loadFixtures: (basePath: string, fixtureName: string, placeholders?: Record<string, string>) => Promise<void>;
    buildWithConfig: (config: BuildConfig, options?: execa.Options) => Promise<void>;
    resolvePath: (p: string) => string;
    cleanup: () => Promise<void>;
}>;
