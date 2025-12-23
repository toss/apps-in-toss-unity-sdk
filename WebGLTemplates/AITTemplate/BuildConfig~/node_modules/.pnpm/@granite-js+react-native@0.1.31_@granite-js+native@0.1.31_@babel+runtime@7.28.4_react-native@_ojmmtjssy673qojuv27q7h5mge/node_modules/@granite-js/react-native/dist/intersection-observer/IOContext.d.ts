import IOManager from './IOManager';
export interface IOContextValue {
    manager: null | IOManager;
}
/**
 * @name IOContext
 * @description Context that shares the IOManager instance.
 */
declare const IOContext: import("react").Context<IOContextValue>;
export default IOContext;
