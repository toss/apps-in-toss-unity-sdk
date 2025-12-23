type PermissionStatus$1 = "notDetermined" | "denied" | "allowed";
export type PermissionAccess = "read" | "write" | "access";
type PermissionName$1 = "clipboard" | "contacts" | "photos" | "geolocation" | "camera";
/**
 * @category PermissionManagement
 * @name requestPermission
 * @description 특정 권한을 요청하는 함수예요. 이미 허용되었거나 거부된 권한 상태를 확인한 뒤, 필요할 때 권한 요청 다이얼로그를 표시해요.
 * @param permission.name {PermissionName} 요청할 권한의 이름이에요. 예를 들어 `clipboard`는 클립보드 접근 권한을, `camera`는 카메라 접근 권한을 나타내요.
 * @param permission.access {PermissionAccess} 요청할 권한의 접근 유형이에요. 예를 들어 `read`는 읽기 권한, `write`는 쓰기 권한을 의미해요.
 * @returns {Promise<'allowed' | 'denied'>} 사용자가 선택한 최종 권한 상태를 반환해요.
 * 반환값은 다음 중 하나예요:
 * - `allowed`: 권한이 허용된 경우
 * - `denied`: 권한이 거부된 경우
 *
 * @example
 * ### 클립보드 읽기 권한 요청하기
 *
 * ```tsx
 * import React, { useState } from 'react';
 * 
 * import { requestPermission, type PermissionStatus } from '@apps-in-toss/web-framework';
 *
 * function PermissionRequest() {
 *   const [permissionStatus, setPermissionStatus] = useState<PermissionStatus | null>(null);
 *
 *   const handleRequestPermission = async () => {
 *     try {
 *       // 클립보드 읽기 권한을 요청해요.
 *       const permissionStatus = await requestPermission({
 *         name: 'clipboard', // 권한 이름: 클립보드
 *         access: 'read', // 접근 유형: 읽기
 *       });
 *       setPermissionStatus(permissionStatus); // 최종 권한 상태를 저장해요.
 *     } catch (error) {
 *       console.error('권한 요청 중 오류가 발생했어요:', error);
 *     }
 *   };
 *
 *   return (
 *     <div>
 *       <span>
 *         클립보드 읽기 권한 상태: {permissionStatus ? permissionStatus : '아직 요청하지 않음'}
 *       </span>
 *       <input type="button" value="권한 요청하기" onClick={handleRequestPermission} />
 *     </div>
 *   );
 * }
 * ```
 */
export declare function requestPermission(permission: {
	name: PermissionName$1;
	access: PermissionAccess;
}): Promise<Exclude<PermissionStatus$1, "notDetermined">>;

export {};
