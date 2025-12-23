import { TurboModule } from 'react-native';
interface GraniteModuleSpec extends TurboModule {
    closeView: () => void;
    schemeUri: string;
}
export declare const GraniteModule: GraniteModuleSpec;
export {};
