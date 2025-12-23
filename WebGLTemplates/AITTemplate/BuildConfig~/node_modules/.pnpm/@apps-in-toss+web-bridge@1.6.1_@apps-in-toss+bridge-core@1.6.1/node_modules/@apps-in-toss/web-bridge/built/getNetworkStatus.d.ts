export type NetworkStatus = "OFFLINE" | "WIFI" | "2G" | "3G" | "4G" | "5G" | "WWAN" | "UNKNOWN";
/**
 * @public
 * @category 네트워크
 * @kind function
 * @name getNetworkStatus
 * @description
 * 디바이스의 현재 네트워크 연결 상태를 가져오는 함수예요.
 * 반환 값은 `NetworkStatus` 타입으로, 인터넷 연결 여부와 연결 유형(Wi-Fi, 모바일 데이터 등)을 나타내요. 값은 다음 중 하나예요.
 *
 * - `OFFLINE`: 인터넷에 연결되지 않은 상태예요.
 * - `WIFI`: Wi-Fi에 연결된 상태예요.
 * - `2G`: 2G 네트워크에 연결된 상태예요.
 * - `3G`: 3G 네트워크에 연결된 상태예요.
 * - `4G`: 4G 네트워크에 연결된 상태예요.
 * - `5G`: 5G 네트워크에 연결된 상태예요.
 * - `WWAN`: 인터넷은 연결되었지만, 연결 유형(Wi-Fi, 2G~5G)을 알 수 없는 상태예요. 이 상태는 iOS에서만 확인할 수 있어요.
 * - `UNKNOWN`: 인터넷 연결 상태를 알 수 없는 상태예요. 이 상태는 안드로이드에서만 확인할 수 있어요.
 *
 * @returns {Promise<NetworkStatus>} 네트워크 상태를 반환해요.
 *
 * @example
 * ### 현재 네트워크 상태 가져오기
 *
 * 네트워크 연결 상태를 가져와 화면에 표시하는 예제예요.
 *
 * ```tsx
 * import { useState, useEffect } from 'react';
 * 
 * import { getNetworkStatus, NetworkStatus } from '@apps-in-toss/native-modules';
 *
 * function GetNetworkStatus() {
 *   const [status, setStatus] = useState<NetworkStatus | ''>('');
 *
 *   useEffect(() => {
 *     async function fetchStatus() {
 *       const networkStatus = await getNetworkStatus();
 *       setStatus(networkStatus);
 *     }
 *
 *     fetchStatus();
 *   }, []);
 *
 *   return (
 *     <div>
 *       <span>현재 네트워크 상태: {status}</span>
 *     </div>
 *   );
 * }
 * ```
 */
export declare function getNetworkStatus(): Promise<NetworkStatus>;

export {};
