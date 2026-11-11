// Golden 파일(tests/fixtures/golden)을 현재 Runtime/SDK 생성 결과로 갱신한다.
//
// golden은 생성기 변경이 의도치 않게 출력을 바꾸지 않았는지 보는 회귀 기준이다. web-framework 버전을
// 올리면 입력이 바뀌어 출력도 정당하게 바뀌므로, SDK 업데이트 워크플로가 `pnpm generate` 뒤에 이
// 스크립트로 golden을 함께 갱신한다. 갱신된 diff는 업데이트 PR에서 리뷰한다.
// 대상은 differential.test.ts와 같다: AIT.Types.{Category}.cs 전체와 AITCore.cs.
//
// 사용: pnpm generate && pnpm test:update-golden
import { copyFileSync, existsSync, mkdirSync, readdirSync, rmSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const goldenDir = path.resolve(here, '../tests/fixtures/golden');
const sdkDir = path.resolve(here, '../../Runtime/SDK');
const TYPE_FILE_RE = /^AIT\.Types\..+\.cs$/;

if (!existsSync(sdkDir)) {
  console.error(`Runtime/SDK 없음: ${sdkDir}`);
  process.exit(1);
}
mkdirSync(goldenDir, { recursive: true });

const targets = readdirSync(sdkDir).filter(f => TYPE_FILE_RE.test(f));
targets.push('AITCore.cs');
for (const f of targets) {
  copyFileSync(path.join(sdkDir, f), path.join(goldenDir, `${f}.golden`));
  console.log(`golden 갱신: ${f}`);
}

// 생성 결과에서 사라진 타입 파일의 golden은 지운다 (다른 테스트가 쓰는 golden은 건드리지 않는다)
const keep = new Set(targets.map(f => `${f}.golden`));
for (const g of readdirSync(goldenDir)) {
  if (g.endsWith('.golden') && TYPE_FILE_RE.test(g.slice(0, -'.golden'.length)) && !keep.has(g)) {
    rmSync(path.join(goldenDir, g));
    console.log(`golden 삭제: ${g}`);
  }
}
