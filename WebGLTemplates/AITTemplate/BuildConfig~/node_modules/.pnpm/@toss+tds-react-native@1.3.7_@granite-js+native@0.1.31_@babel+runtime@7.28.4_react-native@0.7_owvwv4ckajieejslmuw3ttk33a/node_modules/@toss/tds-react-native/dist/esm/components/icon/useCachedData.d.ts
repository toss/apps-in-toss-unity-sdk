export declare function useCachedData<T>(cache: Record<string, any>, cacheKey: string, promise: (...inputs: any) => Promise<T>, inputs?: any[]): {
    data: T | undefined;
    showFallback: boolean;
};
