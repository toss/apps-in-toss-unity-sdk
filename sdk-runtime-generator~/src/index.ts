#!/usr/bin/env node

import { Command } from 'commander';
import picocolors from 'picocolors';
import * as path from 'path';
import * as fs from 'fs/promises';
import { TypeScriptParser } from './parser.js';
import { validateAllTypes } from './validators/types.js';
import { validateCompleteness, printSummary } from './validators/completeness.js';
import { CSharpGenerator, CSharpTypeGenerator } from './generators/csharp.js';
import { JSLibGenerator } from './generators/jslib.js';
import { formatCommand } from './commands/format.js';

const program = new Command();

/**
 * TypeScript 정의 파일 경로 찾기
 */
async function findTypeDefinitions(webFrameworkPath: string): Promise<string> {
  // 일반적인 경로들 확인
  const possiblePaths = [
    // pnpm virtual store 경로 (우선순위 높음)
    path.join(process.cwd(), 'node_modules/.pnpm/@apps-in-toss+web-bridge@1.5.0_@apps-in-toss+bridge-core@1.5.0/node_modules/@apps-in-toss/web-bridge/built'),
    // 일반 node_modules 경로
    path.join(process.cwd(), 'node_modules/@apps-in-toss/web-bridge/built'),
    // web-framework 내부 경로
    path.join(webFrameworkPath, 'node_modules/@apps-in-toss/web-bridge/built'),
    path.join(webFrameworkPath, 'dist-web'),
    path.join(webFrameworkPath, 'built'),
    path.join(webFrameworkPath, 'dist'),
    path.join(webFrameworkPath, 'lib'),
  ];

  for (const p of possiblePaths) {
    try {
      const stat = await fs.stat(p);
      if (stat.isDirectory()) {
        // .d.ts 파일이 있는지 확인 (index.d.ts 제외)
        const files = await fs.readdir(p);
        const hasValidDts = files.some(f =>
          f.endsWith('.d.ts') &&
          f !== 'index.d.ts' &&
          f !== 'index.d.cts'
        );
        if (hasValidDts) {
          console.log(picocolors.green(`✅ TypeScript 정의 파일 발견: ${p}`));
          return p;
        }
      }
    } catch {
      // 경로가 없으면 다음 경로 시도
      continue;
    }
  }

  throw new Error('TypeScript 정의 파일을 찾을 수 없습니다.');
}

/**
 * node_modules에서 web-framework 찾기
 */
async function findWebFrameworkInNodeModules(): Promise<string> {
  const webFrameworkPath = path.join(process.cwd(), 'node_modules/@apps-in-toss/web-framework');

  try {
    await fs.access(webFrameworkPath);
    console.log(picocolors.green(`✅ web-framework 발견: ${webFrameworkPath}`));
    return webFrameworkPath;
  } catch {
    throw new Error(
      'web-framework를 찾을 수 없습니다.\n' +
      '다음 명령을 실행하세요: pnpm install'
    );
  }
}

/**
 * 메인 생성 로직
 */
