import TerminalReporter from './metro/src/lib/TerminalReporter';
import { getDefaultValues } from './metro-config/src/defaults';
import { loadConfig, mergeConfig } from './metro-config/src/loadConfig';
import Terminal from './metro-core/src/Terminal';
import InspectorProxy from './metro-inspector-proxy/src/InspectorProxy';
declare const vendorModules: {
    readonly metro: {
        readonly Metro: {
            runServer(config: any, options: any): Promise<any>;
            runBuild(config: any, options: any): Promise<any>;
        };
        readonly TerminalReporter: typeof TerminalReporter;
    };
    readonly 'metro-config': {
        readonly getDefaultValues: typeof getDefaultValues;
        readonly loadConfig: typeof loadConfig;
        readonly mergeConfig: typeof mergeConfig;
    };
    readonly 'metro-core': {
        readonly Terminal: typeof Terminal;
    };
    readonly 'metro-inspector-proxy': {
        readonly InspectorProxy: typeof InspectorProxy;
    };
};
export declare function getModule<Source extends keyof typeof vendorModules>(source: Source): (typeof vendorModules)[Source];
export {};
