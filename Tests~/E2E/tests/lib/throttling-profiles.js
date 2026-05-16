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

export const NETWORK_KEYS = Object.keys(NETWORK_PROFILES);
export const CPU_KEYS = Object.keys(CPU_PROFILES);
export const UNITY_VERSIONS = Object.keys(UNITY_VERSION_PORTS);
export const COMPRESSIONS = ['disabled', 'brotli'];
