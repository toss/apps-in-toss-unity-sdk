import { PermissionError } from './PermissionError';

/**
 * @public
 * @category 권한
 * @name FetchContactsPermissionError
 * @description 연락처 권한이 거부되었을 때 발생하는 에러예요. 에러가 발생했을 때 `error instanceof FetchContactsPermissionError`를 통해 확인할 수 있어요.
 */
export class FetchContactsPermissionError extends PermissionError {
  constructor() {
    super({ methodName: 'fetchContacts', message: '연락처 권한이 거부되었어요.' });
  }
}
