type ReleaseCallback = () => Promise<any>;
export declare class Permit {
    private onRelease;
    private isReleased;
    constructor(onRelease: ReleaseCallback);
    release(): Promise<void>;
}
export declare class Semaphore {
    private availablePermits;
    private deferreds;
    constructor(count: number);
    acquire(): Promise<Permit>;
    private createPermit;
}
export {};
//# sourceMappingURL=Semaphore.d.ts.map