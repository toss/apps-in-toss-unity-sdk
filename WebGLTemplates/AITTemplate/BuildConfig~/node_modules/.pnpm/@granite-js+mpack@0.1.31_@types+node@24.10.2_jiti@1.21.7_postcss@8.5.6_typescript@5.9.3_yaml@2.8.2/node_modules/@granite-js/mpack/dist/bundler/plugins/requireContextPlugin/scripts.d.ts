interface RequireContextModule {
    moduleIndex: number;
    absolutePath: string;
    relativePath: string;
}
/**
 * require context export 스크립트 반환
 *
 * ```js
 * export const context = require.context('../path'); // A
 * export default require.context('../path'); // B
 * ```
 *
 * 형태의 코드를 아래와 같이 변경
 *
 * ```js
 * // Sample
 * import __context__ from 'require-context:../path';
 *
 * export var context = __context__; // A
 * export default __context__; // B
 * ```
 */
export declare function toRequireContextExportScript(content: string): string;
/**
 * require context 스크립트
 *
 * ```js
 * // Sample
 * import * as module0 from "/path/to/module-0";
 * import * as module1 from "/path/to/module-1";
 * import * as module2 from "/path/to/module-2";
 *
 * var requireContext = function(key) {
 *   var _modules = {};
 *
 *   _modules["./relative/path/of/module-0"] = module0;
 *   _modules["./relative/path/of/module-1"] = module1;
 *   _modules["./relative/path/of/module-2"] = module2;
 *
 *   return _modules[key];
 * };
 *
 * requireContext.keys = function() {
 *   return [
 *     "./relative/path/of/module-0",
 *     "./relative/path/of/module-1",
 *     "./relative/path/of/module-2",
 *   ];
 * };
 *
 * export default requireContext;
 * ```
 */
export declare function getRequireContextScript(modules: RequireContextModule[]): string;
export {};
