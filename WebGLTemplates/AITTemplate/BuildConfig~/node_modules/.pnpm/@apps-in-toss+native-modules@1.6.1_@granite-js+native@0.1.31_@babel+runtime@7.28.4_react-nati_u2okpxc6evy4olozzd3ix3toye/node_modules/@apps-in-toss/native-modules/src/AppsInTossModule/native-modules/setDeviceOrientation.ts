import { AppsInTossModule } from './AppsInTossModule';
import { isMinVersionSupported } from './isMinVersionSupported';

/**
 * @public
 * @category 화면 제어
 * @kind function
 * @name setDeviceOrientation
 * @description
 * `setDeviceOrientation` 함수는 기기의 화면 방향을 설정하는 기능을 제공해요.
 * 이 기능은 특정 화면에서 가로 모드나 세로 모드를 강제로 지정해야 할 때 유용해요.
 *
 * `type` 옵션을 통해 원하는 화면 방향을 지정할 수 있어요. 특히, 이 함수는 앱 전체에 영향을 미치므로
 * 특정 화면에서만 사용하려면 화면을 벗어날 때 이전 상태로 복구하는 추가 작업이 필요해요.
 *
 * 예를 들어, 동영상 감상 화면에서는 가로 모드를 강제하고, 화면을 떠날 때 설정을 복구해서
 * 다른 화면들의 방향 설정에 영향을 주지 않도록 할 수 있어요.
 *
 * @param {object} options 화면 방향 설정 값이에요.
 * @param {string} options.type 화면 방향을 지정하는 옵션이에요.
 * 'portrait' | 'landscape' 중 하나를 선택할 수 있어요.
 *
 * @returns {Promise<void>} 화면 방향 설정이 완료되면 해결되는 Promise를 반환해요.
 *
 * @example
 * ### 화면 방향 설정하기
 *
 * ```tsx
 * import { Button } from 'react-native';
 * import { setDeviceOrientation } from '@apps-in-toss/framework';
 *
 * function SetDeviceOrientation() {
 *   return (
 *     <Button
 *       title="가로 모드로 변경"
 *       onPress={() => {
 *         setDeviceOrientation({ type: 'landscape' });
 *       }}
 *     />
 *   );
 * }
 * ```
 *
 * ### 화면 방향 복구하기
 * 특정 화면을 벗어날 때 이전 상태로 복구하려면 다음과 같이 `useEffect`를 사용하세요.
 *
 * ```tsx
 * import { useEffect } from 'react';
 * import { setDeviceOrientation } from '@apps-in-toss/framework';
 *
 * function VideoScreen() {
 *   useEffect(() => {
 *     setDeviceOrientation({ type: 'landscape' });
 *
 *     return () => {
 *       setDeviceOrientation({ type: 'portrait' }); // 설정을 이전 상태로 복구해요.
 *     };
 *   }, []);
 *
 *   return <Text>동영상을 감상하는 화면</Text>;
 * }
 * ```
 */
export async function setDeviceOrientation(options: { type: 'portrait' | 'landscape' }): Promise<void> {
  const isSupported = isMinVersionSupported({
    android: '5.215.0',
    ios: '5.215.0',
  });
  if (!isSupported) {
    return;
  }

  return AppsInTossModule.setDeviceOrientation(options);
}
