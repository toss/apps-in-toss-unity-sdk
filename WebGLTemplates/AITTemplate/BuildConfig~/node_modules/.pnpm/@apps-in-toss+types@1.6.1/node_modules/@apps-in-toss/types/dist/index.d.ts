/**
 * 앨범 사진을 조회할 때 사용하는 옵션 타입이에요.
 */
interface FetchAlbumPhotosOptions {
    /** 가져올 사진의 최대 개수를 설정해요. 숫자를 입력하고 기본값은 10이에요. */
    maxCount?: number;
    /** 사진의 최대 폭을 제한해요. 단위는 픽셀이고 기본값은 1024이에요. */
    maxWidth?: number;
    /** 이미지를 base64 형식으로 반환할지 설정해요. 기본값은 `false`예요. */
    base64?: boolean;
}
/**
 * 사진 조회 결과를 나타내는 타입이에요.
 */
interface ImageResponse {
    /** 가져온 사진의 고유 ID예요. */
    id: string;
    /** 사진의 데이터 URI예요. `base64` 옵션이 `true`인 경우 Base64 문자열로 반환돼요. */
    dataUri: string;
}
type FetchAlbumPhotos = (options?: FetchAlbumPhotosOptions) => Promise<ImageResponse[]>;

type CompatiblePlaceholderArgument = object;
type PermissionFunctionName = 'getClipboardText' | 'setClipboardText' | 'fetchContacts' | 'fetchAlbumPhotos' | 'getCurrentLocation' | 'openCamera';

/**
 * @public
 * @category 권한
 * @name PermissionError
 * @description 권한 에러를 나타내는 클래스예요. 공통된 권한에러를 처리할 때 사용해요. 에러가 발생했을 때 `error instanceof PermissionError`를 통해 확인할 수 있어요.
 */
declare class PermissionError extends Error {
    constructor({ methodName, message }: {
        methodName: PermissionFunctionName;
        message: string;
    });
}

/**
 * @public
 * @category 권한
 * @name FetchAlbumPhotosPermissionError
 * @description 사진첩 권한이 거부되었을 때 발생하는 에러예요. 에러가 발생했을 때 `error instanceof FetchAlbumPhotosPermissionError`를 통해 확인할 수 있어요.
 */
declare class FetchAlbumPhotosPermissionError extends PermissionError {
    constructor();
}

interface FetchContactsOptions {
    size: number;
    offset: number;
    query?: {
        contains?: string;
    };
}
/**
 * 연락처 정보를 나타내는 타입이에요.
 */
interface ContactEntity {
    /** 연락처 이름이에요. */
    name: string;
    /** 연락처 전화번호로, 문자열 형식이에요. */
    phoneNumber: string;
}
interface ContactResult {
    result: ContactEntity[];
    nextOffset: number | null;
    done: boolean;
}
type FetchContacts = (options: FetchContactsOptions) => Promise<ContactResult>;

/**
 * @public
 * @category 권한
 * @name FetchContactsPermissionError
 * @description 연락처 권한이 거부되었을 때 발생하는 에러예요. 에러가 발생했을 때 `error instanceof FetchContactsPermissionError`를 통해 확인할 수 있어요.
 */
declare class FetchContactsPermissionError extends PermissionError {
    constructor();
}

interface OpenCameraOptions {
    /**
     * 이미지를 Base64 형식으로 반환할지 여부를 나타내는 불리언 값이에요.
     *
     * 기본값: `false`.
     */
    base64?: boolean;
    /**
     * 이미지의 최대 너비를 나타내는 숫자 값이에요.
     *
     * 기본값: `1024`.
     */
    maxWidth?: number;
}
type OpenCamera = (options?: OpenCameraOptions) => Promise<ImageResponse>;

/**
 * @public
 * @category 권한
 * @name OpenCameraPermissionError
 * @description 카메라 권한이 거부되었을 때 발생하는 에러예요. 에러가 발생했을 때 `error instanceof OpenCameraPermissionError`를 통해 확인할 수 있어요.
 */
declare class OpenCameraPermissionError extends PermissionError {
    constructor();
}

/**
 * @public
 * @category 위치 정보
 * @name Accuracy
 * @description 위치 정확도 옵션이에요.
 */
declare enum Accuracy {
    /**
     * 오차범위 3KM 이내
     */
    Lowest = 1,
    /**
     * 오차범위 1KM 이내
     */
    Low = 2,
    /**
     * 오차범위 몇 백미터 이내
     */
    Balanced = 3,
    /**
     * 오차범위 10M 이내
     */
    High = 4,
    /**
     * 가장 높은 정확도
     */
    Highest = 5,
    /**
     * 네비게이션을 위한 최고 정확도
     */
    BestForNavigation = 6
}
interface GetCurrentLocationOptions {
    /**
     * 위치 정보를 가져올 정확도 수준이에요.
     */
    accuracy: Accuracy;
}
/**
 * @public
 * @category 위치 정보
 * @name Location
 * @description 위치 정보를 나타내는 객체예요.
 */
interface Location {
    /**
     * Android에서만 지원하는 옵션이에요.
     *
     * - `FINE`: 정확한 위치
     * - `COARSE`: 대략적인 위치
     *
     * @see https://developer.android.com/codelabs/approximate-location
     */
    accessLocation?: 'FINE' | 'COARSE';
    /**
     * 위치가 업데이트된 시점의 유닉스 타임스탬프예요.
     */
    timestamp: number;
    /**
     * @description 위치 정보를 나타내는 객체예요. 자세한 내용은 [LocationCoords](/react-native/reference/native-modules/Types/LocationCoords.html)을 참고해주세요.
     */
    coords: LocationCoords;
}
/**
 * @public
 * @category 위치 정보
 * @name LocationCoords
 * @description 세부 위치 정보를 나타내는 객체예요.
 */
