import { ImageResponse } from './fetchAlbumPhotos';

export interface OpenCameraOptions {
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

export type OpenCamera = (options?: OpenCameraOptions) => Promise<ImageResponse>;
