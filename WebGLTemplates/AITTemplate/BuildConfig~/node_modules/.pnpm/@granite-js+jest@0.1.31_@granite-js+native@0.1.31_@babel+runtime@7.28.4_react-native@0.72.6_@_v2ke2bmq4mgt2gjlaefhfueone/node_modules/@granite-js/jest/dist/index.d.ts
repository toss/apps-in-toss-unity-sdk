import { Config } from 'jest';

interface ReactNativeSetupOptions {
    rootDir: string;
}
declare function setupReactNative({ rootDir }: ReactNativeSetupOptions): void;

declare const setup$1: typeof setupReactNative;

interface Export {
    config: Config;
    setup: typeof setup$1;
}
declare const exported: Export;
declare const setup: typeof setupReactNative;
declare const config: ((extraSettings: Config) => Config) & {
    testEnvironment: string;
    testEnvironmentOptions: {
        customExportConditions: string[];
    };
    preset: string;
    transformIgnorePatterns: never[];
    setupFilesAfterEnv: string[];
    testPathIgnorePatterns: string[];
    testMatch: string[];
    moduleDirectories: string[];
    maxWorkers: number;
};

export { config, exported as default, setup };
