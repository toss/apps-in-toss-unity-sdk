import { PermissionError } from './PermissionError';

/**
 * @public
 * @category 권한
 * @name GetClipboardTextPermissionError
 * @description 클립보드 읽기 권한이 거부되었을 때 발생하는 에러예요. 에러가 발생했을 때 `error instanceof GetClipboardTextPermissionError`를 통해 확인할 수 있어요.
 */
export class GetClipboardTextPermissionError extends PermissionError {
  constructor() {
    super({ methodName: 'getClipboardText', message: '클립보드 읽기 권한이 거부되었어요.' });
  }
}
