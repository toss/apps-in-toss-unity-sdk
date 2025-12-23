import { Platform } from 'react-native';
import { AppsInTossModule } from './AppsInTossModule';
import { compareVersions } from '../../utils/compareVersion';

/**
 * @public
 * @category 환경 확인
 * @kind function
 * @name isMinVersionSupported
 * @description
 * 현재 토스 앱 버전이 지정한 최소 버전 이상인지 확인해요.
 *
 * 이 함수는 현재 실행 중인 토스 앱의 버전이 파라미터로 전달된 최소 버전 요구사항을 충족하는지 확인해요. 특정 기능이 최신 버전에서만 동작할 때, 사용자에게 앱 업데이트를 안내할 수 있어요.
 *
 * @param {Object} minVersions 플랫폼별 최소 버전 요구사항을 지정하는 객체예요.
 * @param {(`${number}.${number}.${number}` | 'always' | 'never')} minVersions.android 안드로이드 플랫폼의 최소 버전 요구사항이에요.
 * @param {(`${number}.${number}.${number}` | 'always' | 'never')} minVersions.ios iOS 플랫폼의 최소 버전 요구사항이에요.
 * @returns {boolean} 현재 앱 버전이 최소 버전 이상이면 true, 그렇지 않으면 false를 반환해요.
 *
 * @example
 * ### 앱 버전 확인하기
 *
 * ```tsx
 * import { isMinVersionSupported } from '@apps-in-toss/framework';
 * import { Text, View } from 'react-native';
 *
 * function VersionCheck() {
 *   const isSupported = isMinVersionSupported({
 *     android: '1.2.0',
 *     ios: '1.3.0'
 *   });
 *
 *   return (
 *     <View>
 *       {!isSupported && (
 *         <Text>최신 버전으로 업데이트가 필요해요.</Text>
 *       )}
 *     </View>
 *   );
 * }
 * ```
 */
export function isMinVersionSupported(minVersions: {
  android: `${number}.${number}.${number}` | 'always' | 'never';
  ios: `${number}.${number}.${number}` | 'always' | 'never';
}): boolean {
  const operationalEnvironment = AppsInTossModule.operationalEnvironment;
  if (operationalEnvironment === 'sandbox') {
    return true;
  }

  const currentVersion = AppsInTossModule.tossAppVersion;
  const isIOS = Platform.OS === 'ios';
  const minVersion = isIOS ? minVersions.ios : minVersions.android;

  if (minVersion === undefined) {
    return false;
  }

  if (minVersion === 'always') {
    return true;
  }
  if (minVersion === 'never') {
    return false;
  }

  return compareVersions(currentVersion, minVersion) >= 0;
}
