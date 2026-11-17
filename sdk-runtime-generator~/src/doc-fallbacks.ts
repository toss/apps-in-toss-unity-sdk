/**
 * 3.x 번들 JSDoc에 없는 XML 문서(요약, @param, @returns)를 채우는 큐레이션 폴백.
 *
 * 3.x d.ts의 JSDoc은 @param/@returns 태그를 거의 달지 않는다. 기존 SDK의 같은 API에는
 * 이 설명이 있었으므로, 문서가 얇아지지 않도록 키(C# API 이름)별로 옮겨 둔다.
 * 번들 JSDoc에 같은 항목이 있으면 번들 쪽이 우선이고, 이 표는 비어 있는 항목만 채운다.
 * deprecated API의 대체 API(예: OpenURL -> DeviceOpenURL)에도 매개변수 이름이 같으면 함께 쓴다.
 */
export interface DocFallback {
  summary?: string;
  /** 키는 C# 매개변수 이름 */
  params?: Record<string, string>;
  returns?: string;
}

export const DOC_FALLBACKS: Readonly<Record<string, DocFallback>> = {
  AppsInTossSignTossCert: { params: { paramsParam: "서명에 필요한 파라미터를 포함하는 객체예요." } },
  CheckoutPayment: { params: { options: "결제창을 띄울 때 필요한 옵션이에요." }, returns: "인증 성공 여부를 포함한 결과를 반환해요." },
  EventLog: { params: { paramsParam: "로그 기록에 필요한 매개변수 객체예요." }, returns: "로그 기록이 완료되면 해결되는 Promise예요." },
  FetchAlbumItems: { params: { options: "조회 옵션을 담은 객체예요." }, returns: "선택한 미디어 목록을 반환해요. 취소 시 빈 배열을 반환해요." },
  GetAnonymousKey: { returns: "사용자 키 조회 결과를 반환해요. GetAnonymousKeyResponse: 사용자 키 조회에 성공했어요. { type: 'HASH', hash: string } 형태로 반환돼요. 'ERROR': 알 수 없는 오류가 발생했어요. undefined: 앱 버전이 최소 지원 버전보다 낮아요." },
  GetConsentedUserData: { params: { options: "동의 데이터 요청 옵션이에요." }, returns: "서버에서 제공하는 사용자 데이터를 반환해요. 토스앱 버전이 5.264.0 미만이면 undefined를 반환해요." },
  GetDeviceId: { returns: "기기의 고유 식별자를 나타내는 문자열이에요." },
  GetGameCenterGameProfile: { returns: "프로필 정보 또는 undefined를 반환해요." },
  GetGroupId: { returns: "그룹 ID" },
  GetIsTossLoginIntegratedService: { returns: "토스 로그인이 연동된 유저인지 여부를 반환해요. true: 토스 로그인이 연동된 유저에요. false: 토스 로그인이 연동되지 않은 유저에요. undefined: 앱 버전이 최소 지원 버전보다 낮아요." },
  GetLocale: { returns: "사용자의 로케일 정보를 반환해요." },
  GetNetworkStatus: { returns: "네트워크 상태를 반환해요." },
  GetOperationalEnvironment: { returns: "현재 운영 환경을 나타내는 문자열이에요. 'toss': 토스 앱에서 실행 중이에요. 'sandbox': 샌드박스 환경에서 실행 중이에요." },
  GetPermission: { returns: "권한의 현재 상태를 반환해요. 반환값은 다음 중 하나예요: allowed: 권한이 허용된 상태예요. denied: 권한이 거부된 상태예요. notDetermined: 아직 권한 요청에 대한 결정이 이루어지지 않은 상태예요." },
  GetPlatformOS: { returns: "현재 실행 중인 플랫폼" },
  GetSchemeUri: { returns: "처음에 화면에 진입한 스킴 값을 반환해요." },
  GetServerTime: { returns: "서버 시간을 Unix timestamp 밀리초 단위로 반환해요. (예: 1705123456789) 지원하지 않는 버전에서는 undefined를 반환해요." },
  GetTossAppVersion: { returns: "토스 앱 버전" },
  GetTossShareLink: { params: { path: "딥링크로 열고 싶은 경로예요. intoss://로 시작하는 문자열이어야 해요." }, returns: "deep_link_value가 포함된 토스 공유 링크를 반환해요." },
  GoogleAdMobLoadAppsInTossAdMob: { summary: "광고를 미리 불러와서, 광고가 필요한 시점에 바로 보여줄 수 있도록 준비하는 함수예요." },
  GoogleAdMobShowAppsInTossAdMob: { summary: "광고를 사용자에게 노출해요. 이 함수는 `loadAppsInTossAdMob` 로 미리 불러온 광고를 실제로 사용자에게 노출해요." },
  GoogleAdMobIsAppsInTossAdMobLoaded: { summary: "이 함수는 `loadAppsInTossAdMob` 로 광고가 잘 불러와졌는지 확인해요." },
  GrantPromotionReward: { params: { paramsParam: "포인트를 지급하기 위해 필요한 정보예요." }, returns: "포인트 지급 결과를 반환해요. { key: string }: 포인트 지급에 성공했어요. key는 리워드 키를 의미해요. { errorCode: string, message: string }: 포인트 지급에 실패했어요. 에러 코드는 다음과 같아요. \"4100\": 프로모션 정보를 찾을 수 없을 때 \"4104\": 프로모션이 중지되었을 때 \"4105\": 프로모션이 종료되었을 때 \"4108\": 프로모션이 승인되지 않았을 때 \"4109\": 프로모션이 실행중이 아닐 때 \"4110\": 리워드를 지급/회수할 수 없을 때 \"4112\": 프로모션 머니가 부족할 때 \"4113\": 이미 지급/회수된 내역일 때 \"4114\": 프로모션에 설정된 1회 지급 금액을 초과할 때 'ERROR': 알 수 없는 오류가 발생했어요. undefined: 앱 버전이 최소 지원 버전보다 낮아요." },
  GrantPromotionRewardForGame: { params: { paramsParam: "포인트를 지급하기 위해 필요한 정보예요." }, returns: "포인트 지급 결과를 반환해요. { key: string }: 포인트 지급에 성공했어요. key는 리워드 키를 의미해요. { errorCode: string, message: string }: 포인트 지급에 실패했어요. 에러 코드는 다음과 같아요. \"40000\": 게임이 아닌 미니앱에서 호출했을 때 \"4100\": 프로모션 정보를 찾을 수 없을 때 \"4104\": 프로모션이 중지되었을 때 \"4105\": 프로모션이 종료되었을 때 \"4108\": 프로모션이 승인되지 않았을 때 \"4109\": 프로모션이 실행중이 아닐 때 \"4110\": 리워드를 지급/회수할 수 없을 때 \"4112\": 프로모션 머니가 부족할 때 \"4113\": 이미 지급/회수된 내역일 때 \"4114\": 프로모션에 설정된 1회 지급 금액을 초과할 때 'ERROR': 알 수 없는 오류가 발생했어요. undefined: 앱 버전이 최소 지원 버전보다 낮아요." },
  IsMinVersionSupported: { params: { minVersions: "플랫폼별 최소 버전 요구사항을 지정하는 객체예요." }, returns: "현재 앱 버전이 최소 버전 이상이면 true, 그렇지 않으면 false를 반환해요." },
  OpenGameCenterLeaderboard: { returns: "리더보드 웹뷰를 호출해요. 앱 버전이 낮으면 아무 동작도 하지 않고 undefined를 반환해요." },
  OpenPDFViewer: { params: { paramsParam: "PDF 데이터와 파일 이름을 담은 객체예요." }, returns: "PDF 뷰어가 닫히면 'CLOSE'를 반환해요." },
  OpenURL: { params: { url: "열고자 하는 URL 주소" }, returns: "URL이 성공적으로 열렸을 때 해결되는 Promise" },
  PartnerAddAccessoryButton: { summary: "상단 네비게이션의 악세서리 버튼을 추가해요. callback에 대한 정의는 `tdsEvent.addEventListener(\"navigationAccessoryEvent\", callback)`를 참고해주세요." },
  PartnerRemoveAccessoryButton: { summary: "상단 네비게이션의 악세서리 버튼을 제거해요." },
  RequestTossPayPaysBilling: { params: { options: "정기결제창을 띄울 때 필요한 옵션이에요." }, returns: "인증 성공 여부를 포함한 결과를 반환해요." },
  SaveBase64Data: { params: { paramsParam: "저장할 데이터와 파일 정보를 담은 객체예요." } },
  SetDeviceOrientation: { params: { options: "화면 방향 설정 값이에요." }, returns: "화면 방향 설정이 완료되면 해결되는 Promise를 반환해요." },
  SetIosSwipeGestureEnabled: { params: { options: "스와이프하여 뒤로가기 기능을 활성화하거나 비활성화하는 옵션이에요." } },
  SetScreenAwakeMode: { params: { options: "화면 항상 켜짐 모드의 설정 값이에요." }, returns: "현재 화면 항상 켜짐 모드의 설정 상태를 반환해요." },
  SetSecureScreen: { params: { options: "화면 캡쳐 설정 옵션이에요." }, returns: "현재 설정된 캡쳐 차단 상태를 반환해요." },
  SubmitGameCenterLeaderBoardScore: { returns: "점수 제출 결과를 반환해요. 앱 버전이 최소 지원 버전보다 낮으면 아무 동작도 하지 않고 undefined를 반환해요." },
  GetUserKeyForGame: { summary: "v2 호환용 별칭이에요. GetAnonymousKey 또는 UserGetAnonymousKey를 사용해주세요." },
  MigrationGetOriginStorage: { summary: "이전 origin과 현재 origin의 localStorage, IndexedDB, OPFS 덤프를 가져와요. SDK 3.0.x origin에 저장된 데이터를 3.1.0 이상으로 옮길 때 사용해요.", returns: "previous(이전 origin)와 current(현재 origin) 덤프를 반환해요. 읽지 못한 저장소는 각 덤프의 Errors에 담겨요. IndexedDB 레코드의 바이너리 값은 base64 문자열로 와요." },
  OpenPermissionDialog: { summary: "사용자에게 권한을 요청하는 다이얼로그를 띄워요.", returns: "권한의 현재 상태를 반환해요. 반환값은 다음 중 하나예요: allowed: 권한이 허용된 상태예요. denied: 권한이 거부된 상태예요." },
  RequestPermission: { summary: "권한을 요청하고 사용자가 선택한 최종 상태를 받아요.", returns: "사용자가 선택한 최종 권한 상태를 반환해요. 반환값은 다음 중 하나예요: allowed: 권한이 허용된 경우 denied: 권한이 거부된 경우" },
  SafeAreaInsetsGet: { summary: "현재 Safe Area insets(top/bottom/left/right)를 반환해요." },
  TossAdsAttach: { summary: "더 이상 권장되지 않아요. TossAdsAttachBanner를 사용해주세요." },
  TossAdsAttachBanner: { summary: "배너 광고를 target에 붙여요." },
  TossAdsDestroy: { summary: "slotId에 해당하는 광고 슬롯을 제거해요." },
  TossAdsDestroyAll: { summary: "붙어 있는 광고 슬롯을 모두 제거해요." },
  TossAdsInitialize: { summary: "토스 광고를 초기화해요." },
  TossPayCheckoutPayment: { summary: "토스페이 결제창을 띄우고 사용자 인증을 수행해요. 실제 결제 처리는 인증 성공 후 서버에서 별도로 해야 해요. 더 이상 사용되지 않으니 TossPayAuthorize를 사용해주세요." },
  TossPayRequestTossPayPaysBilling: { summary: "토스페이 정기결제창을 띄우고 사용자 인증을 수행해요. 실제 결제 처리는 인증 성공 후 서버에서 별도로 해야 해요. 더 이상 사용되지 않으니 TossPayAuthorizeSubscription을 사용해주세요." },
};

