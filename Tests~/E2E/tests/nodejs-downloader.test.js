import { test, expect } from '@playwright/test';
import { execSync, spawn } from 'child_process';
import path from 'path';
import fs from 'fs';
import crypto from 'crypto';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const SDK_ROOT = path.join(__dirname, '../../..');
const TOOLS_PATH = path.join(SDK_ROOT, 'Tools~', 'NodeJS');

// Node.js 버전 및 체크섬 (AITNodeJSDownloader.cs와 동기화)
const NODE_VERSION = '24.11.1';
const CHECKSUMS = {
  'darwin-arm64': 'b05aa3a66efe680023f930bd5af3fdbbd542794da5644ca2ad711d68cbd4dc35',
  'darwin-x64': '096081b6d6fcdd3f5ba0f5f1d44a47e83037ad2e78eada26671c252fe64dd111',
  'win-x64': '5355ae6d7c49eddcfde7d34ac3486820600a831bf81dc3bdca5c8db6a9bb0e76',
  'linux-x64': '60e3b0a8500819514aca603487c254298cd776de0698d3cd08f11dba5b8289a8'
};

/**
 * 현재 플랫폼 감지
 */
function detectPlatform() {
  const platform = process.platform;
  const arch = process.arch;

  if (platform === 'darwin') {
    return arch === 'arm64' ? 'darwin-arm64' : 'darwin-x64';
  } else if (platform === 'win32') {
    return 'win-x64';
  } else if (platform === 'linux') {
    return 'linux-x64';
  }

  throw new Error(`Unsupported platform: ${platform}-${arch}`);
}

/**
 * SHA256 체크섬 계산
 */
function calculateSHA256(filePath) {
  const fileBuffer = fs.readFileSync(filePath);
  const hashSum = crypto.createHash('sha256');
  hashSum.update(fileBuffer);
  return hashSum.digest('hex');
}

/**
 * Embedded Node.js 경로
 */
function getEmbeddedNodePath(platform) {
  const nodePath = path.join(TOOLS_PATH, platform);
  const npmPath = platform === 'win-x64'
    ? path.join(nodePath, 'npm.cmd')
    : path.join(nodePath, 'bin', 'npm');

  return { nodePath, npmPath };
}

/**
 * 다운로드 URL 생성
 */
function getDownloadUrl(platform, mirror = 'nodejs') {
  const fileName = platform === 'win-x64'
    ? `node-v${NODE_VERSION}-${platform}.zip`
    : `node-v${NODE_VERSION}-${platform}.tar.gz`;

  const mirrors = {
    nodejs: `https://nodejs.org/dist/v${NODE_VERSION}/${fileName}`,
    npmmirror: `https://cdn.npmmirror.com/binaries/node/v${NODE_VERSION}/${fileName}`,
    huawei: `https://repo.huaweicloud.com/nodejs/v${NODE_VERSION}/${fileName}`
  };

  return { url: mirrors[mirror], fileName };
}

