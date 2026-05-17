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

// 측정 대상 네트워크. 3G(regular-3g/good-3g)는 비압축 빌드(~92MB)에서 cold
// 다운로드가 cell당 수십 분이라 전체 매트릭스 측정이 비현실적이어서 제외.
// NETWORK_PROFILES에는 정의를 남겨 향후 재사용 가능하게 둔다.
export const NETWORK_KEYS = ['regular-4g', 'wifi'];
export const CPU_KEYS = Object.keys(CPU_PROFILES);
export const UNITY_VERSIONS = Object.keys(UNITY_VERSION_PORTS);
export const COMPRESSIONS = ['disabled', 'brotli'];
