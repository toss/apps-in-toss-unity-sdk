/**
 * @name excludeFileExtension
 * @description Removes file extension.
 */
export declare function excludeFileExtension(name: string): string;
/**
 * @name excludeRelativePath
 * @description Removes relative path from path.
 * @example
 * ```ts
 * excludeRelativePath("./index.tsx") // "index.tsx"
 * excludeRelativePath("./list/detail.tsx") // "list/detail.tsx"
 * excludeRelativePath("../../index.tsx") // "index.tsx"
 * ```
 */
export declare function excludeRelativePath(filePath: string): string;
/**
 * @name excludeDynamicNamePattern
 * @description Removes `[` `]` from dynamic route pattern.
 * @example
 * ```ts
 * excludeDynamicNamePattern("[id]") // "id"
 * excludeDynamicNamePattern("[id]/[name]") // "id/name"
 * ```
 */
export declare function excludeDynamicNamePattern(filePath: string): string;
/**
 * @name getRoutePath
 * @description Converts to route path.
 * @example
 * ```ts
 * getRoutePath('./index.tsx') // "/"
 * getRoutePath('./list/index.tsx') // "/list"
 * getRoutePath('./list/detail.tsx') // "/list/detail"
 * getRoutePath('./list/[id].js') // "/list/:id"
 * ```
 */
export declare function getRoutePath(filePath: string): string;
/**
 * @name getFileNameFromPath
 * @description Extracts filename from file path.
 * @example
 * ```ts
 * getFileNameFromPath('/path/to/file.txt') // "file.txt"
 * getFileNameFromPath('file.txt') // "file.txt"
 * getFileNameFromPath('') // ""
 * getFileNameFromPath('/path/to/directory/') // ""
 * getFileNameFromPath('/path/to/file.txt', { withExtension: false }) // "file"
 * ```
 */
export declare function getFileNameFromPath(filePath: string, options?: {
    withExtension?: boolean;
}): string;
