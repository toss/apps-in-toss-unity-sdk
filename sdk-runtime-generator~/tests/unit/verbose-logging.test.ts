/**
 * Verbose Logging 스위치 게이팅 검증
 *
 * 생성된 SDK의 정보성 로그(jslib console.log, AITCore Debug.Log)가
 * AITCore.VerboseLogging / window.__AIT_VERBOSE 스위치 뒤에 게이팅되어
 * 기본값 OFF(조용)로 동작하는지 검증합니다.
 *
 * - console.error / console.warn, Debug.LogWarning / Debug.LogError는
 *   경고·에러 로그이므로 게이팅되지 않고 항상 출력되어야 합니다.
 * - AITCore.VerboseLogging 프로퍼티가 WebGL 빌드에서 jslib 브릿지
 *   (window.__AIT_VERBOSE)로 전파되는지도 함께 검증합니다.
 */

import { describe, test, expect, beforeAll } from 'vitest';
import path from 'path';
import { fileURLToPath } from 'url';
import * as fs from 'fs/promises';
import { glob } from 'glob';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

describe('Verbose Logging 게이팅 검증', () => {
  let coreCsContent: string;
  let jslibFiles: Map<string, string>;

  beforeAll(async () => {
    const sdkGeneratorRoot = path.resolve(__dirname, '../..');
    const runtimeSDKPath = path.resolve(sdkGeneratorRoot, '../Runtime/SDK');
    const pluginsPath = path.join(runtimeSDKPath, 'Plugins');

    try {
      await fs.access(runtimeSDKPath);
    } catch {
      throw new Error(
        '❌ 생성된 SDK 파일을 찾을 수 없습니다!\n' +
          '   먼저 "pnpm generate"를 실행하여 SDK를 생성하세요.\n' +
          `   Expected path: ${runtimeSDKPath}`
      );
    }

    coreCsContent = await fs.readFile(path.join(runtimeSDKPath, 'AITCore.cs'), 'utf-8');

    jslibFiles = new Map();
    const jslibFileNames = await glob('*.jslib', { cwd: pluginsPath, absolute: false });
    for (const fileName of jslibFileNames) {
      const content = await fs.readFile(path.join(pluginsPath, fileName), 'utf-8');
      jslibFiles.set(fileName, content);
    }
  });

  describe('AITCore.VerboseLogging 스위치', () => {
    test('public static bool VerboseLogging 프로퍼티가 존재해야 함', () => {
      expect(coreCsContent).toMatch(/public\s+static\s+bool\s+VerboseLogging/);
    });

    test('VerboseLogging 프로퍼티에 XML doc 주석이 있어야 함', () => {
      const propIndex = coreCsContent.indexOf('public static bool VerboseLogging');
      expect(propIndex).toBeGreaterThan(-1);

      // 프로퍼티 선언 직전 최대 40줄 이내에 /// <summary> 주석이 있어야 함
      const before = coreCsContent.slice(0, propIndex);
      const precedingLines = before.split('\n').slice(-40).join('\n');
      expect(precedingLines).toMatch(/\/\/\/\s*<summary>/);
    });

    test('기본값은 false (조용) 이어야 함', () => {
      expect(coreCsContent).toMatch(/_verboseLogging\s*=\s*false/);
    });

    test('setter가 WebGL 빌드에서 JS 브릿지로 전파해야 하며 다른 플랫폼/에디터에서는 컴파일만 되어야 함', () => {
      const propIndex = coreCsContent.indexOf('public static bool VerboseLogging');
      expect(propIndex).toBeGreaterThan(-1);

      // 프로퍼티 블록만 추출 (다음 200자 내에서 balanced하게 대략 추출)
      const propBlock = coreCsContent.slice(propIndex, propIndex + 600);

      // setter 내부에서 #if UNITY_WEBGL && !UNITY_EDITOR 가드 뒤에서만 JS 브릿지 호출
      expect(propBlock).toMatch(/#if UNITY_WEBGL && !UNITY_EDITOR[\s\S]*?__AITSetVerboseLogging\([\s\S]*?\)[\s\S]*?#endif/);
    });

    test('__AITSetVerboseLogging DllImport extern 선언이 #if UNITY_WEBGL && !UNITY_EDITOR 가드 안에 있어야 함', () => {
      const externIndex = coreCsContent.indexOf('private static extern void __AITSetVerboseLogging');
      expect(externIndex).toBeGreaterThan(-1);

      const before = coreCsContent.slice(0, externIndex);
      const lastIfIndex = before.lastIndexOf('#if UNITY_WEBGL && !UNITY_EDITOR');
      const lastEndifBeforeExtern = before.lastIndexOf('#endif');

      // 가장 최근의 #if가 가장 최근의 #endif보다 뒤에 있어야 (아직 닫히지 않은 가드 안에 있어야) 함
      expect(lastIfIndex).toBeGreaterThan(-1);
      expect(lastIfIndex).toBeGreaterThan(lastEndifBeforeExtern);
    });
  });

  describe('AITCore.cs 정보성 Debug.Log 게이팅', () => {
    // 게이팅되어야 하는 정보성 로그 (수신 payload / 라우팅 / 등록 / 응답)
    const INFO_LOG_MESSAGES = [
      'OnAITEventCallback received:',
      'Routing event callback:',
      'OnVisibilityStateChanged received:',
      'OnAITCallback received:',
      'Routing callback:',
      'Registered nested callback:',
      'Removed nested callback:',
      'OnNestedCallback received:',
      'RespondToNestedCallback:',
    ];

    test.each(INFO_LOG_MESSAGES)('"%s" Debug.Log는 VerboseLogging으로 게이팅되어야 함', (message) => {
      // 해당 메시지를 포함하는 라인을 찾아 if (VerboseLogging) 가드가 있는지 확인
      const lines = coreCsContent.split('\n');
      const matchingLines = lines.filter(l => l.includes('Debug.Log(') && l.includes(message));

      expect(matchingLines.length).toBeGreaterThan(0);
      for (const line of matchingLines) {
        expect(line).toMatch(/if\s*\(VerboseLogging\)\s*Debug\.Log\(/);
      }
    });

    test('Debug.LogWarning / Debug.LogError는 게이팅되지 않고 항상 출력되어야 함', () => {
      const lines = coreCsContent.split('\n');
      const warnOrErrorLines = lines.filter(
        l => l.includes('Debug.LogWarning(') || l.includes('Debug.LogError(')
      );

      expect(warnOrErrorLines.length).toBeGreaterThan(0);
      for (const line of warnOrErrorLines) {
        expect(line).not.toMatch(/if\s*\(VerboseLogging\)/);
      }
    });

    test('AITCore.cs에 게이팅되지 않은 정보성 Debug.Log가 남아있지 않아야 함', () => {
      const lines = coreCsContent.split('\n');
      const violations: string[] = [];

      for (const line of lines) {
        const trimmed = line.trim();
        if (!trimmed.startsWith('Debug.Log(')) continue; // Debug.LogWarning/LogError는 다른 접두사이므로 자연히 제외
        violations.push(trimmed);
      }

      if (violations.length > 0) {
        console.error('❌ 게이팅되지 않은 Debug.Log:');
        violations.forEach(v => console.error(`   - ${v}`));
      }

      expect(violations).toHaveLength(0);
    });
  });

  describe('jslib 정보성 console.log 게이팅', () => {
    test('모든 jslib 파일이 로딩되어야 함', () => {
      expect(jslibFiles.size).toBeGreaterThan(0);
    });

    test('AppsInToss-IAP.jslib에 정보성 console.log가 다수 존재해야 함 (회귀 감지용)', () => {
      const content = jslibFiles.get('AppsInToss-IAP.jslib');
      expect(content).toBeDefined();
      const count = (content!.match(/console\.log\(/g) || []).length;
      expect(count).toBeGreaterThan(0);
    });

    test('모든 jslib 파일의 console.log 호출은 window.__AIT_VERBOSE 가드 뒤에 있어야 함', () => {
      const violations: string[] = [];

      for (const [fileName, content] of jslibFiles) {
        const lines = content.split('\n');
        for (const line of lines) {
          if (!line.includes('console.log(')) continue;
          if (!line.includes('if (window.__AIT_VERBOSE)')) {
            violations.push(`${fileName}: ${line.trim()}`);
          }
        }
      }

      if (violations.length > 0) {
        console.error('❌ 게이팅되지 않은 console.log:');
        violations.forEach(v => console.error(`   - ${v}`));
      }

      expect(violations).toHaveLength(0);
    });

    test('console.error / console.warn 호출은 게이팅되지 않고 항상 출력되어야 함', () => {
      const violations: string[] = [];

      for (const [fileName, content] of jslibFiles) {
        const lines = content.split('\n');
        for (const line of lines) {
          if (!line.includes('console.error(') && !line.includes('console.warn(')) continue;
          if (line.includes('if (window.__AIT_VERBOSE)')) {
            violations.push(`${fileName}: ${line.trim()}`);
          }
        }
      }

      if (violations.length > 0) {
        console.error('❌ 잘못 게이팅된 console.error/warn:');
        violations.forEach(v => console.error(`   - ${v}`));
      }

      expect(violations).toHaveLength(0);
    });

    test('console.error / console.warn 호출이 최소 1개 이상 존재해야 함 (에러 로그 보존 회귀 감지용)', () => {
      let total = 0;
      for (const content of jslibFiles.values()) {
        total += (content.match(/console\.(error|warn)\(/g) || []).length;
      }
      expect(total).toBeGreaterThan(0);
    });
  });

  describe('AppsInToss-Core.jslib (verbose 스위치 브릿지)', () => {
    test('AppsInToss-Core.jslib 파일이 생성되어야 함', () => {
      expect(jslibFiles.has('AppsInToss-Core.jslib')).toBe(true);
    });

    test('__AITSetVerboseLogging 함수가 window.__AIT_VERBOSE를 설정해야 함', () => {
      const content = jslibFiles.get('AppsInToss-Core.jslib');
      expect(content).toBeDefined();
      expect(content).toMatch(/__AITSetVerboseLogging\s*:\s*function\s*\(verbose\)\s*\{/);
      expect(content).toMatch(/window\.__AIT_VERBOSE\s*=\s*verbose\s*!==\s*0/);
    });
  });
});
