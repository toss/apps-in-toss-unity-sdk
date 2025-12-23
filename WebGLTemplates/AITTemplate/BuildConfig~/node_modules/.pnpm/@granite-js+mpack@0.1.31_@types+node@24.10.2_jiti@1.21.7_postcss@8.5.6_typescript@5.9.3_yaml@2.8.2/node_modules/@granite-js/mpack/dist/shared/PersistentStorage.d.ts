import { INTERNAL__Id } from '../types';
interface PersistentData {
    [id: INTERNAL__Id]: {
        /**
         * Resolved module count (used for progress bar progress calculation)
         */
        totalModuleCount: number;
    };
}
declare class PersistentStorage {
    private data;
    constructor();
    getData(): PersistentData;
    setData(newData: Partial<PersistentData>): void;
    loadData(): void;
    saveData(): Promise<void>;
}
export declare const persistentStorage: PersistentStorage;
export {};
