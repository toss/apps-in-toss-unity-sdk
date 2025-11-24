#!/usr/bin/env node

import { Command } from 'commander';
import picocolors from 'picocolors';
import * as path from 'path';
import * as fs from 'fs/promises';
import { execSync } from 'child_process';
import { TypeScriptParser } from './parser.js';
import { validateAllTypes } from './validators/types.js';
import { validateCompleteness, printSummary } from './validators/completeness.js';
import { validateAllSyntax, printValidationResults } from './validators/syntax.js';
import { CSharpGenerator, CSharpTypeGenerator } from './generators/csharp.js';
import { JSLibGenerator } from './generators/jslib.js';

const program = new Command();

/**
 * GitHub에서 web-framework 레포지토리 clone
 */
async function cloneWebFramework(tag: string, tempDir: string): Promise<string> {
  console.log(picocolors.cyan(`\n📦 web-framework clone 중... (tag: ${tag})`));

  const repoUrl = 'https://github.com/toss/frontend-bedrock.git';
  const clonePath = path.join(tempDir, 'frontend-bedrock');

  try {
    // 기존 디렉토리 삭제
    await fs.rm(clonePath, { recursive: true, force: true });

    // Sparse checkout으로 필요한 부분만 clone
    execSync(
      `git clone --depth 1 --branch ${tag} --filter=blob:none --sparse ${repoUrl} ${clonePath}`,
      { stdio: 'inherit' }
    );

    // web-framework 디렉토리만 checkout
    execSync('git sparse-checkout set apps-in-toss-packages/web-framework', {
      cwd: clonePath,
      stdio: 'inherit',
    });

    const webFrameworkPath = path.join(
      clonePath,
      'apps-in-toss-packages',
      'web-framework'
    );

    console.log(picocolors.green(`✅ Clone 완료: ${webFrameworkPath}`));
    return webFrameworkPath;
  } catch (error) {
    throw new Error(
      `GitHub clone 실패: ${error instanceof Error ? error.message : String(error)}`
    );
  }
}

/**
 * npm 패키지 빌드
 */
async function buildWebFramework(webFrameworkPath: string): Promise<void> {
  console.log(picocolors.cyan('\n🔨 web-framework 빌드 중...'));

  try {
    // package.json 확인
    const packageJsonPath = path.join(webFrameworkPath, 'package.json');
    const packageJson = JSON.parse(await fs.readFile(packageJsonPath, 'utf-8'));

    // 빌드 스크립트가 있는지 확인
    if (!packageJson.scripts?.build) {
      console.log(picocolors.yellow('⚠️  빌드 스크립트 없음, 스킵'));
      return;
    }

    // npm install
    console.log(picocolors.cyan('  npm install...'));
    execSync('npm install', {
      cwd: webFrameworkPath,
      stdio: 'inherit',
    });

    // npm run build
    console.log(picocolors.cyan('  npm run build...'));
    execSync('npm run build', {
      cwd: webFrameworkPath,
      stdio: 'inherit',
    });

    console.log(picocolors.green('✅ 빌드 완료'));
  } catch (error) {
    throw new Error(
      `web-framework 빌드 실패: ${error instanceof Error ? error.message : String(error)}`
    );
  }
}

/**
 * TypeScript 정의 파일 경로 찾기
 */
