// Chrome DevTools 표준 네트워크 프리셋 (bytes/sec, ms).
// CDP `Network.emulateNetworkConditions` 인자.
export const NETWORK_PROFILES = {
  'regular-3g': {
    downloadThroughput: (250 * 1024) / 8,
    uploadThroughput: (100 * 1024) / 8,
    latency: 300,
  },
  'good-3g': {
    downloadThroughput: (1.0 * 1024 * 1024) / 8,
    uploadThroughput: (0.4 * 1024 * 1024) / 8,
    latency: 40,
  },
  'regular-4g': {
    downloadThroughput: (4.0 * 1024 * 1024) / 8,
    uploadThroughput: (3.0 * 1024 * 1024) / 8,
    latency: 20,
  },
  'wifi': {
    downloadThroughput: (30 * 1024 * 1024) / 8,
    uploadThroughput: (15 * 1024 * 1024) / 8,
    latency: 2,
  },
  // 한국 LTE 실측 모사. 다운로드/업로드는 과기정통부·NIA 「2024 통신서비스
  // 품질평가」 3사 평균(LTE down 151.92Mbps / up 39.39Mbps)을 반올림한 값.
  // 정부 평가는 LTE latency를 개별 공표하지 않아 Opensignal 한국 4G 실측
  // (~37–38ms)을 채택. Unity WebGL은 자산을 순차 fetch하므로 latency가 누적
  // 로딩에 직결되어 실측값 반영이 중요하다.
  'kr-lte': {
    downloadThroughput: (150 * 1024 * 1024) / 8,
    uploadThroughput: (39 * 1024 * 1024) / 8,
    latency: 38,
  },
  // 한국 상용 WiFi 실측 모사. 다운로드는 과기정통부·NIA 「2024 통신서비스
  // 품질평가」 상용 와이파이 평균(374.89Mbps)을 반올림. 정부 평가가 상용
  // WiFi 업로드/latency를 따로 공표하지 않아, 업로드는 고정망 대칭성과 5G
  // 업로드(90Mbps)를 근거로 100Mbps 보수 추정, latency는 고정망 WiFi 일반
  // 수준 15ms로 둔다.
  'kr-wifi': {
    downloadThroughput: (375 * 1024 * 1024) / 8,
    uploadThroughput: (100 * 1024 * 1024) / 8,
    latency: 15,
  },
  // 가설 검증용 LTE 변형 — 과기정통부·NIA 「2024 통신서비스 품질평가」 LTE
  // 다운로드 통신사별 실측(SKT 238.49 / KT 166.81 / LGU+ 128.85Mbps)의
  // 최저·최고를 그대로 채택한다. 기존 kr-lte(3사 평균 기반)는 건드리지 않는다.
  //
  // kr-lte-slow: LGU+ 실측 하단. 업로드·latency는 LTE 하위 사업자 수준의
  // 보수적 값(up 30Mbps / latency 45ms)을 둔다.
  'kr-lte-slow': {
    downloadThroughput: (128 * 1024 * 1024) / 8,
    uploadThroughput: (30 * 1024 * 1024) / 8,
    latency: 45,
  },
  // kr-lte-fast: SKT 실측 상단. 업로드·latency는 LTE 상위 사업자 수준
  // (up 50Mbps / latency 30ms).
  'kr-lte-fast': {
    downloadThroughput: (238 * 1024 * 1024) / 8,
    uploadThroughput: (50 * 1024 * 1024) / 8,
    latency: 30,
  },
};

// CDP `Emulation.setCPUThrottlingRate` 슬로우다운 배율. 코어 수 emulation 불가, 정수만.
export const CPU_PROFILES = {
  'cpu-1x': { rate: 1 },
  'cpu-2x': { rate: 2 },
  'cpu-4x': { rate: 4 },
  'cpu-6x': { rate: 6 },
};

export const UNITY_VERSION_PORTS = {
  '2021.3': 4173,
  '2022.3': 4174,
  '6000.0': 4175,
  '6000.2': 4176,
  '6000.3': 4177,
};

// 측정 대상 네트워크. 한국 실측 기반 두 프로파일(kr-lte, kr-wifi)로 측정해
// 모바일(LTE)과 고정망(WiFi) 환경에서 필러 간 차이가 어떻게 달라지는지 본다.
// 나머지 프리셋(3G/4G/wifi 표준)은 정의를 남겨 향후 재사용 가능하게 둔다.
export const NETWORK_KEYS = ['kr-lte', 'kr-wifi'];
// 3-필러 벤치마크는 cpu-4x 단일에 고정 (저성능 모바일 근사).
export const CPU_KEYS = ['cpu-4x'];
export const UNITY_VERSIONS = Object.keys(UNITY_VERSION_PORTS);

// 가설 검증 벤치마크(scripts/benchmark-hypothesis.sh) 전용 축. 두 가설
// (p1→p2 네트워크 의존 / p2→p3 CPU 의존)을 직접 검증하려면 네트워크·CPU를
// 각각 3점으로 넓혀야 한다. 위 NETWORK_KEYS/CPU_KEYS는 기존 3-필러 벤치마크
// 동작 보존을 위해 그대로 두고, 가설 측정은 이 상수를 사용한다.
export const HYPOTHESIS_NETWORK_KEYS = ['kr-lte-slow', 'kr-lte-fast', 'kr-wifi'];
export const HYPOTHESIS_CPU_KEYS = ['cpu-2x', 'cpu-4x', 'cpu-6x'];

// 3-필러 — 압축/전송 설정의 세 가지 현실 시나리오.
// 각 필러는 (빌드 산출물 dist) × (정적 서버의 Content-Encoding 동작) 조합.
//
//   pillar1 (압축 미설정)        : Unity compressionFormat=Disabled. 평문 .data/
//                                  .wasm/.js 산출물을, 정적 서버(=CDN 근사)가
//                                  on-the-fly gzip으로 Content-Encoding: gzip
//                                  전송 → 브라우저 네이티브 gzip 디코딩.
//   pillar2 (압축 O, 헤더 X)     : Unity Brotli .unityweb 산출물을, 서버가
//                                  Content-Encoding 헤더 없이 전송 → Unity
//                                  로더의 JS decompressionFallback 디코딩.
//   pillar3 (압축 O, 헤더 O)     : Unity Brotli .unityweb 산출물을, 서버가
//                                  Content-Encoding: br 전송 → 브라우저 네이티브
//                                  Brotli 디코딩. (정석 설정)
//
// dist     — benchmark.sh Phase 1이 생성하는 산출물 디렉토리 접미사.
// encoding — 정적 서버 동작: 'cdn-gzip'=평문에 on-the-fly gzip 부여,
//            'none'=.unityweb에 Content-Encoding 생략, 'native'=.unityweb의
//            내장 압축에 맞는 Content-Encoding 부여.
export const PILLARS = {
  pillar1: { dist: 'web-disabled', encoding: 'cdn-gzip', label: '압축 미설정 + CDN gzip' },
  pillar2: { dist: 'web-brotli', encoding: 'none', label: '압축 O + Content-Encoding 누락' },
  pillar3: { dist: 'web-brotli', encoding: 'native', label: '압축 O + Content-Encoding O' },
};
export const PILLAR_KEYS = Object.keys(PILLARS);
