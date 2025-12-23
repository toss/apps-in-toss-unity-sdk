declare function getPackageRoot(): string;

declare function readZipContent(zipPath: string, fileName: string): Promise<string>;
declare function readZipEntries(zipPath: string): Promise<Record<string, string>>;

type Fn<T> = (...args: any[]) => T;
declare function ensureSafetyInvokeSync<T>(fn: Fn<T>): Fn<T | null>;

declare function getLocalTempDirectoryPath(rootDir: string): string;
declare function prepareLocalDirectory(rootDir: string): string;

export { ensureSafetyInvokeSync, getLocalTempDirectoryPath, getPackageRoot, prepareLocalDirectory, readZipContent, readZipEntries };
