#!/usr/bin/env node
/**
 * 버전 동기화 스크립트
 *
 * web-framework 버전을 SDK 전체에 동기화합니다.
 *
 * 사용법:
 *   node scripts/sync-version.js <version>
 *   node scripts/sync-version.js 1.6.0
 *   node scripts/sync-version.js --check  # 현재 버전만 확인
 */

const fs = require("fs");
const path = require("path");

const ROOT = path.resolve(__dirname, "..");

// 버전 업데이트 대상 파일들
const FILES_TO_UPDATE = [
  {
    path: "package.json",
    description: "UPM 패키지 버전",
    update: (content, version) => {
      const pkg = JSON.parse(content);
      pkg.version = version;
      return JSON.stringify(pkg, null, 2) + "\n";
    },
    getVersion: (content) => {
      const pkg = JSON.parse(content);
      return pkg.version;
    },
  },
  {
    path: "sdk-runtime-generator~/package.json",
    description: "web-framework 의존성 (생성기)",
    update: (content, version) => {
      const pkg = JSON.parse(content);
      pkg.dependencies["@apps-in-toss/web-framework"] = version;
      return JSON.stringify(pkg, null, 2) + "\n";
    },
    getVersion: (content) => {
      const pkg = JSON.parse(content);
      return pkg.dependencies["@apps-in-toss/web-framework"];
    },
  },
  {
    path: "WebGLTemplates/AITTemplate/BuildConfig/package.json",
    description: "web-framework 의존성 (빌드 템플릿)",
    update: (content, version) => {
      const pkg = JSON.parse(content);
      pkg.dependencies["@apps-in-toss/web-framework"] = version;
      return JSON.stringify(pkg, null, 2) + "\n";
    },
    getVersion: (content) => {
      const pkg = JSON.parse(content);
      return pkg.dependencies["@apps-in-toss/web-framework"];
    },
  },
];

function checkVersions() {
  console.log("현재 버전 정보:\n");

  for (const file of FILES_TO_UPDATE) {
    const filePath = path.join(ROOT, file.path);
    try {
      const content = fs.readFileSync(filePath, "utf8");
      const version = file.getVersion(content);
      console.log(`  ${file.description}`);
      console.log(`    파일: ${file.path}`);
      console.log(`    버전: ${version}\n`);
    } catch (error) {
      console.error(`  ${file.path}: 읽기 실패 - ${error.message}\n`);
    }
  }
}

function syncVersion(version) {
  console.log(`버전 동기화: ${version}\n`);

  let hasError = false;

  for (const file of FILES_TO_UPDATE) {
    const filePath = path.join(ROOT, file.path);
    try {
      const content = fs.readFileSync(filePath, "utf8");
      const oldVersion = file.getVersion(content);
      const updated = file.update(content, version);
      fs.writeFileSync(filePath, updated);
      console.log(`  ✓ ${file.path}`);
      console.log(`    ${oldVersion} → ${version}`);
    } catch (error) {
      console.error(`  ✗ ${file.path}: ${error.message}`);
      hasError = true;
    }
  }

  console.log("");

  if (hasError) {
    console.error("일부 파일 업데이트에 실패했습니다.");
    process.exit(1);
  }

  console.log("버전 동기화 완료!");
  console.log("");
  console.log("다음 단계:");
  console.log("  1. cd sdk-runtime-generator~ && pnpm install");
  console.log("  2. pnpm generate");
  console.log("  3. 변경사항 커밋");
}

function printUsage() {
  console.log("사용법:");
  console.log("  node scripts/sync-version.js <version>  버전 동기화");
  console.log("  node scripts/sync-version.js --check    현재 버전 확인");
  console.log("");
  console.log("예시:");
  console.log("  node scripts/sync-version.js 1.6.0");
}

// CLI 실행
const args = process.argv.slice(2);

if (args.length === 0) {
  printUsage();
  process.exit(1);
}

if (args[0] === "--check" || args[0] === "-c") {
  checkVersions();
} else if (args[0] === "--help" || args[0] === "-h") {
  printUsage();
} else {
  const version = args[0];

  // 간단한 semver 형식 검증
  if (!/^\d+\.\d+\.\d+(-[\w.]+)?$/.test(version)) {
    console.error(`오류: 올바른 버전 형식이 아닙니다: ${version}`);
    console.error("예시: 1.6.0, 2.0.0-beta.1");
    process.exit(1);
  }

  syncVersion(version);
}
