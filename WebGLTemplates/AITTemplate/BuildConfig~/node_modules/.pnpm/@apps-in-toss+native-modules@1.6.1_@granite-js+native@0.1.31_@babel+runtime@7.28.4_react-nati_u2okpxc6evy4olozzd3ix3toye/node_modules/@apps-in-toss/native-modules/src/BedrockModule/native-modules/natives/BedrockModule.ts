import { NativeModules } from 'react-native';
import type { HapticFeedbackOptions } from './generateHapticFeedback/types';
import type { NetworkStatus } from './getNetworkStatus/types';

interface BedrockModule {
  closeView: () => void;
  generateHapticFeedback: (options: HapticFeedbackOptions) => Promise<void>;
  share: (message: { message: string }) => void;
  setSecureScreen: (options: { enabled: boolean }) => Promise<{ enabled: boolean }>;
  setScreenAwakeMode: (options: { enabled: boolean }) => Promise<{ enabled: boolean }>;
  getNetworkStatus: () => Promise<NetworkStatus>;
  setIosSwipeGestureEnabled: ({ isEnabled }: { isEnabled: boolean }) => Promise<void>;
  deviceId: string;
  DeviceInfo: {
    locale: string;
  };
  schemeUri: string;
}

export const BedrockModule: BedrockModule = NativeModules.BedrockModule;
