interface CacheConfig<T> {
    /**
     * 파일 시스템에 저장되는 데이터는 string 타입이기에, 데이터를 로드한 후 T 타입으로 변환하기 위한 함수가 필요함.
     */
    parse: (data: string) => T;
    /**
     * 파일 시스템에 저장되는 데이터는 string 타입이기에, stringify 함수가 필요함.
     */
    stringify: (value: T) => string;
}
export declare class Cache<T> {
    private config;
    static BASE_CACHE_DIR: string;
    private cache;
    private cachePath;
    constructor(cacheDirectoryName: string, config: CacheConfig<T>);
    getDir(): string;
    getMemoryCache(): Record<string, T>;
    read(key: string): Promise<T | null>;
    write(key: string, value: T): Promise<void>;
    protected readFromFileSystem(key: string): Promise<string>;
    protected writeToMemory(key: string, value: T): void;
    protected writeToFileSystem(key: string, value: T): Promise<void>;
}
export {};
