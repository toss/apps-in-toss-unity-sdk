import { CreatePermissionFunctionOptions, PermissionFunctionWithDialog } from '@apps-in-toss/types';
import { openPermissionDialog } from './openPermissionDialog';
import { requestPermission } from './requestPermission';
import { getPermission } from '../getPermission';

export function createPermissionFunction<T extends (...args: any[]) => any>({
  handler,
  permission,
  error,
}: CreatePermissionFunctionOptions<T>): PermissionFunctionWithDialog<T> {
  const permissionFunction = async (...args: Parameters<T>): Promise<Awaited<ReturnType<T>>> => {
    const permissionStatus = await requestPermission(permission);

    if (permissionStatus === 'denied') {
      throw new error();
    }

    return handler(...args);
  };

  permissionFunction.getPermission = () => getPermission(permission);
  permissionFunction.openPermissionDialog = () => openPermissionDialog(permission);

  return permissionFunction as any;
}
