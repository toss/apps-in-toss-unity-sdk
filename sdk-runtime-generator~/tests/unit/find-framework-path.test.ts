/**
 * findFrameworkPath가 pnpm virtual store 디렉터리 이름에 의존하지 않는지 검증
 *
 * Windows의 pnpm은 virtualStoreDirMaxLength(기본 60)를 넘는 디렉터리 이름을
 * "@apps-in-toss+framework@1.6_<hash>"처럼 자른다. 이름 접두사로 버전을 찾던 예전 구현은
 * 이 경우 framework .d.ts를 못 찾아 loadFullScreenAd/showFullScreenAd를 조용히 빼먹었다.
 */

import { afterEach, beforeEach, describe, expect, test } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { findFrameworkPath } from '../../src/parser/framework-parser.js';

let root: string;

// .pnpm/<storeDirName>/node_modules/@apps-in-toss/framework 에 패키지를 두고
// node_modules/@apps-in-toss/framework 링크로 연결한다 (pnpm isolated 레이아웃).
function layout(declared: string, installed: string, storeDirName: string): string {
  fs.writeFileSync(
    path.join(root, 'package.json'),
    JSON.stringify({ dependencies: { '@apps-in-toss/framework': declared } }),
  );
  const pkgDir = path.join(root, 'node_modules', '.pnpm', storeDirName, 'node_modules', '@apps-in-toss', 'framework');
  fs.mkdirSync(path.join(pkgDir, 'dist'), { recursive: true });
  fs.writeFileSync(path.join(pkgDir, 'package.json'), JSON.stringify({ version: installed }));
  fs.writeFileSync(path.join(pkgDir, 'dist', 'index.d.cts'), 'export declare function loadFullScreenAd(): void;\n');
  fs.mkdirSync(path.join(root, 'node_modules', '@apps-in-toss'), { recursive: true });
  fs.symlinkSync(pkgDir, path.join(root, 'node_modules', '@apps-in-toss', 'framework'), 'junction');
  return fs.realpathSync(path.join(pkgDir, 'dist', 'index.d.cts'));
}

beforeEach(() => {
  root = fs.mkdtempSync(path.join(os.tmpdir(), 'find-framework-path-'));
});

afterEach(() => {
  fs.rmSync(root, { recursive: true, force: true });
});

describe('findFrameworkPath', () => {
  test('store 디렉터리 이름이 잘려 버전 접두사가 없어도 node_modules 링크로 찾는다', () => {
    const expected = layout('1.6.0', '1.6.0', '@apps-in-toss+framework@1.6_b3c368ab78fa966019aeb37102760355');
    expect(findFrameworkPath(undefined, root)).toBe(expected);
  });

  test('링크된 패키지 버전이 package.json 선언과 다르면 null', () => {
    layout('1.6.0', '1.6.1', '@apps-in-toss+framework@1.6.1');
    expect(findFrameworkPath(undefined, root)).toBeNull();
  });
});
