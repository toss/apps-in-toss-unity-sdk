import { NativeModules } from 'react-native';

interface BedrockCoreModule {
  addListener: (eventType: string) => void;
  removeListeners: (count: number) => void;
}

export const BedrockCoreModule: BedrockCoreModule = NativeModules.BedrockCoreModule;
