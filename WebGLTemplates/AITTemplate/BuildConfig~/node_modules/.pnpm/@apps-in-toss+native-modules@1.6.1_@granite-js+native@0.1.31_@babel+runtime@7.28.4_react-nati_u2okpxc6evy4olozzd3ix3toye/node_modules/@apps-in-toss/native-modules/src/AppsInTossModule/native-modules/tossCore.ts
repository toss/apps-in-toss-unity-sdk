import { NativeModules } from 'react-native';
import { getOperationalEnvironment } from './getOperationalEnvironment';
import { isMinVersionSupported } from './isMinVersionSupported';

const TossCoreModule: {
  eventLog: (params: {
    params: {
      log_name: string;
      log_type: string;
      params: Record<string, unknown>;
    };
  }) => Promise<void>;
} = NativeModules.TossCoreModule;

export function tossCoreEventLog(params: { log_name: string; log_type: string; params: Record<string, unknown> }) {
  const supported = isMinVersionSupported({ ios: '5.210.0', android: '5.210.0' });
  const isSandbox = getOperationalEnvironment() === 'sandbox';
  if (!supported || isSandbox) {
    return;
  }

  TossCoreModule.eventLog({
    params: {
      log_name: params.log_name,
      log_type: params.log_type,
      params: params.params,
    },
  });
}