interface LocationCoords {
    /**
     * 위도
     */
    latitude: number;
    /**
     * 경도
     */
    longitude: number;
    /**
     * 높이
     */
    altitude: number;
    /**
     * 위치 정확도
     */
    accuracy: number;
    /**
     * 고도 정확도
     */
    altitudeAccuracy: number;
    /**
     * 방향
     */
    heading: number;
}
type GetCurrentLocation = (options: GetCurrentLocationOptions) => Promise<Location>;

/**
 * @public
 * @category 권한
 *  @name GetCurrentLocationPermissionError
 * @description 위치 권한이 거부되었을 때 발생하는 에러예요. 에러가 발생했을 때 `error instanceof GetCurrentLocationPermissionError`를 통해 확인할 수 있어요.
 */
declare class GetCurrentLocationPermissionError extends PermissionError {
    constructor();
}

interface StartUpdateLocationOptions {
    /**
     * 위치 정확도를 설정해요.
     */
    accuracy: Accuracy;
    /**
     * 위치 업데이트 주기를 밀리초(ms) 단위로 설정해요.
     */
    timeInterval: number;
    /**
     * 위치 변경 거리를 미터(m) 단위로 설정해요.
     */
    distanceInterval: number;
}
type StartUpdateLocationEventParams = {
    onEvent: (response: Location) => void;
    onError: (error: unknown) => void;
    options: StartUpdateLocationOptions;
};
type StartUpdateLocation = (eventParams: StartUpdateLocationEventParams) => () => void;

/**
 * @public
 * @category 권한
 *  @name StartUpdateLocationPermissionError
 * @description 위치 업데이트 권한이 거부되었을 때 발생하는 에러예요. 에러가 발생했을 때 `error instanceof StartUpdateLocationPermissionError`를 통해 확인할 수 있어요.
 */
declare const StartUpdateLocationPermissionError: typeof GetCurrentLocationPermissionError;

type GetClipboardText = () => Promise<string>;

/**
 * @public
 * @category 권한
 * @name GetClipboardTextPermissionError
 * @description 클립보드 읽기 권한이 거부되었을 때 발생하는 에러예요. 에러가 발생했을 때 `error instanceof GetClipboardTextPermissionError`를 통해 확인할 수 있어요.
 */
declare class GetClipboardTextPermissionError extends PermissionError {
    constructor();
}

interface SetClipboardTextOptions {
    text: string;
}
type SetClipboardText = (text: string) => Promise<void>;

/**
 * @public
 * @category 권한
 * @name SetClipboardTextPermissionError
 * @description 클립보드 쓰기 권한이 거부되었을 때 발생하는 에러예요. 에러가 발생했을 때 `error instanceof SetClipboardTextPermissionError`를 통해 확인할 수 있어요.
 */
declare class SetClipboardTextPermissionError extends PermissionError {
    constructor();
}

type PermissionStatus = 'notDetermined' | 'denied' | 'allowed';
type PermissionAccess = 'read' | 'write' | 'access';
type PermissionName = 'clipboard' | 'contacts' | 'photos' | 'geolocation' | 'camera';
interface PermissionErrorConstructorParams {
    methodName: PermissionFunctionName;
    message: string;
}
interface PermissionErrorType extends Error {
    name: string;
    message: string;
}
interface CreatePermissionFunctionOptions<T extends (...args: any[]) => any> {
    handler: T;
    permission: {
        name: PermissionName;
        access: PermissionAccess;
    };
    error: new () => PermissionErrorType;
}
type RequestPermissionFunction = (permission: {
    name: PermissionName;
    access: PermissionAccess;
}) => Promise<Exclude<PermissionStatus, 'notDetermined'>>;
type InternalPermissionDialogFunction = (permission: {
    name: PermissionName;
    access: PermissionAccess;
}) => Promise<Exclude<PermissionStatus, 'notDetermined'>>;
type InternalGetPermissionFunction = (permission: {
    name: PermissionName;
    access: PermissionAccess;
}) => Promise<PermissionStatus>;
type PermissionDialogFunction = () => Promise<Exclude<PermissionStatus, 'notDetermined'>>;
type GetPermissionFunction = () => Promise<PermissionStatus>;
type PermissionFunctionWithDialog<T extends (...args: any[]) => any> = T & {
    getPermission: GetPermissionFunction;
    openPermissionDialog: PermissionDialogFunction;
};

export { Accuracy, type CompatiblePlaceholderArgument, type ContactEntity, type ContactResult, type CreatePermissionFunctionOptions, type FetchAlbumPhotos, type FetchAlbumPhotosOptions, FetchAlbumPhotosPermissionError, type FetchContacts, type FetchContactsOptions, FetchContactsPermissionError, type GetClipboardText, GetClipboardTextPermissionError, type GetCurrentLocation, type GetCurrentLocationOptions, GetCurrentLocationPermissionError, type GetPermissionFunction, type ImageResponse, type InternalGetPermissionFunction, type InternalPermissionDialogFunction, type Location, type LocationCoords, type OpenCamera, type OpenCameraOptions, OpenCameraPermissionError, type PermissionAccess, type PermissionDialogFunction, type PermissionErrorConstructorParams, type PermissionErrorType, type PermissionFunctionName, type PermissionFunctionWithDialog, type PermissionName, type PermissionStatus, type RequestPermissionFunction, type SetClipboardText, type SetClipboardTextOptions, SetClipboardTextPermissionError, type StartUpdateLocation, type StartUpdateLocationEventParams, type StartUpdateLocationOptions, StartUpdateLocationPermissionError };
