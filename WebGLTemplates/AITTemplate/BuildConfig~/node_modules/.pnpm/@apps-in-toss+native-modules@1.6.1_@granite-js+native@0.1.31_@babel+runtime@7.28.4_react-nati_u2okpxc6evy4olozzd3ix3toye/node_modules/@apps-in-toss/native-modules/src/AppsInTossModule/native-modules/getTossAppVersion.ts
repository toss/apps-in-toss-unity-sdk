import { AppsInTossModule } from './AppsInTossModule';

/**
 * @public
 * @category 환경 확인
 * @name getTossAppVersion
 * @description 토스 앱 버전을 가져옵니다. 예를 들어, `5.206.0`과 같은 형태로 반환돼요. 토스 앱 버전을 로그로 남기거나, 특정 기능이 특정 버전 이상에서만 실행될 때 사용돼요.
 * @signature
 * ```typescript
 * function getTossAppVersion(): string
 * ```
 *
 * @returns {string} 토스 앱 버전
 *
 * @example
 *
 *
 * ### 토스 앱 버전 확인하기
 *
 * ```tsx
 * import { getTossAppVersion } from '@apps-in-toss/framework';
 * import { Text } from 'react-native';
 *
 * export function TossAppVersionPage() {
 *  return (
 *   <Text>{getTossAppVersion()}</Text>
 *  )
 * }
 * ```
 */
export function getTossAppVersion() {
  return AppsInTossModule.tossAppVersion;
}
