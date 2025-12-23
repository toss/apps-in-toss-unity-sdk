import { AppsInTossModule } from './AppsInTossModule';
import { isMinVersionSupported } from './isMinVersionSupported';

const V2_MIN_VERSION = {
  android: '5.240.0',
  ios: '5.239.0',
} as const;

/**
 * @public
 * @category 공유
 * @kind function
 * @name getTossShareLink
 * @description
 * 사용자가 지정한 경로를 토스 앱에서 열 수 있는 공유 링크를 생성해요.
 *
 * 생성된 링크를 다른 사람과 공유하면:
 * - 토스 앱이 설치되어 있으면: 토스 앱이 실행되면서 지정한 경로로 이동해요.
 * - 토스 앱이 없으면: iOS는 앱스토어로, Android는 플레이스토어로 이동해요.
 *
 * @param path - 딥링크 경로예요. `intoss://`로 시작하는 문자열이어야 해요. (예: `intoss://my-app`, `intoss://my-app/detail?id=123`)
 * @param ogImageUrl - (선택) 공유 시 표시될 커스텀 OG 이미지 URL이에요. 최소 버전: Android 5.240.0, iOS 5.239.0
 * @returns {Promise<string>} 생성된 토스 공유 링크
 *
 * @example
 * ```tsx
 * import { share } from '@granite-js/react-native';
 * import { getTossShareLink } from '@apps-in-toss/framework';
 *
 * // 기본 사용법
 * const tossLink = await getTossShareLink('intoss://my-app');
 * await share({ message: tossLink });
 *
 * // 커스텀 OG 이미지와 함께 사용
 * const linkWithImage = await getTossShareLink(
 *   'intoss://my-app/event',
 *   'https://example.com/og-image.png'
 * );
 * await share({ message: linkWithImage });
 * ```
 */
export async function getTossShareLink(path: string, ogImageUrl?: string): Promise<string> {
  if (!isMinVersionSupported(V2_MIN_VERSION)) {
    return await getTossShareLinkV1(path);
  }

  const params = {
    params: {
      url: path,
      ogImageUrl,
    },
  };

  const { shareLink } = await AppsInTossModule.getTossShareLink(params);
  return shareLink;
}

async function getTossShareLinkV1(path: string): Promise<string> {
  const { shareLink } = await AppsInTossModule.getTossShareLink({});
  const shareUrl = new URL(shareLink);

  shareUrl.searchParams.set('deep_link_value', path);
  shareUrl.searchParams.set('af_dp', path);
  return shareUrl.toString();
}