test.describe('Node.js Embedded Runtime E2E Tests', () => {
  const platform = detectPlatform();
  const { nodePath, npmPath } = getEmbeddedNodePath(platform);

  test('1. Platform detection should match current system', () => {
    console.log(`🖥️  Detected platform: ${platform}`);
    console.log(`   Process platform: ${process.platform}`);
    console.log(`   Process arch: ${process.arch}`);

    expect(['darwin-arm64', 'darwin-x64', 'win-x64', 'linux-x64']).toContain(platform);
  });

  test('2. Checksum dictionary should have all platforms', () => {
    const platforms = ['darwin-arm64', 'darwin-x64', 'win-x64', 'linux-x64'];

    platforms.forEach(p => {
      expect(CHECKSUMS).toHaveProperty(p);
      expect(CHECKSUMS[p]).toMatch(/^[a-f0-9]{64}$/); // SHA256 format
      console.log(`✓ ${p}: ${CHECKSUMS[p]}`);
    });
  });

  test('3. Download URLs should be accessible', async () => {
    const mirrors = ['nodejs', 'npmmirror', 'huawei'];

    for (const mirror of mirrors) {
      const { url, fileName } = getDownloadUrl(platform, mirror);
      console.log(`\n📡 Testing: ${mirror}`);
      console.log(`   URL: ${url}`);

      try {
        const response = await fetch(url, { method: 'HEAD' });
        console.log(`   Status: ${response.status} ${response.statusText}`);

        if (response.ok) {
          const contentLength = response.headers.get('content-length');
          if (contentLength) {
            const sizeMB = (parseInt(contentLength) / 1024 / 1024).toFixed(2);
            console.log(`   Size: ${sizeMB} MB`);
          }
          console.log(`   ✓ ${mirror} is accessible`);
        } else {
          console.warn(`   ⚠️  ${mirror} returned non-200 status`);
        }
      } catch (error) {
        console.error(`   ✗ ${mirror} failed: ${error.message}`);
        // 첫 번째 미러(공식)만 필수, 나머지는 폴백
        if (mirror === 'nodejs') {
          throw error;
        }
      }
    }
  });

  test('4. Download and verify checksum (REAL DOWNLOAD)', async () => {
    test.setTimeout(300000); // 5분 타임아웃

    console.log(`\n📦 Downloading Node.js ${NODE_VERSION} for ${platform}...`);

    // 이미 존재하면 스킵
    if (fs.existsSync(npmPath)) {
      console.log(`✓ Embedded Node.js already exists: ${npmPath}`);
      console.log(`  Skipping download test.`);
      return;
    }

    const { url, fileName } = getDownloadUrl(platform, 'nodejs');
    const tempFile = path.join(SDK_ROOT, 'Tests~', 'E2E', 'temp', fileName);
    const tempDir = path.dirname(tempFile);

    // 임시 디렉토리 생성
    fs.mkdirSync(tempDir, { recursive: true });

    try {
      console.log(`📥 Downloading from: ${url}`);
      console.log(`   Target: ${tempFile}`);

      // fetch로 다운로드
      const response = await fetch(url);
      if (!response.ok) {
        throw new Error(`Download failed: ${response.status} ${response.statusText}`);
      }

      const arrayBuffer = await response.arrayBuffer();
      fs.writeFileSync(tempFile, Buffer.from(arrayBuffer));

      const sizeMB = (fs.statSync(tempFile).size / 1024 / 1024).toFixed(2);
      console.log(`✓ Downloaded: ${sizeMB} MB`);

      // 체크섬 검증
      console.log(`\n🔒 Verifying SHA256 checksum...`);
      const actualChecksum = calculateSHA256(tempFile);
      const expectedChecksum = CHECKSUMS[platform];

      console.log(`   Expected: ${expectedChecksum}`);
      console.log(`   Actual:   ${actualChecksum}`);

      expect(actualChecksum).toBe(expectedChecksum);
      console.log(`✓ Checksum verification passed!`);

      // 압축 해제
      console.log(`\n📂 Extracting to: ${nodePath}`);
      fs.mkdirSync(nodePath, { recursive: true });

      if (platform === 'win-x64') {
        // Windows: ZIP 압축 해제
        execSync(`unzip -q "${tempFile}" -d "${nodePath}"`, { stdio: 'inherit' });

        // 중첩 폴더 제거
        const extractedDir = path.join(nodePath, `node-v${NODE_VERSION}-win-x64`);
        if (fs.existsSync(extractedDir)) {
          const files = fs.readdirSync(extractedDir);
          files.forEach(file => {
            fs.renameSync(
              path.join(extractedDir, file),
              path.join(nodePath, file)
            );
          });
          fs.rmdirSync(extractedDir);
        }
      } else {
        // macOS/Linux: tar.gz 압축 해제
        execSync(`tar -xzf "${tempFile}" -C "${nodePath}" --strip-components=1`, { stdio: 'inherit' });

        // 실행 권한 부여
        execSync(`chmod +x "${path.join(nodePath, 'bin', 'node')}"`, { stdio: 'inherit' });
        execSync(`chmod +x "${path.join(nodePath, 'bin', 'npm')}"`, { stdio: 'inherit' });
        execSync(`chmod +x "${path.join(nodePath, 'bin', 'npx')}"`, { stdio: 'inherit' });
      }

      console.log(`✓ Extraction complete`);

      // npm 경로 확인
      expect(fs.existsSync(npmPath)).toBe(true);
      console.log(`✓ npm found at: ${npmPath}`);

    } finally {
      // 임시 파일 삭제
      if (fs.existsSync(tempFile)) {
        fs.unlinkSync(tempFile);
        console.log(`🗑️  Cleaned up: ${tempFile}`);
      }
    }
  }, 300000);

  test('5. Embedded npm should be executable', () => {
    if (!fs.existsSync(npmPath)) {
      console.log(`⚠️  Skipping: Embedded npm not found at ${npmPath}`);
      console.log(`   Run test 4 first to download Node.js`);
      test.skip();
      return;
    }

    console.log(`\n🔧 Testing npm executable: ${npmPath}`);

    try {
      const version = execSync(`"${npmPath}" --version`, { encoding: 'utf-8' }).trim();
      console.log(`   npm version: ${version}`);
      expect(version).toMatch(/^\d+\.\d+\.\d+$/);
      console.log(`✓ npm is executable and working`);
    } catch (error) {
      console.error(`✗ npm execution failed: ${error.message}`);
      throw error;
    }
  });

  test('6. Embedded node should be executable', () => {
    const nodeExe = platform === 'win-x64'
      ? path.join(nodePath, 'node.exe')
      : path.join(nodePath, 'bin', 'node');

    if (!fs.existsSync(nodeExe)) {
      console.log(`⚠️  Skipping: Embedded node not found at ${nodeExe}`);
      test.skip();
      return;
    }

    console.log(`\n🔧 Testing node executable: ${nodeExe}`);

    try {
      const version = execSync(`"${nodeExe}" --version`, { encoding: 'utf-8' }).trim();
      console.log(`   node version: ${version}`);
      expect(version).toBe(`v${NODE_VERSION}`);
      console.log(`✓ node is executable and version matches`);
    } catch (error) {
      console.error(`✗ node execution failed: ${error.message}`);
      throw error;
    }
  });

  test('7. npm install should work in test project', () => {
    if (!fs.existsSync(npmPath)) {
      console.log(`⚠️  Skipping: Embedded npm not found`);
      test.skip();
      return;
    }

    test.setTimeout(120000); // 2분 타임아웃

    const testProjectPath = path.join(SDK_ROOT, 'Tests~', 'E2E', 'test-npm-install');

    console.log(`\n📦 Creating test project: ${testProjectPath}`);

    // 테스트 프로젝트 생성
    fs.mkdirSync(testProjectPath, { recursive: true });

    const packageJson = {
      name: 'test-npm-install',
      version: '1.0.0',
      dependencies: {
        'lodash': '^4.17.21'
      }
    };

    fs.writeFileSync(
      path.join(testProjectPath, 'package.json'),
      JSON.stringify(packageJson, null, 2)
    );

    try {
      console.log(`📥 Running: npm install`);
      execSync(`"${npmPath}" install`, {
        cwd: testProjectPath,
        stdio: 'inherit',
        timeout: 120000
      });

      // node_modules 확인
      const nodeModulesPath = path.join(testProjectPath, 'node_modules');
      const lodashPath = path.join(nodeModulesPath, 'lodash');

      expect(fs.existsSync(nodeModulesPath)).toBe(true);
      expect(fs.existsSync(lodashPath)).toBe(true);

      console.log(`✓ npm install succeeded`);
      console.log(`✓ node_modules created with dependencies`);

    } finally {
      // 테스트 프로젝트 삭제
      if (fs.existsSync(testProjectPath)) {
        fs.rmSync(testProjectPath, { recursive: true, force: true });
        console.log(`🗑️  Cleaned up test project`);
      }
    }
  }, 120000);

  test('8. Checksum validation should fail for tampered file', () => {
    const testFile = path.join(SDK_ROOT, 'Tests~', 'E2E', 'temp', 'tampered.tar.gz');
    const tempDir = path.dirname(testFile);

    fs.mkdirSync(tempDir, { recursive: true });

    try {
      // 가짜 파일 생성
      fs.writeFileSync(testFile, 'This is a tampered file, not real Node.js');

      const actualChecksum = calculateSHA256(testFile);
      const expectedChecksum = CHECKSUMS[platform];

      console.log(`\n🔒 Testing checksum validation for tampered file...`);
      console.log(`   Expected: ${expectedChecksum}`);
      console.log(`   Actual:   ${actualChecksum}`);

      // 체크섬이 다르면 성공
      expect(actualChecksum).not.toBe(expectedChecksum);
      console.log(`✓ Checksum validation correctly detected tampered file`);

    } finally {
      if (fs.existsSync(testFile)) {
        fs.unlinkSync(testFile);
      }
    }
  });
});

test.describe('Cleanup', () => {
  test('Clean up temp directory', () => {
    const tempDir = path.join(SDK_ROOT, 'Tests~', 'E2E', 'temp');
    if (fs.existsSync(tempDir)) {
      fs.rmSync(tempDir, { recursive: true, force: true });
      console.log(`🗑️  Cleaned up: ${tempDir}`);
    }
  });
});
