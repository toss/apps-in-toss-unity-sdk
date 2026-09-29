/**
 * Format 명령어 - 생성된 C# 파일들을 CSharpier로 포맷팅
 */

import { execSync } from 'child_process';
import * as fs from 'fs/promises';
import * as path from 'path';
import { fileURLToPath } from 'url';
import pc from 'picocolors';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

interface FormatResult {
  success: number;
  unchanged: number;
  failed: number;
  errors: { file: string; error: string }[];
}

export async function formatCommand(): Promise<void> {
  console.log(pc.cyan('🎨 C# 파일 포맷팅 중...\n'));

  // SDK 경로 확인
  const sdkPath = path.resolve(__dirname, '../../../Runtime/SDK');

  try {
    await fs.access(sdkPath);
  } catch {
    console.error(pc.red('❌ 생성된 SDK 파일을 찾을 수 없습니다!'));
    console.error(pc.gray('   먼저 "pnpm generate"를 실행하여 SDK를 생성하세요.'));
    process.exit(1);
  }

  // dotnet CLI 확인
  try {
    execSync('dotnet --version', { stdio: 'pipe' });
  } catch {
    console.error(pc.red('❌ dotnet CLI를 찾을 수 없습니다.'));
    console.error(pc.gray('📦 설치 방법: https://dotnet.microsoft.com/download'));
    process.exit(1);
  }

  // CSharpier 설치 확인
  try {
    const toolList = execSync('dotnet tool list -g', { encoding: 'utf-8' });
    if (!toolList.includes('csharpier')) {
      console.log(pc.yellow('📦 CSharpier 설치 중...'));
      execSync('dotnet tool install -g csharpier', { stdio: 'inherit' });
      console.log('');
    }
  } catch (error) {
    console.error(pc.red('❌ CSharpier 설치 확인 실패'));
    process.exit(1);
  }

  // C# 파일 찾기
  const files = await fs.readdir(sdkPath);
  const csFiles = files
    .filter(f => f.endsWith('.cs'))
    .map(f => path.join(sdkPath, f));

  if (csFiles.length === 0) {
    console.error(pc.red('❌ 포맷팅할 .cs 파일이 없습니다.'));
    process.exit(1);
  }

  console.log(pc.gray(`📂 대상: ${csFiles.length}개 파일\n`));

  // 포맷팅 실행
  const result: FormatResult = {
    success: 0,
    unchanged: 0,
    failed: 0,
    errors: [],
  };

  for (const filePath of csFiles) {
    const fileName = path.basename(filePath);

    try {
      const output = execSync(`dotnet csharpier "${filePath}"`, {
        encoding: 'utf-8',
        stdio: 'pipe',
      });

      if (output.includes('Formatted')) {
        console.log(pc.green(`   ✓ ${fileName}`) + pc.gray(' (포맷팅 적용)'));
        result.success++;
      } else {
        console.log(pc.gray(`   ○ ${fileName} (변경 없음)`));
        result.unchanged++;
      }
    } catch (error: any) {
      console.log(pc.red(`   ✗ ${fileName}`) + pc.gray(' (실패)'));
      result.failed++;
      result.errors.push({
        file: fileName,
        error: error.stderr?.toString() || error.message,
      });
    }
  }

  console.log('');

  // 결과 출력
  const total = csFiles.length;
  const formatted = result.success + result.unchanged;

  if (result.failed === 0) {
    console.log(pc.green(`✅ 포맷팅 완료! (${formatted}/${total})`));
    if (result.success > 0) {
      console.log(pc.gray(`   ${result.success}개 파일 포맷팅, ${result.unchanged}개 파일 변경 없음`));
    }
    process.exit(0);
  } else {
    console.log(pc.yellow(`⚠️  포맷팅 완료 (성공: ${formatted}, 실패: ${result.failed})`));

    if (result.errors.length > 0) {
      console.log('');
      console.log(pc.red('실패한 파일:'));
      for (const { file, error } of result.errors.slice(0, 5)) {
        const errorMsg = error.split('\n')[0].trim();
        console.log(pc.gray(`   - ${file}: ${errorMsg}`));
      }
      if (result.errors.length > 5) {
        console.log(pc.gray(`   ... 외 ${result.errors.length - 5}개`));
      }
    }

    process.exit(1);
  }
}
