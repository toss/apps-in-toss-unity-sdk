import type { PermissionAccess, PermissionDialogFunction, PermissionName } from '@apps-in-toss/types';
import { AppsInTossModule } from '../AppsInTossModule';

/**
 * @category AppsInTossModules
 * @name openPermissionDialog
 * @description 권한 요청 다이얼로그를 표시하는 함수예요. 사용자는 이 다이얼로그에서 특정 권한을 허용하거나 거부할 수 있어요.
 * @param permission.name {PermissionName} 요청할 권한의 이름이에요. 예를 들어 `clipboard`는 클립보드 접근 권한을, `camera`는 카메라 접근 권한을 나타내요.
 * @param permission.access {PermissionAccess} 요청할 권한의 접근 유형이에요. 예를 들어 `read`는 읽기 권한, `write`는 쓰기 권한을 의미해요.
 * @returns {Promise<'allowed' | 'denied'>} 권한의 현재 상태를 반환해요. 반환값은 다음 중 하나예요:
 * - `allowed`: 권한이 허용된 상태예요.
 * - `denied`: 권한이 거부된 상태예요.
 *
 * @example
 * ### 클립보드 읽기 권한 다이얼로그 열기
 *
 * ```tsx
 * import React, { useState } from 'react';
 * import { View, Text, Button } from 'react-native';
 * import { openPermissionDialog, type PermissionStatus } from '@apps-in-toss/framework';
 *
 * function PermissionRequest() {
 *   const [permission, setPermission] = useState<PermissionStatus | null>(null);
 *
 *   const handleRequestPermission = async () => {
 *     try {
 *       // 클립보드 읽기 권한을 요청하는 다이얼로그를 표시해요.
 *       const status = await openPermissionDialog({
 *         name: 'clipboard',
 *         access: 'read',
 *       });
 *       setPermission(status); // 권한 상태를 업데이트해요.
 *     } catch (error) {
 *       console.error('권한 요청 중 오류가 발생했어요:', error);
 *     }
 *   };
 *
 *   return (
 *     <View>
 *       <Text>
 *         클립보드 읽기 권한 상태: {permission ? permission : '아직 요청하지 않음'}
 *       </Text>
 *       <Button title="권한 요청하기" onPress={handleRequestPermission} />
 *     </View>
 *   );
 * }
 * ```
 */
export function openPermissionDialog(permission: {
  name: PermissionName;
  access: PermissionAccess;
}): ReturnType<PermissionDialogFunction> {
  return AppsInTossModule.openPermissionDialog(permission);
}
