// @ts-check
// e2e-full-pipeline.test.js에서 추출한 경로 상수·환경 플래그·포트 계산.
// 이 디렉터리(lib/) 파일은 이름이 *.test.js/*.spec.js가 되면 안 된다(playwright 기본 testMatch에 잡힘).
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

// ES Module에서 __dirname 대체 (lib/ 안이므로 한 단계 위가 Tests~/E2E/tests다)
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
export const TESTS_DIR = path.resolve(__dirname, '..');

// 모바일 에뮬레이션 활성화 여부 (macOS CI에서만 true)
export const isMobileEmulation = process.env.MOBILE_EMULATION === 'true';

// CPU 쓰로틀링 배율 (환경변수로 제어, 기본값: 0 = 비활성화)
export const cpuThrottleRate = parseInt(process.env.CPU_THROTTLE_RATE || '0', 10);

// 경로 상수
export const PROJECT_ROOT = path.resolve(TESTS_DIR, '../../..');

// UNITY_PROJECT_PATH 환경변수로 프로젝트 경로 지정 가능
function findSampleProject() {
  const envPath = process.env.UNITY_PROJECT_PATH;
  if (envPath && fs.existsSync(envPath)) {
    return envPath;
  }

  const versionPatterns = ['6000.2', '6000.0', '2022.3', '2021.3'];
  for (const version of versionPatterns) {
    const projectPath = path.resolve(TESTS_DIR, `../SampleUnityProject-${version}`);
    const distPath = path.resolve(projectPath, 'ait-build/dist/web');
    if (fs.existsSync(distPath)) {
      console.log(`📁 Auto-detected project: SampleUnityProject-${version}`);
      return projectPath;
    }
  }

  const legacyPath = path.resolve(TESTS_DIR, '../SampleUnityProject');
  if (fs.existsSync(legacyPath)) {
    return legacyPath;
  }

  for (const version of versionPatterns) {
    const projectPath = path.resolve(TESTS_DIR, `../SampleUnityProject-${version}`);
    if (fs.existsSync(projectPath)) {
      return projectPath;
    }
  }

  return path.resolve(TESTS_DIR, '../SampleUnityProject');
}

export const SAMPLE_PROJECT = findSampleProject();
export const AIT_BUILD = path.resolve(SAMPLE_PROJECT, 'ait-build');
export const DIST_WEB = path.resolve(AIT_BUILD, 'dist/web');

// 벤치마크 기준
// E2E CI는 AIT_DEVELOPMENT_BUILD=true + AIT_COMPRESSION_FORMAT=0(Disabled)로 빌드하여
// 빌드 wallclock을 단축한다(unity-build.yml 참조).
// Dev Build는 wasm 크기가 Release 대비 2-3배 커지고 압축도 비활성화되므로
// MAX_BUILD_SIZE_MB 단언을 사용할 수 없어 isDevBuild일 때 스킵한다.
export const isDevBuild = process.env.AIT_DEVELOPMENT_BUILD === 'true';
export const BENCHMARKS = isMobileEmulation ? {
  MAX_LOAD_TIME_MS: 30000,
  MAX_BUILD_SIZE_MB: 50,
} : {
  MAX_LOAD_TIME_MS: 10000,
  MAX_BUILD_SIZE_MB: 50,
};

/**
 * Unity 버전에서 고유 포트 오프셋 계산
 */
function getPortOffsetFromUnityVersion(projectPath) {
  const match = projectPath.match(/SampleUnityProject-(\d+)\.(\d+)/);
  if (!match) return 0;

  const major = parseInt(match[1], 10);
  const minor = parseInt(match[2], 10);

  if (major === 2021) return 0;
  if (major === 2022) return 1;
  if (major === 6000 && minor === 0) return 2;
  if (major === 6000 && minor === 2) return 3;
  if (major === 6000 && minor === 3) return 4;
  return 0;
}

export const PORT_OFFSET = getPortOffsetFromUnityVersion(SAMPLE_PROJECT);
export const VITE_DEV_PORT = 8081 + PORT_OFFSET;

export const SERVER_PORT = 4173 + PORT_OFFSET;
console.log(`📦 Unity project: ${SAMPLE_PROJECT}`);
console.log(`🔌 Server port: ${SERVER_PORT} (offset: ${PORT_OFFSET})`);