interface DocTarget {
  pascalName: string;
  description?: string;
  returnDescription?: string;
  parameters: Array<{ name: string; description?: string }>;
}

/**
 * 번들 JSDoc에 비어 있는 요약/@param/@returns를 DOC_FALLBACKS로 채운다.
 * 이미 값이 있는 항목은 건드리지 않는다. 채운 API 이름 목록을 반환한다.
 */
export function applyDocFallbacks(
  apis: DocTarget[],
  escapeParamName: (name: string) => string,
): string[] {
  const applied: string[] = [];
  for (const api of apis) {
    const fb = DOC_FALLBACKS[api.pascalName];
    if (!fb) continue;
    let changed = false;
    if (fb.summary && !api.description?.trim()) {
      api.description = fb.summary;
      changed = true;
    }
    if (fb.returns && !api.returnDescription?.trim()) {
      api.returnDescription = fb.returns;
      changed = true;
    }
    if (fb.params) {
      for (const param of api.parameters) {
        const text = fb.params[escapeParamName(param.name)];
        if (text && !param.description?.trim()) {
          param.description = text;
          changed = true;
        }
      }
    }
    if (changed) applied.push(api.pascalName);
  }
  return applied;
}

/**
 * 3.x d.ts의 JSDoc에 없는 타입 멤버 설명. 키는 `C# 타입 이름.C# 멤버 이름`.
 * 생성된 타입 코드에서 설명이 없는 필드에만 `<summary>`를 채운다 (이미 있으면 건드리지 않는다).
 */
