import { GranitePluginCore } from "@granite-js/plugin-core";

//#region src/types.d.ts
interface MicroFrontendPluginOptions extends MicroFrontendOptions {
  /**
   * Path to react-native base directory.
   *
   * Defaults to project's `react-native` directory.
   */
  reactNativeBasePath?: string;
}
interface MicroFrontendOptions {
  /**
   * Container name
   */
  name: string;
  /**
   * Configuration for the remote field to emulate the internal `importLazy` behavior
   */
  remote?: RemoteConfig;
  /**
   * Shared modules config
   */
  shared?: SharedConfig | string[];
  /**
   * Configuration for exposing modules to other containers
   */
  exposes?: ExposeConfig;
}
interface RemoteConfig {
  /**
   * Host name
   */
  host: string;
  /**
   * Port number
   */
  port: number;
}
interface SharedConfig {
  [lib: string]: {
    /**
     * Whether the module is eager
     *
     * Specifies whether the module is eager; if true, it's bundled with the host, otherwise loaded from the shared registry
     */
    eager?: boolean;
  };
}
interface ExposeConfig {
  [exposePath: string]: string;
} //#endregion
//#region src/microFrontendPlugin.d.ts
declare const microFrontendPlugin: (options: MicroFrontendPluginOptions) => Promise<GranitePluginCore>;

//#endregion
export { MicroFrontendPluginOptions, microFrontendPlugin as microFrontend };