import { BedrockModule } from './BedrockModule';

/**
 * @public
 * @category 화면 제어
 * @kind function
 * @name setScreenAwakeMode
 * @description
 * `setScreenAwakeMode` 함수는 화면이 항상 켜져 있도록 설정하거나 해제하는 기능을 제공해요.
 * 이 기능은 웹툰, 동영상, 문서 읽기 등 화면을 지속해서 켜두어야 하는 상황에서 유용해요.
 *
 * `enabled` 옵션을 `true`로 설정하면 화면이 꺼지지 않게 유지하고,  `false`로 설정하면 기본 화면 보호기 시간에 따라 화면이 꺼져요.  특히, 이 함수는 앱 전체에 영향을 미치므로 특정 화면에서만 사용하려면 화면을 벗어날 때 이전 상태로 복구하는 추가 작업이 필요해요.
 *
 * 예를 들어, 미디어 콘텐츠 감상 화면에서는 항상 켜짐 모드를 활성화하고, 화면을 떠날 때 설정을 복구해서 불필요한 배터리 소모를 방지할 수 있어요.
 *
 * 다만, 앱에서 벗어나는 상황에서는 항상 켜짐 모드가 비활성화될 수 있으니 주의해야 해요.
 *
 * @param {object} options 화면 항상 켜짐 모드의 설정 값이에요.
 * @param {boolean} options.enabled 화면 항상 켜짐 모드를 켜거나 끄는 옵션이에요.
 * `true`로 설정하면 화면이 항상 켜지고, `false`로 설정하면 화면 보호기 시간에 따라 꺼져요.
 *
 * @returns {object} 현재 화면 항상 켜짐 모드의 설정 상태를 반환해요.
 * @returns {boolean} enabled 화면 항상 켜짐 모드가 켜져 있는지 여부를 나타내는 값이에요.
 *
 * @example
 * ### 화면 항상 켜짐 모드 설정하기
 *
 * ```tsx
 * import { Button } from 'react-native';
 * import { setScreenAwakeMode } from '@apps-in-toss/native-modules';
 *
 * function SetScreenAwakeMode() {
 *   return (
 *     <Button
 *       title="화면 항상 켜기"
 *       onPress={() => {
 *         setScreenAwakeMode({ enabled: true });
 *       }}
 *     />
 *   );
 * }
 * ```
 *
 * ### 화면 항상 켜짐 모드 복구하기
 * 특정 화면을 벗어날 때 이전 상태로 복구하려면 다음과 같이 `useEffect`를 사용하세요.
 *
 * ```tsx
 * import { useEffect } from 'react';
 * import { setScreenAwakeMode, cleanUp } from '@apps-in-toss/native-modules';
 *
 * function MediaScreen() {
 *   useEffect(() => {
 *     setScreenAwakeMode({ enabled: true });
 *
 *     return () => {
 *       setScreenAwakeMode({ enabled: false }); // 설정을 이전 상태로 복구해요.
 *     };
 *   }, []);
 *
 *   return <Text>미디어 콘텐츠를 감상하는 화면</Text>;
 * }
 * ```
 */
export async function setScreenAwakeMode(options: { enabled: boolean }) {
  return BedrockModule.setScreenAwakeMode(options);
}
