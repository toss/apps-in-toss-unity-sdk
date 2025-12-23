import type { PermissionFunctionName } from './nativeModules';

/**
 * @public
 * @category 권한
 * @name PermissionError
 * @description 권한 에러를 나타내는 클래스예요. 공통된 권한에러를 처리할 때 사용해요. 에러가 발생했을 때 `error instanceof PermissionError`를 통해 확인할 수 있어요.
 */
export class PermissionError extends Error {
  constructor({ methodName, message }: { methodName: PermissionFunctionName; message: string }) {
    super();
    this.name = `${methodName} permission error`;
    this.message = message;
  }
}
