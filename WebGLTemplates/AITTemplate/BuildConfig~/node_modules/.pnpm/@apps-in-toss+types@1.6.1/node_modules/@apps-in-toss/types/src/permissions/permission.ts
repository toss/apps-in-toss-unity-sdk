import { PermissionFunctionName } from './nativeModules';

export type PermissionStatus = 'notDetermined' | 'denied' | 'allowed';
export type PermissionAccess = 'read' | 'write' | 'access';
export type PermissionName = 'clipboard' | 'contacts' | 'photos' | 'geolocation' | 'camera';

export interface PermissionErrorConstructorParams {
  methodName: PermissionFunctionName;
  message: string;
}

export interface PermissionErrorType extends Error {
  name: string;
  message: string;
}

export interface CreatePermissionFunctionOptions<T extends (...args: any[]) => any> {
  handler: T;
  permission: {
    name: PermissionName;
    access: PermissionAccess;
  };
  error: new () => PermissionErrorType;
}

export type RequestPermissionFunction = (permission: {
  name: PermissionName;
  access: PermissionAccess;
}) => Promise<Exclude<PermissionStatus, 'notDetermined'>>;

export type InternalPermissionDialogFunction = (permission: {
  name: PermissionName;
  access: PermissionAccess;
}) => Promise<Exclude<PermissionStatus, 'notDetermined'>>;
export type InternalGetPermissionFunction = (permission: {
  name: PermissionName;
  access: PermissionAccess;
}) => Promise<PermissionStatus>;

export type PermissionDialogFunction = () => Promise<Exclude<PermissionStatus, 'notDetermined'>>;
export type GetPermissionFunction = () => Promise<PermissionStatus>;

export type PermissionFunctionWithDialog<T extends (...args: any[]) => any> = T & {
  getPermission: GetPermissionFunction;
  openPermissionDialog: PermissionDialogFunction;
};
