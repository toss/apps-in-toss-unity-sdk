/**
 * Golden Files 업데이트 스크립트
 *
 * 현재 생성된 SDK 파일을 golden file로 저장합니다.
 * SDK 생성기의 의도적 변경 후 실행하여 새로운 기준을 설정합니다.
 *
 * 사용법: pnpm test:update-golden
 */

import * as fs from 'fs/promises';
import * as path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// 경로 설정
const GOLDEN_DIR = path.resolve(__dirname, '../fixtures/golden');
const SDK_GENERATED_DIR = path.resolve(__dirname, '../../../..', 'Runtime/SDK');

// 복사할 파일 목록
const FILES_TO_COPY = ['AIT.Types.cs', 'AITCore.cs'];

async function main() {
  console.log('\n📝 Golden Files 업데이트 시작\n');
  console.log(`   소스: ${SDK_GENERATED_DIR}`);
  console.log(`   대상: ${GOLDEN_DIR}\n`);

  // Golden 디렉토리 생성
  await fs.mkdir(GOLDEN_DIR, { recursive: true });

  let successCount = 0;
  let failCount = 0;

  for (const fileName of FILES_TO_COPY) {
    const sourcePath = path.join(SDK_GENERATED_DIR, fileName);
    const goldenPath = path.join(GOLDEN_DIR, `${fileName}.golden`);

    try {
      const content = await fs.readFile(sourcePath, 'utf-8');
      await fs.writeFile(goldenPath, content);
      console.log(`   ✅ ${fileName} → ${fileName}.golden`);
      successCount++;
    } catch (error) {
      console.log(`   ❌ ${fileName}: 파일을 읽을 수 없음`);
      failCount++;
    }
  }

  console.log('\n' + '='.repeat(50));
  console.log(`   성공: ${successCount}개`);
  console.log(`   실패: ${failCount}개`);
  console.log('='.repeat(50) + '\n');

  if (failCount > 0) {
    console.log('⚠️  일부 파일이 없습니다. "pnpm generate"를 먼저 실행하세요.\n');
    process.exit(1);
  }

  console.log('✅ Golden files 업데이트 완료\n');
  console.log('💡 이제 "pnpm test:differential"로 회귀 테스트를 실행할 수 있습니다.\n');
}

main().catch(console.error);