async function generate(options: {
  tag: string;
  output: string;
  skipClone?: boolean;
  sourcePath?: string;
}) {
  const startTime = Date.now();

  try {
    console.log(picocolors.cyan(picocolors.bold('\n🚀 Unity SDK 자동 생성 시작\n')));
    console.log(picocolors.cyan(`📁 출력 경로: ${options.output}\n`));

    // 1. web-framework 경로 결정
    let webFrameworkPath: string;
    if (options.skipClone && options.sourcePath) {
      console.log(picocolors.yellow(`⚠️  로컬 경로 사용: ${options.sourcePath}`));
      webFrameworkPath = options.sourcePath;
    } else {
      // node_modules에서 web-framework 찾기
      webFrameworkPath = await findWebFrameworkInNodeModules();
    }

    // 2. TypeScript 정의 파일 찾기
    const typeDefinitionsPath = await findTypeDefinitions(webFrameworkPath);

    // 4. API 파싱
    console.log(picocolors.cyan('\n📊 web-framework 분석 중...'));
    const parser = new TypeScriptParser(typeDefinitionsPath);
    const apis = await parser.parseAPIs();

    if (apis.length === 0) {
      console.error(picocolors.red('\n❌ web-framework에서 API를 발견하지 못했습니다.\n'));
      console.error(picocolors.yellow('다음을 확인하세요:'));
      console.error(picocolors.yellow(`  1. TypeScript 정의 경로: ${typeDefinitionsPath}`));
      console.error(picocolors.yellow(`  2. web-framework 버전: ${webFrameworkPath}`));
      console.error(picocolors.yellow(`  3. .d.ts 파일에 export된 함수가 있는지 확인`));
      process.exit(1);
    }

    console.log(picocolors.green(`✓ ${apis.length}개 API 발견`));

    // 5. 타입 검증
    console.log(picocolors.cyan('\n🔍 타입 검증 중...'));
    const typeValidation = validateAllTypes(apis);
    if (!typeValidation.success) {
      console.error(picocolors.red('\n❌ 타입 검증 실패\n'));
      for (const error of typeValidation.errors) {
        console.error(error.message);
      }
      process.exit(1);
    }
    console.log(picocolors.green('✓ 타입 매핑 완료'));

    // 6. 타입 정의 파싱 (enum, interface)
    console.log(picocolors.cyan('\n📦 타입 정의 파싱 중...'));
    const typeDefinitions = await parser.parseTypeDefinitions();
    console.log(picocolors.green(`✓ ${typeDefinitions.length}개 타입 정의 발견`));

    // enum과 interface 분류
    const enums = typeDefinitions.filter(t => t.kind === 'enum');
    const interfaces = typeDefinitions.filter(t => t.kind === 'interface');
    if (enums.length > 0) {
      console.log(picocolors.gray(`   - Enum: ${enums.length}개 (${enums.map(e => e.name).join(', ')})`));
    }
    if (interfaces.length > 0) {
      console.log(picocolors.gray(`   - Interface: ${interfaces.length}개 (${interfaces.map(i => i.name).join(', ')})`));
    }

    // 7. 코드 생성
    console.log(picocolors.cyan('\n🔨 코드 생성 중...'));
    const csharpGenerator = new CSharpGenerator();
    const jslibGenerator = new JSLibGenerator();
    const typeGenerator = new CSharpTypeGenerator();

    // C# API 생성 (기존 방식 - 검증용)
    const generatedCodes = await csharpGenerator.generate(apis, options.tag);

    // 메인 AIT.cs 생성 (partial class 선언만)
    const mainFile = await csharpGenerator.generateMainFile(options.tag, apis.length);
    console.log(picocolors.green(`✓ AIT.cs (메인 partial class)`));

    // 카테고리별 API partial class 파일들 생성
    const categoryFiles = await csharpGenerator.generateCategoryFiles(apis);
    console.log(picocolors.green(`✓ ${categoryFiles.size}개 카테고리 파일 (AIT.{Category}.cs)`));

    // AITCore 생성 (인프라 코드) - enum 타입 목록 전달
    const enumTypeNames = new Set(enums.map(e => e.name));
    const coreFile = await csharpGenerator.generateCoreFile(apis, enumTypeNames);
    console.log(picocolors.green(`✓ AITCore.cs (Infrastructure)`));

    // C# 타입 정의 생성 (파싱된 enum/interface) - 본문만
    const parsedTypesBody = await typeGenerator.generateTypeDefinitions(typeDefinitions);

    // 파싱된 타입 이름 목록 생성 (중복 방지용)
    const parsedTypeNames = new Set(typeDefinitions.map(t => t.name));

    // C# 타입 정의 생성 (API에서 추출된 타입) - 본문만 (중복 제외)
    const apiTypesBody = await typeGenerator.generateTypes(apis, parsedTypeNames);

    // 헤더 + 본문들을 합침
    const typeFileHeader = `// -----------------------------------------------------------------------
// <copyright file="AIT.Types.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Type Definitions
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AppsInToss
{
`;
    const typeFileFooter = `}
`;

    const typesFile = typeFileHeader +
      (apiTypesBody ? apiTypesBody + '\n\n' : '') +
      (parsedTypesBody ? parsedTypesBody + '\n' : '') +
      typeFileFooter;
    console.log(picocolors.green(`✓ AIT.Types.cs (${typeDefinitions.length}개 타입 정의)`));

    // jslib 파일들 생성
    const jslibFiles = await jslibGenerator.generate(apis, options.tag);
    console.log(picocolors.green(`✓ ${jslibFiles.size}개 jslib 파일`));

    // 7. 완전성 검증
    console.log(picocolors.cyan('\n🔍 API 완전성 검증 중...'));
    const completenessValidation = validateCompleteness(apis, generatedCodes);
    if (!completenessValidation.success) {
      console.error(picocolors.red('\n❌ API 완전성 검증 실패\n'));
      for (const error of completenessValidation.errors) {
        console.error(error.message);
      }
      process.exit(1);
    }
    console.log(picocolors.green('✓ API 완전성 확인'));

    // 9. 파일 출력
    console.log(picocolors.cyan('\n📝 파일 쓰기 중...'));
    const outputDir = path.resolve(process.cwd(), options.output);

    // 기존 생성 파일 모두 삭제 (재현성 보장)
    console.log(picocolors.yellow('  🗑️  기존 생성 파일 삭제 중...'));
    try {
      // 기존 단일 파일 삭제
      await fs.rm(path.join(outputDir, 'AIT.cs'), { force: true });
      await fs.rm(path.join(outputDir, 'AITCore.cs'), { force: true });
      await fs.rm(path.join(outputDir, 'AIT.Types.cs'), { force: true });

      // 개별 partial class 파일들 삭제 (AIT.*.cs 패턴)
      const files = await fs.readdir(outputDir).catch(() => []);
      for (const file of files) {
        if (file.startsWith('AIT.') && file.endsWith('.cs') && file !== 'AIT.cs') {
          await fs.rm(path.join(outputDir, file), { force: true });
        }
      }

      await fs.rm(path.join(outputDir, 'Plugins'), { recursive: true, force: true });
      console.log(picocolors.green('  ✓ 기존 파일 삭제 완료'));
    } catch (error) {
      // 파일이 없으면 무시
    }

    await fs.mkdir(outputDir, { recursive: true });

    // 메인 AIT.cs 쓰기 (partial class 선언만)
    await fs.writeFile(path.join(outputDir, 'AIT.cs'), mainFile);
    console.log(picocolors.green(`  ✓ ${path.join(outputDir, 'AIT.cs')}`));

    // 카테고리별 API partial class 파일들 쓰기
    for (const [fileName, content] of categoryFiles.entries()) {
      await fs.writeFile(path.join(outputDir, fileName), content);
      console.log(picocolors.green(`  ✓ ${path.join(outputDir, fileName)}`));
    }

    // AITCore.cs 쓰기 (내부 인프라)
    await fs.writeFile(path.join(outputDir, 'AITCore.cs'), coreFile);
    console.log(picocolors.green(`  ✓ ${path.join(outputDir, 'AITCore.cs')}`));

    // AIT.Types.cs 쓰기 (타입 정의)
    await fs.writeFile(path.join(outputDir, 'AIT.Types.cs'), typesFile);
    console.log(picocolors.green(`  ✓ ${path.join(outputDir, 'AIT.Types.cs')}`));

    // jslib 파일들 쓰기
    const pluginsDir = path.join(outputDir, 'Plugins');
    await fs.mkdir(pluginsDir, { recursive: true });
    for (const [fileName, content] of jslibFiles.entries()) {
      await fs.writeFile(path.join(pluginsDir, fileName), content);
      console.log(picocolors.green(`  ✓ ${path.join(pluginsDir, fileName)}`));
    }

    // 9. 요약 출력
    printSummary(apis, generatedCodes);

    const elapsed = ((Date.now() - startTime) / 1000).toFixed(1);
    console.log(picocolors.green(picocolors.bold(`\n✅ 생성 완료! (${elapsed}s)\n`)));
  } catch (error) {
    console.error(
      picocolors.red(`\n❌ 생성 실패: ${error instanceof Error ? error.message : String(error)}\n`)
    );
    process.exit(1);
  }
}

// CLI 설정
program
  .name('generate-unity-sdk')
  .description('Unity SDK 자동 생성 도구')
  .version('1.0.0');

program
  .command('generate')
  .description('node_modules의 @apps-in-toss/web-framework에서 Unity SDK 생성')
  .option('-o, --output <path>', '출력 디렉토리', '../Runtime/SDK')
  .option('--source-path <path>', '(옵션) 로컬 web-framework 경로 (개발/테스트용)')
  .action((options) => {
    generate({
      tag: 'next', // 더 이상 사용하지 않음 (node_modules에서 가져옴)
      output: options.output,
      skipClone: !!options.sourcePath,
      sourcePath: options.sourcePath,
    });
  });

program
  .command('format')
  .description('생성된 C# 파일들을 CSharpier로 포맷팅')
  .action(formatCommand);

program.parse();
