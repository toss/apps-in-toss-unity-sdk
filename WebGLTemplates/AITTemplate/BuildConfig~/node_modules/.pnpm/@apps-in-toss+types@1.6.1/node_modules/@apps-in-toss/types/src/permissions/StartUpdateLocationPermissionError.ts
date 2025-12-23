import { GetCurrentLocationPermissionError } from './GetCurrentLocationPermissionError';

/**
 * @public
 * @category 권한
 *  @name StartUpdateLocationPermissionError
 * @description 위치 업데이트 권한이 거부되었을 때 발생하는 에러예요. 에러가 발생했을 때 `error instanceof StartUpdateLocationPermissionError`를 통해 확인할 수 있어요.
 */
export const StartUpdateLocationPermissionError = GetCurrentLocationPermissionError;
