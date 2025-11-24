import { execSync } from 'child_process';
import { writeFileSync, readFileSync, mkdirSync, rmSync } from 'fs';
import { join } from 'path';
import { tmpdir } from 'os';

/**
 * C# 코드 포맷터 (CSharpier 사용)
 */
export class CSharpFormatter {
  /**
   * CSharpier를 사용하여 C# 코드를 포맷팅합니다
   */
  static format(code: string): string {
    // XML 주석 내 백틱 처리 (Markdown → C# XML 주석)
    code = this.fixXmlComments(code);
    // 임시 디렉토리 생성
    const tempDir = join(tmpdir(), `csharpier-${Date.now()}`);
    const tempFile = join(tempDir, 'temp.cs');

    try {
      // 임시 디렉토리 및 파일 생성
      mkdirSync(tempDir, { recursive: true });
      writeFileSync(tempFile, code, 'utf8');

      // CSharpier 실행 (로컬 도구로 설치된 것 사용)
      const cwd = join(process.cwd()); // generate-unity-sdk 디렉토리
      execSync(`dotnet csharpier format "${tempFile}"`, {
        cwd,
        stdio: 'pipe', // 출력 숨김
      });

      // 포맷팅된 코드 읽기
      const formatted = readFileSync(tempFile, 'utf8');

      return formatted;
    } catch (error) {
      console.warn('⚠️  CSharpier formatting failed, returning original code');
      if (error instanceof Error) {
        console.warn('   Error:', error.message);
      }
      return code;
    } finally {
      // 임시 파일 정리
      try {
        rmSync(tempDir, { recursive: true, force: true });
      } catch {
        // 정리 실패는 무시
      }
    }
  }

  /**
   * XML 주석 내 Markdown 문법을 C# XML 주석으로 변환
   */
  private static fixXmlComments(code: string): string {
    let result = code;

    // 모든 백틱을 제거 (C#에서 백틱은 제네릭 타입에만 사용 가능)
    result = result.replace(/`/g, '');

    // Markdown 목록 항목들을 XML 주석 내로 포함
    // 여러 줄에 걸친 목록을 하나의 <returns> 태그 안에 유지
    const lines = result.split('\n');
    const fixed: string[] = [];
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i];
      // "- item" 형태의 줄을 "/// - item"으로 변환 (XML 주석 계속)
      if (line.match(/^\s*-\s+/) && i > 0 && fixed[fixed.length - 1].includes('///')) {
        fixed.push(line.replace(/^(\s*)/, '$1/// '));
      } else {
        fixed.push(line);
      }
    }
    result = fixed.join('\n');

    // XML 주석 내 TypeScript 타입 표기 제거
    // /// <returns>: type} description → /// <returns>description
    result = result.replace(/(<returns>)\s*:\s*[^}]*}\s*/g, '$1 ');

    return result;
  }

  /**
   * 간단한 C# 유효성 검증
   */
  static validate(code: string): { valid: boolean; errors: string[] } {
    const errors: string[] = [];

    // 중괄호 매칭 검증
    const openBraces = (code.match(/\{/g) || []).length;
    const closeBraces = (code.match(/\}/g) || []).length;

    if (openBraces !== closeBraces) {
      errors.push(`중괄호 불일치: { = ${openBraces}, } = ${closeBraces}`);
    }

    return {
      valid: errors.length === 0,
      errors,
    };
  }
}
