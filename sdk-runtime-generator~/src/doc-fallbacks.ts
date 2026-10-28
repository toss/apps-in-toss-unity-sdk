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
  OpenPermissionDialog: { returns: "권한의 현재 상태를 반환해요. 반환값은 다음 중 하나예요: allowed: 권한이 허용된 상태예요. denied: 권한이 거부된 상태예요." },
  OpenURL: { params: { url: "열고자 하는 URL 주소" }, returns: "URL이 성공적으로 열렸을 때 해결되는 Promise" },
  PartnerAddAccessoryButton: { summary: "상단 네비게이션의 악세서리 버튼을 추가해요. callback에 대한 정의는 `tdsEvent.addEventListener(\"navigationAccessoryEvent\", callback)`를 참고해주세요." },
  PartnerRemoveAccessoryButton: { summary: "상단 네비게이션의 악세서리 버튼을 제거해요." },
  RequestPermission: { returns: "사용자가 선택한 최종 권한 상태를 반환해요. 반환값은 다음 중 하나예요: allowed: 권한이 허용된 경우 denied: 권한이 거부된 경우" },
  RequestTossPayPaysBilling: { params: { options: "정기결제창을 띄울 때 필요한 옵션이에요." }, returns: "인증 성공 여부를 포함한 결과를 반환해요." },
  SaveBase64Data: { params: { paramsParam: "저장할 데이터와 파일 정보를 담은 객체예요." } },
  SetDeviceOrientation: { params: { options: "화면 방향 설정 값이에요." }, returns: "화면 방향 설정이 완료되면 해결되는 Promise를 반환해요." },
  SetIosSwipeGestureEnabled: { params: { options: "스와이프하여 뒤로가기 기능을 활성화하거나 비활성화하는 옵션이에요." } },
  SetScreenAwakeMode: { params: { options: "화면 항상 켜짐 모드의 설정 값이에요." }, returns: "현재 화면 항상 켜짐 모드의 설정 상태를 반환해요." },
  SetSecureScreen: { params: { options: "화면 캡쳐 설정 옵션이에요." }, returns: "현재 설정된 캡쳐 차단 상태를 반환해요." },
  SubmitGameCenterLeaderBoardScore: { returns: "점수 제출 결과를 반환해요. 앱 버전이 최소 지원 버전보다 낮으면 아무 동작도 하지 않고 undefined를 반환해요." },
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
