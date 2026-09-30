const fs = require('fs');
const os = require('os');
const path = require('path');
const { execFileSync } = require('child_process');

const overrideVersion = process.env.OVERRIDE_VERSION;
const onWindows = process.platform === 'win32';
// Windows에서 npx/pnpm은 .cmd 래퍼라 shell:true 필요. POSIX는 그대로.
const run = (cmd, args, opts = {}) =>
  execFileSync(cmd, args, { stdio: 'inherit', shell: onWindows, ...opts });

console.log('=== SDK Version Override ===');
console.log(`Target SDK version: ${overrideVersion}`);
console.log('');

// 1. package.json 버전 변경 (메타데이터용 — AITVersion.Version의 소스)
console.log('=== Updating package.json version ===');
{
  const pkgPath = 'package.json';
  const pkg = JSON.parse(fs.readFileSync(pkgPath, 'utf8'));
  console.log(`Original version: ${pkg.version}`);
  pkg.version = overrideVersion;
  fs.writeFileSync(pkgPath, JSON.stringify(pkg, null, 2) + '\n');
  console.log(`Overridden version: ${pkg.version}`);
}
// lockfile을 만든 pnpm과 같은 버전을 쓴다(CLAUDE.md "pnpm 버전 핀 동기화").
// floating pnpm@10은 pnpm 11 lockfile을 그대로 쓰지 못하고 전체를 다시 resolve했다.
const pnpm = ['-y', `pnpm@${JSON.parse(fs.readFileSync('package.json', 'utf8')).packageManager.replace(/^pnpm@/, '')}`];

// 2. BuildConfig의 web-framework 버전만 동기화
// 주의: override는 web-framework 전용 버전(예: 3.0.0-beta.<hash>)이다.
// @apps-in-toss/web-analytics는 별도 버전 라인(latest=2.6.1)이라 해당
// 버전이 npm에 존재하지 않으며, web-framework 3.x는 web-analytics에
// 의존하지도 않는다(검증됨). web-analytics까지 override로 덮으면
// ait-build의 pnpm install이 ERR_PNPM_NO_MATCHING_VERSION으로 실패하므로
// web-analytics는 BuildConfig 기본 핀(2.6.1)을 그대로 둔다.
console.log('');
console.log('=== Syncing BuildConfig web-framework version ===');
{
  const buildConfig = 'WebGLTemplates/AITTemplate/BuildConfig~/package.json';
  const pkg = JSON.parse(fs.readFileSync(buildConfig, 'utf8'));
  for (const section of ['dependencies', 'devDependencies']) {
    if (!pkg[section]) continue;
    if (pkg[section]['@apps-in-toss/web-framework']) {
      pkg[section]['@apps-in-toss/web-framework'] = overrideVersion;
    }
  }
  fs.writeFileSync(buildConfig, JSON.stringify(pkg, null, 2) + '\n');
  console.log('BuildConfig updated (web-analytics는 의도적으로 유지):');
  console.log(fs.readFileSync(buildConfig, 'utf8'));
}

// 3. SDK 코드 재생성 (러너별 영속 pnpm store)
// self-hosted 러너는 잡을 하나씩만 돌리므로 RUNNER_NAME별 store는 동시 쓰기가 없다.
// 매번 빈 store로 ~2,100개 패키지를 받으면 레지스트리 fetch 타임아웃이 반복됐다
// (2026-09-29 override 런 두 번의 빌드 레그 20개 중 16개가 install 단계 TimeoutError로 실패).
console.log('');
console.log('=== SDK 코드 재생성 ===');
const generatorDir = 'sdk-runtime-generator~';
const store = path.join(process.env.RUNNER_TOOL_CACHE || os.tmpdir(), `pnpm-store-sdk-override-${process.env.RUNNER_NAME || 'local'}`);
const fetchArgs = ['--store-dir', store, '--prefer-offline', '--fetch-retries', '5',
  '--fetch-retry-mintimeout', '15000', '--fetch-retry-maxtimeout', '90000', '--fetch-timeout', '120000'];
run('npx', [...pnpm, 'install', '--frozen-lockfile', ...fetchArgs], { cwd: generatorDir });
run('npx', [...pnpm, 'update', `@apps-in-toss/web-framework@${overrideVersion}`,
  '--force', '--registry', 'https://registry.npmjs.org', ...fetchArgs], { cwd: generatorDir });

// 설치된 버전 검증
let installedVersion = 'unknown';
try {
  const listJson = execFileSync('npx', [...pnpm, 'list', '@apps-in-toss/web-framework', '--json'],
    { cwd: generatorDir, shell: onWindows, encoding: 'utf8' });
  installedVersion = JSON.parse(listJson)[0].dependencies['@apps-in-toss/web-framework'].version;
} catch (e) {
  console.log(`::warning::설치 버전 파싱 실패: ${e.message}`);
}
console.log(`요청 버전: ${overrideVersion}`);
console.log(`설치된 버전: ${installedVersion}`);
if (installedVersion !== overrideVersion) {
  console.log(`::warning::버전 불일치 - 요청: ${overrideVersion}, 설치됨: ${installedVersion}`);
}

console.log('SDK 코드 생성 중...');
run('npx', [...pnpm, 'generate'], { cwd: generatorDir });
console.log('SDK 코드 생성 완료');

// 4. 재생성된 SDK 파일 확인
console.log('');
console.log('=== Regenerated SDK files ===');
for (const f of fs.readdirSync('Runtime/SDK').slice(0, 20)) console.log(f);
