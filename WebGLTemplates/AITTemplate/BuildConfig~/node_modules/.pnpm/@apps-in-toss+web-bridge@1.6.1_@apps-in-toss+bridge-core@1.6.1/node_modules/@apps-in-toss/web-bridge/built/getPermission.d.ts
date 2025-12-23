type PermissionStatus$1 = "notDetermined" | "denied" | "allowed";
export type PermissionAccess = "read" | "write" | "access";
type PermissionName$1 = "clipboard" | "contacts" | "photos" | "geolocation" | "camera";
/**
 * @category AppsInTossModules
 * @name getPermission
 * @description 특정 권한의 현재 상태를 확인하는 함수예요. 사용자가 권한을 허용했는지, 거부했는지, 또는 아직 선택하지 않았는지를 알 수 있어요.
 * @param permission.name {PermissionName} 요청할 권한의 이름이에요. 예를 들어 `clipboard`는 클립보드 접근 권한을, `camera`는 카메라 접근 권한을 나타내요.
 * @param permission.access {PermissionAccess} 요청할 권한의 접근 유형이에요. 예를 들어 `read`는 읽기 권한, `write`는 쓰기 권한을 의미해요.
 * @returns {Promise<'allowed' | 'denied' | 'notDetermined'>} 권한의 현재 상태를 반환해요. 반환값은 다음 중 하나예요:
 * - `allowed`: 권한이 허용된 상태예요.
 * - `denied`: 권한이 거부된 상태예요.
 * - `notDetermined`: 아직 권한 요청에 대한 결정이 이루어지지 않은 상태예요.
 *
 * @example
 * ### 클립보드 읽기 권한 상태 확인하기
 *
 * ```tsx
 * import React, { useEffect, useState } from 'react';
 * 
 * import { getPermission, type PermissionStatus } from '@apps-in-toss/web-framework';
 *
 * function PermissionCheck() {
 *   const [permission, setPermission] = useState<PermissionStatus | null>(null);
 *
 *   useEffect(() => {
 *     async function checkPermission() {
 *       try {
 *         // 'clipboard'는 권한 이름, 'read'는 접근 유형을 나타내요.
 *         const status = await getPermission({
 *           name: 'clipboard', // 클립보드 접근 권한
 *           access: 'read', // 읽기 권한 요청
 *         });
 *         setPermission(status); // 권한 상태를 업데이트해요.
 *       } catch (error) {
 *         console.error('권한 상태를 확인하지 못했어요:', error);
 *       }
 *     }
 *
 *     checkPermission();
 *   }, []);
 *
 *   return (
 *     <div>
 *       <span>
 *         클립보드 읽기 권한 상태: {permission ? permission : '확인 중...'}
 *       </span>
 *     </div>
 *   );
 * }
 * ```
 */
export declare function getPermission(permission: {
	name: PermissionName$1;
	access: PermissionAccess;
}): Promise<PermissionStatus$1>;

export {};