export const TYPE_MEMBER_DOC_FALLBACKS: Readonly<Record<string, string>> = {
  'CheckoutPaymentResult.Reason': '인증이 실패했을 경우의 이유예요.',
  'CheckoutPaymentResult.Success': '인증이 성공했는지 여부예요.',
  'ContactEntity.Name': '연락처 이름이에요.',
  'ContactEntity.PhoneNumber': '연락처 전화번호로, 문자열 형식이에요.',
  'FetchAlbumPhotosOptions.Base64': '이미지를 base64 형식으로 반환할지 설정해요. 기본값은 false예요.',
  'FetchAlbumPhotosOptions.MaxCount': '가져올 사진의 최대 개수를 설정해요. 숫자를 입력하고 기본값은 10이에요.',
  'FetchAlbumPhotosOptions.MaxWidth': '사진의 최대 폭을 제한해요. 단위는 픽셀이고 기본값은 1024이에요.',
  'GetCurrentLocationOptions.Accuracy': '위치 정보를 가져올 정확도 수준이에요.',
  'ImageResponse.DataUri': '사진의 데이터 URI예요. base64 옵션이 true인 경우 Base64 문자열로 반환돼요.',
  'ImageResponse.Id': '가져온 사진의 고유 ID예요.',
  'OriginStorageDumpIndexedDBObjectStore.KeyPath': '저장소의 keyPath예요. 문자열, 문자열 배열(JArray), null 중 하나로 와요.',
  'OriginStorageDumpIndexedDBObjectStoreIndex.KeyPath': '인덱스의 keyPath예요. 문자열 또는 문자열 배열(JArray)로 와요.',
  'LocationCoords.Accuracy': '위치 정확도',
  'LocationCoords.Altitude': '높이',
  'LocationCoords.AltitudeAccuracy': '고도 정확도',
  'LocationCoords.Heading': '방향',
  'LocationCoords.Latitude': '위도',
  'LocationCoords.Longitude': '경도',
  'OpenCameraOptions.Base64': '이미지를 Base64 형식으로 반환할지 여부를 나타내는 불리언 값이에요. 기본값: false.',
  'OpenCameraOptions.MaxWidth': '이미지의 최대 너비를 나타내는 숫자 값이에요. 기본값: 1024.',
  'RequestTossPayPaysBillingResult.Reason': '인증이 실패했을 경우의 이유예요.',
  'RequestTossPayPaysBillingResult.Success': '인증이 성공했는지 여부예요.',
  'ShowFullScreenAdEvent.Data': 'Event data (only for userEarnedReward)',
  'ShowFullScreenAdEvent.Type': 'Event type: clicked, dismissed, failedToShow, impression, show, userEarnedReward, requested',
  'StartUpdateLocationOptions.Accuracy': '위치 정확도를 설정해요.',
  'StartUpdateLocationOptions.DistanceInterval': '위치 변경 거리를 미터(m) 단위로 설정해요.',
  'StartUpdateLocationOptions.TimeInterval': '위치 업데이트 주기를 밀리초(ms) 단위로 설정해요.',
};