async function findTypeDefinitions(webFrameworkPath: string): Promise<string> {
  // 일반적인 경로들 확인
  const possiblePaths = [
    path.join(webFrameworkPath, 'dist-web'),
    path.join(webFrameworkPath, 'node_modules/@apps-in-toss/web-bridge/built'),
    path.join(webFrameworkPath, 'built'),
    path.join(webFrameworkPath, 'dist'),
    path.join(webFrameworkPath, 'lib'),
  ];

  for (const p of possiblePaths) {
    try {
      const stat = await fs.stat(p);
      if (stat.isDirectory()) {
        // .d.ts 파일이 있는지 확인
        const files = await fs.readdir(p);
        if (files.some(f => f.endsWith('.d.ts'))) {
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
    console.log(picocolors.cyan(`📌 web-framework tag: ${options.tag}`));
    console.log(picocolors.cyan(`📁 출력 경로: ${options.output}\n`));

    // 1. GitHub에서 clone 또는 로컬 경로 사용
    let webFrameworkPath: string;
    if (options.skipClone && options.sourcePath) {
      console.log(picocolors.yellow(`⚠️  Clone 스킵, 로컬 경로 사용: ${options.sourcePath}`));
      webFrameworkPath = options.sourcePath;
    } else {
      const tempDir = path.join(process.cwd(), '.tmp');
      await fs.mkdir(tempDir, { recursive: true });
      webFrameworkPath = await cloneWebFramework(options.tag, tempDir);
    }

    // 2. 빌드 (필요시)
    if (!options.skipClone) {
      await buildWebFramework(webFrameworkPath);
    }

    // 3. TypeScript 정의 파일 찾기
    const typeDefinitionsPath = await findTypeDefinitions(webFrameworkPath);

    // 4. API 파싱
    console.log(picocolors.cyan('\n📊 web-framework 분석 중...'));
    const parser = new TypeScriptParser(typeDefinitionsPath);
    const apis = await parser.parseAPIs();
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

    // C# API 생성
    const generatedCodes = await csharpGenerator.generate(apis, options.tag);
    const csharpClassFile = await csharpGenerator.generateClassFile(apis, options.tag);
    console.log(picocolors.green(`✓ AIT.cs (${apis.length} APIs)`));

    // AITCore 생성 (인프라 코드)
    const coreFile = await csharpGenerator.generateCoreFile(apis);
    console.log(picocolors.green(`✓ AITCore.cs (Infrastructure)`));

    // C# 타입 정의 생성 (API에서 추출된 타입) - 본문만
    const apiTypesBody = await typeGenerator.generateTypes(apis);

    // C# 타입 정의 생성 (파싱된 enum/interface) - 본문만
    const parsedTypesBody = await typeGenerator.generateTypeDefinitions(typeDefinitions);

    // 헤더 + 본문들을 합침
    const typeFileHeader = `// -----------------------------------------------------------------------
// <copyright file="AIT.Types.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Type Definitions
// </copyright>
// -----------------------------------------------------------------------

using System;

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

    // 8. 문법 검증
    console.log(picocolors.cyan('\n🧪 문법 검증 중...'));
    const syntaxValidation = validateAllSyntax(csharpClassFile, jslibFiles);
    if (!syntaxValidation.success) {
      console.error(picocolors.yellow('\n⚠️  문법 경고 발견\n'));
      printValidationResults(syntaxValidation.errors);
      // 경고는 계속 진행
    } else {
      console.log(picocolors.green('✓ C# 문법 검증'));
      console.log(picocolors.green('✓ jslib 문법 검증'));
    }

    // 9. 파일 출력
    console.log(picocolors.cyan('\n📝 파일 쓰기 중...'));
    const outputDir = path.resolve(process.cwd(), options.output);

    // 기존 생성 파일 모두 삭제 (재현성 보장)
    console.log(picocolors.yellow('  🗑️  기존 생성 파일 삭제 중...'));
    try {
      await fs.rm(path.join(outputDir, 'AIT.cs'), { force: true });
      await fs.rm(path.join(outputDir, 'AITCore.cs'), { force: true });
      await fs.rm(path.join(outputDir, 'AIT.Types.cs'), { force: true });
      await fs.rm(path.join(outputDir, 'Plugins'), { recursive: true, force: true });
      console.log(picocolors.green('  ✓ 기존 파일 삭제 완료'));
    } catch (error) {
      // 파일이 없으면 무시
    }

    await fs.mkdir(outputDir, { recursive: true });

    // AIT.cs 쓰기 (주요 API)
    await fs.writeFile(path.join(outputDir, 'AIT.cs'), csharpClassFile);
    console.log(picocolors.green(`  ✓ ${path.join(outputDir, 'AIT.cs')}`));

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
  .description('web-framework에서 Unity SDK 생성')
  .option('-t, --tag <tag>', 'web-framework Git 태그', 'next')
  .option('-o, --output <path>', '출력 디렉토리', '../../Runtime/SDK')
  .option('--skip-clone', '로컬 경로 사용 (개발용)', false)
  .option('--source-path <path>', '로컬 web-framework 경로 (--skip-clone과 함께 사용)')
  .action(generate);

program.parse();
