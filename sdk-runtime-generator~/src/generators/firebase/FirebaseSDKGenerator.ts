/**
 * Firebase SDK 생성 오케스트레이터
 *
 * Firebase API 파싱 → C# 코드 생성 → jslib 생성 → firebase-bridge.ts 생성
 * 모든 파일을 Runtime/Firebase/ 디렉토리에 출력합니다.
 */

import * as path from 'path';
import * as fs from 'fs/promises';
import * as crypto from 'crypto';
import picocolors from 'picocolors';
import { parseFirebaseAPIs } from '../../parser/firebase-parser.js';
import { generateFirebaseCSharp } from './FirebaseCSharpGenerator.js';
import { generateFirebaseJSLib } from './FirebaseJSLibGenerator.js';
import { generateFirebaseTypes } from './FirebaseTypeGenerator.js';
import { generateFirebaseBridge } from './FirebaseBridgeGenerator.js';
import { generateFirebaseCallbackRouter } from './FirebaseCallbackRouterGenerator.js';

/**
 * Unity GUID 생성
 */
function generateUnityGUID(): string {
  return crypto.randomBytes(16).toString('hex');
}

/**
 * C# .meta 파일 생성
 */
function generateCSharpMeta(guid: string): string {
  return `fileFormatVersion: 2
guid: ${guid}
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
`;
}

/**
 * jslib .meta 파일 생성
 */
function generateJslibMeta(guid: string): string {
  return `fileFormatVersion: 2
guid: ${guid}
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 1
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any:
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true
  - first:
      WebGL: WebGL
    second:
      enabled: 1
      settings: {}
  userData:
  assetBundleName:
  assetBundleVariant:
`;
}

/**
 * Assembly definition .meta 파일 생성
 */
function generateAsmdefMeta(guid: string): string {
  return `fileFormatVersion: 2
guid: ${guid}
AssemblyDefinitionImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
`;
}

/**
 * 폴더 .meta 파일 생성
 */
function generateFolderMeta(guid: string): string {
  return `fileFormatVersion: 2
guid: ${guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
`;
}

/**
 * .meta 파일 관리 (기존 파일이 있으면 보존, 없으면 생성)
 */
async function ensureMetaFile(
  filePath: string,
  existingMetas: Map<string, string>,
  type: 'cs' | 'jslib' | 'asmdef' | 'folder'
): Promise<void> {
  const metaPath = filePath + '.meta';

  if (existingMetas.has(filePath)) {
    await fs.writeFile(metaPath, existingMetas.get(filePath)!);
  } else {
    const guid = generateUnityGUID();
    let content: string;
    switch (type) {
      case 'cs': content = generateCSharpMeta(guid); break;
      case 'jslib': content = generateJslibMeta(guid); break;
      case 'asmdef': content = generateAsmdefMeta(guid); break;
      case 'folder': content = generateFolderMeta(guid); break;
    }
    await fs.writeFile(metaPath, content);
    console.log(picocolors.blue(`  📄 새 .meta 파일 생성: ${path.basename(metaPath)}`));
  }
}

/**
 * 기존 .meta 파일 수집
 */
async function collectMetaFiles(dir: string): Promise<Map<string, string>> {
  const metaFiles = new Map<string, string>();
  const absoluteDir = path.resolve(dir);

  try {
    const entries = await fs.readdir(absoluteDir, { withFileTypes: true });
    for (const entry of entries) {
      const fullPath = path.join(absoluteDir, entry.name);
      if (entry.isDirectory()) {
        const subMetas = await collectMetaFiles(fullPath);
        for (const [key, value] of subMetas) {
          metaFiles.set(key, value);
        }
      } else if (entry.name.endsWith('.meta')) {
        const content = await fs.readFile(fullPath, 'utf-8');
        metaFiles.set(fullPath.slice(0, -5), content);
      }
    }
  } catch {
    // 디렉토리가 없으면 빈 맵 반환
  }

  return metaFiles;
}

/**
 * Firebase SDK 전체 생성
 */
