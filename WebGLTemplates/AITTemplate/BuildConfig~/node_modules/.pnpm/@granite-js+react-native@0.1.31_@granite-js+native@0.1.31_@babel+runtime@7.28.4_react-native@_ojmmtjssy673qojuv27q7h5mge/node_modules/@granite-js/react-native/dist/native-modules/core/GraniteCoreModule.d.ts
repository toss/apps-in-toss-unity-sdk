import { TurboModule } from 'react-native';
interface GraniteCoreModule extends TurboModule {
    addListener: (eventType: string) => void;
    removeListeners: (count: number) => void;
    importLazy: () => Promise<void>;
}
export declare const GraniteCoreModule: GraniteCoreModule;
export {};
