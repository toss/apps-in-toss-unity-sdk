/**
 * 앨범 사진을 조회할 때 사용하는 옵션 타입이에요.
 */
export interface FetchAlbumPhotosOptions {
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
export interface ImageResponse {
  /** 가져온 사진의 고유 ID예요. */
  id: string;
  /** 사진의 데이터 URI예요. `base64` 옵션이 `true`인 경우 Base64 문자열로 반환돼요. */
  dataUri: string;
}

export type FetchAlbumPhotos = (options?: FetchAlbumPhotosOptions) => Promise<ImageResponse[]>;