const escapeRegExp = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/**
 * 타입 코드(C# 클래스 하나 이상)에서 설명이 빠진 필드에 TYPE_MEMBER_DOC_FALLBACKS의 `<summary>`를 채운다.
 */
export function applyTypeMemberDocFallbacks(code: string): string {
  let result = code;
  for (const [key, doc] of Object.entries(TYPE_MEMBER_DOC_FALLBACKS)) {
    const [typeName, member] = key.split('.');
    const classStart = result.search(new RegExp(`public class ${escapeRegExp(typeName)}\\b`));
    if (classStart < 0) continue;
    const classEnd = result.indexOf('\n    }\n', classStart);
    const body = result.slice(classStart, classEnd < 0 ? result.length : classEnd);
    const field = new RegExp(`(\\n)( {8}\\[Preserve\\]\\n {8}\\[JsonProperty\\("[^"]*"\\)\\]\\n {8}public [^\\n]*? ${escapeRegExp(member)}(?: = [^;\\n]+)?;)`);
    const match = field.exec(body);
    if (!match) continue;
    // 바로 위가 설명 주석이면 건드리지 않는다
    const before = body.slice(0, match.index);
    if (/\/\/\/ [^\n]*\n?$/.test(before) && before.trimEnd().endsWith('</summary>')) continue;
    const insertAt = classStart + match.index + 1;
    const safe = doc.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    result = `${result.slice(0, insertAt)}        /// <summary>${safe}</summary>\n${result.slice(insertAt)}`;
  }
  return result;
}