export async function generateFirebaseSDK(
  outputDir: string,
  existingMetas: Map<string, string>
): Promise<void> {
  console.log(picocolors.cyan('🔥 Firebase SDK 생성 시작...'));

  // 1. Firebase API 파싱
  const { apis, types } = parseFirebaseAPIs();
  console.log(picocolors.green(`  ✓ ${apis.length}개 Firebase API 파싱 완료`));

  // 2. 기존 .meta 파일 수집
  const firebaseMetas = await collectMetaFiles(outputDir);
  // 기존 existingMetas와 병합
  for (const [key, value] of firebaseMetas) {
    existingMetas.set(key, value);
  }

  // 3. 출력 디렉토리 생성
  await fs.mkdir(outputDir, { recursive: true });
  const pluginsDir = path.join(outputDir, 'Plugins');
  await fs.mkdir(pluginsDir, { recursive: true });

  // 폴더 .meta 파일
  await ensureMetaFile(outputDir, existingMetas, 'folder');
  await ensureMetaFile(pluginsDir, existingMetas, 'folder');

  // 4. Assembly definition 생성
  const asmdefContent = JSON.stringify({
    name: 'AppsInToss.Firebase',
    rootNamespace: 'AppsInToss.Firebase',
    references: ['AppsInTossSDK'],
    includePlatforms: [],
    excludePlatforms: [],
    allowUnsafeCode: false,
    overrideReferences: false,
    precompiledReferences: [],
    autoReferenced: true,
    defineConstraints: [],
    versionDefines: [],
    noEngineReferences: false,
  }, null, 2);
  const asmdefPath = path.join(outputDir, 'AppsInToss.Firebase.asmdef');
  await fs.writeFile(asmdefPath, asmdefContent);
  await ensureMetaFile(asmdefPath, existingMetas, 'asmdef');
  console.log(picocolors.green('  ✓ AppsInToss.Firebase.asmdef'));

  // 5. 메인 AITFirebase.cs 생성
  const mainCsContent = generateFirebaseMainClass();
  const mainCsPath = path.join(outputDir, 'AITFirebase.cs');
  await fs.writeFile(mainCsPath, mainCsContent);
  await ensureMetaFile(mainCsPath, existingMetas, 'cs');
  console.log(picocolors.green('  ✓ AITFirebase.cs'));

  // 6. 카테고리별 C# partial class 파일 생성
  const categoryFiles = generateFirebaseCSharp(apis);
  for (const [fileName, content] of categoryFiles.entries()) {
    const filePath = path.join(outputDir, fileName);
    await fs.writeFile(filePath, content);
    await ensureMetaFile(filePath, existingMetas, 'cs');
    console.log(picocolors.green(`  ✓ ${fileName}`));
  }

  // 7. 타입 정의 생성
  const typesContent = generateFirebaseTypes(types);
  const typesPath = path.join(outputDir, 'AITFirebase.Types.cs');
  await fs.writeFile(typesPath, typesContent);
  await ensureMetaFile(typesPath, existingMetas, 'cs');
  console.log(picocolors.green('  ✓ AITFirebase.Types.cs'));

  // 8. 콜백 라우터 생성
  const routerContent = generateFirebaseCallbackRouter(apis, types);
  const routerPath = path.join(outputDir, 'FirebaseCallbackRouter.cs');
  await fs.writeFile(routerPath, routerContent);
  await ensureMetaFile(routerPath, existingMetas, 'cs');
  console.log(picocolors.green('  ✓ FirebaseCallbackRouter.cs'));

  // 9. jslib 파일 생성
  const jslibFiles = generateFirebaseJSLib(apis);
  for (const [fileName, content] of jslibFiles.entries()) {
    const filePath = path.join(pluginsDir, fileName);
    await fs.writeFile(filePath, content);
    await ensureMetaFile(filePath, existingMetas, 'jslib');
    console.log(picocolors.green(`  ✓ Plugins/${fileName}`));
  }

  // 10. firebase-bridge.ts 생성
  const bridgeContent = generateFirebaseBridge(apis);
  const bridgePath = path.resolve(outputDir, '../../WebGLTemplates/AITTemplate/BuildConfig~/firebase-bridge.ts');
  await fs.writeFile(bridgePath, bridgeContent);
  console.log(picocolors.green('  ✓ firebase-bridge.ts'));

  console.log(picocolors.green(picocolors.bold(`\n🔥 Firebase SDK 생성 완료 (${apis.length}개 API, ${types.length}개 타입)\n`)));
}

/**
 * 메인 AITFirebase.cs partial class 선언 생성
 */
function generateFirebaseMainClass(): string {
  return `// -----------------------------------------------------------------------
// <copyright file="AITFirebase.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Firebase Integration
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace AppsInToss.Firebase
{
    /// <summary>
    /// Firebase SDK for Apps in Toss Unity
    /// </summary>
    /// <remarks>
    /// This file is auto-generated. Do not modify directly.
    /// 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
    /// </remarks>
    public static partial class AITFirebase
    {
        // 개별 API 메서드들은 AITFirebase.{Category}.cs 파일들에 정의되어 있습니다.
    }
}
`;
}
