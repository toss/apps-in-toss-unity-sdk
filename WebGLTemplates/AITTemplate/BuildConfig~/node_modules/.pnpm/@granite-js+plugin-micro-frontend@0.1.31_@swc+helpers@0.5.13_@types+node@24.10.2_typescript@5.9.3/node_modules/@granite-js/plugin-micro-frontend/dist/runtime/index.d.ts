//#region src/types.d.ts

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
//#region src/runtime/types.d.ts
declare global {
  var __MICRO_FRONTEND__: RuntimeContext;
}
interface RuntimeContext {
  __INSTANCES__: Container[] & Record<string, number>;
  __SHARED__: SharedModuleRegistry;
}
interface Container {
  name: string;
  exposeMap: Record<string, Module>;
  config: {
    remote?: RemoteConfig;
    shared?: SharedConfig;
  };
}
interface SharedModuleRegistry {
  [libName: string]: {
    get: () => Module;
    loaded: boolean;
  };
}
type Module = any;

//#endregion
//#region src/runtime/createContainer.d.ts
declare function createContainer(name: string, config: {
  remote?: RemoteConfig;
  shared?: SharedConfig;
  exposes?: ExposeConfig;
}): Container;

//#endregion
//#region src/runtime/registerShared.d.ts
declare function registerShared(libName: string, module: Module): void;

//#endregion
//#region src/runtime/exposeModule.d.ts
declare function exposeModule(container: Container, exposeName: string, module: Module): void;

//#endregion
//#region src/runtime/utils.d.ts
declare function getContainer(instanceName: string): Container | null;
declare function parseRemotePath(remotePath: string): {
  remoteName: string;
  modulePath: string;
  fullRequest: string;
};
declare function importRemoteModule(remoteRequestPath: string): any;

//#endregion
export { Container, Module, RuntimeContext, SharedModuleRegistry, createContainer, exposeModule, getContainer, importRemoteModule, parseRemotePath, registerShared };