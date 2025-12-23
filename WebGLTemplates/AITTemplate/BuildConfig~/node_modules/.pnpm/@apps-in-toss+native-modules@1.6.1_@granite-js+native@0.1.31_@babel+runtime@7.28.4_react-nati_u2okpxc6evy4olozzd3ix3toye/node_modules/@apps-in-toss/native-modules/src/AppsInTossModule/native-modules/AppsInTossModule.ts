import type {
  CompatiblePlaceholderArgument,
  ContactResult,
  FetchAlbumPhotosOptions,
  FetchContactsOptions,
  GetCurrentLocationOptions,
  ImageResponse,
  Location,
  OpenCameraOptions,
  PermissionAccess,
  PermissionName,
  PermissionStatus,
  SetClipboardTextOptions,
} from '@apps-in-toss/types';
import { TurboModuleRegistry, type TurboModule as __TurboModule } from 'react-native';
import type { AppsInTossSignTossCertParams } from './appsInTossSignTossCert';
import type { CheckoutPaymentOptions, CheckoutPaymentResult } from './checkoutPayment';
import type { GameCenterGameProfileResponse } from './getGameCenterGameProfile';
import { GetUserKeyForGameResponse } from './getUserKeyForGame';
import { GrantPromotionRewardForGameResponse } from './grantPromotionRewardForGame';
import { IapCreateOneTimePurchaseOrderResult, IapProductListItem, CompletedOrRefundedOrdersResult } from './iap';
import type { SaveBase64DataParams } from './saveBase64Data';
import type { SubmitGameCenterLeaderBoardScoreResponse } from './submitGameCenterLeaderBoardScore';
import type { ContactsViralParams } from '../native-event-emitter/contactsViral';

/**
 * TurboModule 타입 별칭 사용하는 이유?
 * React Native Codegen 에 의해 코드젠 되는 것이 아니라 추후 내부 모듈 체계에 의해 처리될 것이기 때문에 RN Codegen에 본 파일을 코드젠 하지 않도록 함
 * (코드젠 내부에서 "extends TurboModule" 문자열을 찾기 때문에 패턴에 매칭되지 않도록 함)
 */
interface Spec extends __TurboModule {
  groupId: string;
  operationalEnvironment: 'sandbox' | 'toss';
  tossAppVersion: string;
  deviceId: string;

  getClipboardText: (arg: CompatiblePlaceholderArgument) => Promise<string>;
  setClipboardText: (option: SetClipboardTextOptions) => Promise<void>;
  fetchContacts: (option: FetchContactsOptions) => Promise<ContactResult>;
  fetchAlbumPhotos: (options: FetchAlbumPhotosOptions) => Promise<ImageResponse[]>;
  getCurrentLocation: (options: GetCurrentLocationOptions) => Promise<Location>;
  openCamera: (options: OpenCameraOptions) => Promise<ImageResponse>;

  getWebBundleURL: (arg: CompatiblePlaceholderArgument) => { url: string };
  getPermission: (permission: { name: PermissionName; access: PermissionAccess }) => Promise<PermissionStatus>;
  openPermissionDialog: (permission: {
    name: PermissionName;
    access: PermissionAccess;
  }) => Promise<Exclude<PermissionStatus, 'notDetermined'>>;
  appLogin: (
    arg: CompatiblePlaceholderArgument
  ) => Promise<{ authorizationCode: string; referrer: 'DEFAULT' | 'SANDBOX' }>;
  checkoutPayment: (options: { params: CheckoutPaymentOptions }) => Promise<CheckoutPaymentResult>;

  /** Storage */
  getStorageItem: (params: { key: string }) => Promise<string | null>;
  setStorageItem: (params: { key: string; value: string }) => Promise<void>;
  removeStorageItem: (params: { key: string }) => Promise<void>;
  clearStorage: (arg: CompatiblePlaceholderArgument) => Promise<void>;
  eventLog: (params: {
    log_name: string;
    log_type: 'debug' | 'info' | 'warn' | 'error' | 'event' | 'screen' | 'impression' | 'click';
    params: Record<string, string>;
  }) => Promise<void>;
  getTossShareLink: (params: object) => Promise<{ shareLink: string }>;
  setDeviceOrientation: (options: { type: 'portrait' | 'landscape' }) => Promise<void>;
  saveBase64Data: (params: SaveBase64DataParams) => Promise<void>;

  /** IAP */
  iapGetProductItemList: (arg: CompatiblePlaceholderArgument) => Promise<{ products: IapProductListItem[] }>;
  /** @deprecated `requestOneTimePurchase`를 사용해주세요. */
  iapCreateOneTimePurchaseOrder: (params: { productId: string }) => Promise<IapCreateOneTimePurchaseOrderResult>;
  requestOneTimePurchase: (
    params: { sku: string },
    fallbacks: { onPurchased: (params: { orderId: string }) => void }
  ) => () => void;
  processProductGrant: (params: { orderId: string; isProductGranted: boolean }) => Promise<void>;
  getPendingOrders: (
    params: CompatiblePlaceholderArgument
  ) => Promise<{ orders: { orderId: string; sku: string; paymentCompletedDate: string }[] }>;
  getCompletedOrRefundedOrders: (params: { key?: string | null }) => Promise<CompletedOrRefundedOrdersResult>;
  completeProductGrant: (params: { params: { orderId: string } }) => Promise<boolean>;

  getGameCenterGameProfile: (params: CompatiblePlaceholderArgument) => Promise<GameCenterGameProfileResponse>;
  getUserKeyForGame: (params: CompatiblePlaceholderArgument) => Promise<GetUserKeyForGameResponse>;
  getIsTossLoginIntegratedService: (params: CompatiblePlaceholderArgument) => Promise<boolean>;
  grantPromotionRewardForGame: (params: {
    params: { promotionCode: string; amount: number };
  }) => Promise<GrantPromotionRewardForGameResponse>;
  submitGameCenterLeaderBoardScore: (params: { score: string }) => Promise<SubmitGameCenterLeaderBoardScoreResponse>;

  contactsViral: (params: ContactsViralParams) => () => void;

  /** 토스인증 */
  appsInTossSignTossCert: (params: { params: AppsInTossSignTossCertParams }) => void;
}

const Module = TurboModuleRegistry.getEnforcing<Spec>('AppsInTossModule');

export const AppsInTossModuleInstance = Module as any;
export const AppsInTossModule = Module;
