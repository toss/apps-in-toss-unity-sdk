#!/usr/bin/env node
// ait-inject-runtime-version.mjs
//
// web-framework 3.x의 `ait deploy`는 .ait 메타데이터에 runtimeVersion(non-empty)을
// 요구하지만, 현재 `ait build`는 이 값을 채우지 않는다(upstream in-flight gap:
// deploy 게이트는 들어왔는데 build emit이 아직 따라오지 않음).
//
// 이 스크립트는 빌드된 .ait를 round-trip하여 runtimeVersion을 번들에 이미
// 들어있는 sdkVersion(= web-framework 버전)으로 채운다. 즉 runtimeVersion이
// web-framework 버전과 함께 자동으로 올라간다(하드코딩 없음).
//
// 안전 규칙 — 어떤 상황에서도 빌드/배포를 막지 않는다(항상 exit 0):
//   - .ait 없음(granite/비-ait 빌드) / 포맷 불일치 / sdkVersion 없음 → 경고 후 통과
//   - runtimeVersion이 이미 채워져 있으면(upstream이 build를 고치면) noop
// stable 경로(.ait 미생성)에도 무해하다.

import { readdir, readFile, writeFile } from "node:fs/promises";
import { resolve } from "node:path";

const TAG = "[ait-inject-runtime-version]";

async function main() {
  let AppsInTossBundle;
  try {
    ({ AppsInTossBundle } = await import("@apps-in-toss/ait-format"));
  } catch (e) {
    console.warn(`${TAG} @apps-in-toss/ait-format 로드 실패 — 주입을 건너뜁니다: ${e?.message ?? e}`);
    return;
  }

  const cwd = process.cwd();
  let aitName;
  try {
    aitName = (await readdir(cwd)).find((f) => f.endsWith(".ait"));
  } catch (e) {
    console.warn(`${TAG} 디렉터리 읽기 실패 — 건너뜁니다: ${e?.message ?? e}`);
    return;
  }
  if (!aitName) {
    console.warn(`${TAG} .ait 파일이 없습니다 — 건너뜁니다 (granite/비-ait 빌드일 수 있음).`);
    return;
  }
  const aitPath = resolve(cwd, aitName);

  let reader;
  try {
    const buf = await readFile(aitPath);
    reader = AppsInTossBundle.reader(new Uint8Array(buf));
  } catch (e) {
    console.warn(`${TAG} ${aitName} 파싱 실패 — 주입을 건너뜁니다: ${e?.message ?? e}`);
    return;
  }

  const bundle = reader.bundle;
  const meta = bundle?.metadata;

  const existing = meta?.runtimeVersion;
  if (typeof existing === "string" && existing.length > 0) {
    console.log(`${TAG} runtimeVersion이 이미 설정됨('${existing}') — 주입 불필요(noop).`);
    return;
  }

  const derived = meta?.sdkVersion;
  if (typeof derived !== "string" || derived.length === 0) {
    console.warn(`${TAG} sdkVersion이 비어 있어 runtimeVersion을 파생할 수 없습니다 — 건너뜁니다.`);
    return;
  }

  // round-trip: 모든 필드를 보존하고 runtimeVersion만 채운다.
  try {
    const writer = AppsInTossBundle.writer({
      appName: bundle.appName,
      deploymentId: bundle.deploymentId,
      formatVersion: bundle.formatVersion,
      createdBy: bundle.createdBy || undefined,
    });
    writer.setMetadata({
      isGame: meta?.isGame ?? false,
      platform: meta?.platform,
      runtimeVersion: derived,
      bundleFiles: meta?.bundleFiles ?? [],
      packageJson: meta?.packageJson,
      extra: meta?.extra,
      sdkVersion: meta?.sdkVersion,
    });
    for (const p of bundle.permissions ?? []) {
      writer.addPermission(p.name, p.access);
    }
    for (const name of reader.listEntries()) {
      writer.addFile(name, await reader.readEntry(name));
    }
    const sig = reader.readSignature();
    if (sig) writer.setSignature(sig);

    const out = await writer.toBuffer();
    await writeFile(aitPath, out);
    console.log(`${TAG} ✓ runtimeVersion='${derived}' 주입 완료 (${aitName}).`);
  } catch (e) {
    console.warn(`${TAG} 재작성 실패 — 원본 유지, 주입 건너뜀: ${e?.message ?? e}`);
  }
}

main().catch((e) => {
  // 최후의 안전망: 어떤 예외도 빌드/배포를 막지 않는다.
  console.warn(`${TAG} 예기치 못한 오류 — 주입 건너뜀: ${e?.message ?? e}`);
});
