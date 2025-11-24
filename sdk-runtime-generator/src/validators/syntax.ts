import { ValidationError } from '../types.js';
import picocolors from 'picocolors';

/**
 * C# 코드 기본 문법 검증
 *
 * 완벽한 검증은 아니지만, 기본적인 문법 오류를 잡아냅니다.
 */
export function validateCSharpSyntax(code: string, apiName: string): ValidationError[] {
  const errors: ValidationError[] = [];

  // 중괄호 짝 검증
  const openBraces = (code.match(/{/g) || []).length;
  const closeBraces = (code.match(/}/g) || []).length;
  if (openBraces !== closeBraces) {
    errors.push({
      api: apiName,
      type: 'syntax-error',
      message: picocolors.red(`
❌ C# 문법 오류: 중괄호 짝이 맞지 않음

API: ${apiName}
열린 중괄호: ${openBraces}
닫힌 중괄호: ${closeBraces}

🛠️  조치 필요:
1. tools/generate-unity-sdk/src/templates/ 템플릿 수정
2. 또는 src/generators/csharp.ts 로직 수정

생성 중단됨.
      `),
      suggestion: '템플릿의 중괄호를 확인하세요.',
    });
  }

  // 소괄호 짝 검증
  const openParens = (code.match(/\(/g) || []).length;
  const closeParens = (code.match(/\)/g) || []).length;
  if (openParens !== closeParens) {
    errors.push({
      api: apiName,
      type: 'syntax-error',
      message: picocolors.red(`
❌ C# 문법 오류: 소괄호 짝이 맞지 않음

API: ${apiName}
열린 소괄호: ${openParens}
닫힌 소괄호: ${closeParens}

🛠️  조치 필요:
1. tools/generate-unity-sdk/src/templates/ 템플릿 수정
2. 또는 src/generators/csharp.ts 로직 수정

생성 중단됨.
      `),
      suggestion: '템플릿의 소괄호를 확인하세요.',
    });
  }

  // DllImport 선언 검증
  if (!code.includes('[DllImport("__Internal")]') && !code.includes('class ')) {
    // 클래스 정의가 아닌데 DllImport가 없으면 경고
    errors.push({
      api: apiName,
      type: 'syntax-error',
      message: picocolors.yellow(`
⚠️  C# 경고: DllImport 선언이 없습니다

API: ${apiName}

일반적으로 Unity WebGL 메서드는 DllImport("__Internal")을 사용합니다.
      `),
      suggestion: 'DllImport 선언을 추가하거나 클래스 정의를 확인하세요.',
    });
  }

  // 세미콜론 누락 검증 (간단한 휴리스틱)
  const lines = code.split('\n');
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i].trim();
    // 메서드 호출이나 선언으로 보이지만 세미콜론이 없는 경우
    if (
      line.length > 0 &&
      !line.endsWith(';') &&
      !line.endsWith('{') &&
      !line.endsWith('}') &&
      !line.startsWith('//') &&
      !line.startsWith('/*') &&
      !line.startsWith('*') &&
      !line.startsWith('[') &&
      !line.startsWith('#') &&
      !line.includes('=>') &&
      (line.includes('(') || line.includes('='))
    ) {
      errors.push({
        api: apiName,
        type: 'syntax-error',
        message: picocolors.yellow(`
⚠️  C# 경고: 세미콜론 누락 가능성

API: ${apiName}
Line ${i + 1}: ${line}
        `),
        suggestion: '해당 라인에 세미콜론이 필요한지 확인하세요.',
      });
      break; // 첫 번째 경고만 표시
    }
  }

  return errors;
}

/**
 * JavaScript 코드 기본 문법 검증
 */
export function validateJavaScriptSyntax(code: string, apiName: string): ValidationError[] {
  const errors: ValidationError[] = [];

  // 중괄호 짝 검증
  const openBraces = (code.match(/{/g) || []).length;
  const closeBraces = (code.match(/}/g) || []).length;
  if (openBraces !== closeBraces) {
    errors.push({
      api: apiName,
      type: 'syntax-error',
      message: picocolors.red(`
❌ JavaScript 문법 오류: 중괄호 짝이 맞지 않음

API: ${apiName}
열린 중괄호: ${openBraces}
닫힌 중괄호: ${closeBraces}

🛠️  조치 필요:
1. tools/generate-unity-sdk/src/templates/ 템플릿 수정
2. 또는 src/generators/jslib.ts 로직 수정

생성 중단됨.
      `),
      suggestion: '템플릿의 중괄호를 확인하세요.',
    });
  }

  // 소괄호 짝 검증
  const openParens = (code.match(/\(/g) || []).length;
  const closeParens = (code.match(/\)/g) || []).length;
  if (openParens !== closeParens) {
    errors.push({
      api: apiName,
      type: 'syntax-error',
      message: picocolors.red(`
❌ JavaScript 문법 오류: 소괄호 짝이 맞지 않음

API: ${apiName}
열린 소괄호: ${openParens}
닫힌 소괄호: ${closeParens}

🛠️  조치 필요:
1. tools/generate-unity-sdk/src/templates/ 템플릿 수정
2. 또는 src/generators/jslib.ts 로직 수정

생성 중단됨.
      `),
      suggestion: '템플릿의 소괄호를 확인하세요.',
    });
  }

  // jslib 파일은 특정 패턴을 따라야 함
  if (code.includes('mergeInto(LibraryManager.library')) {
    // mergeInto 패턴 사용 중
    if (!code.includes('autoAddDeps(')) {
      errors.push({
        api: apiName,
        type: 'syntax-error',
        message: picocolors.yellow(`
⚠️  JavaScript 경고: autoAddDeps가 없습니다

API: ${apiName}

Unity jslib 파일은 일반적으로 autoAddDeps를 사용합니다.
        `),
        suggestion: 'autoAddDeps 추가를 고려하세요.',
      });
    }
  }

  return errors;
}

/**
 * 생성된 모든 코드에 대한 문법 검증
 */
export function validateAllSyntax(
  csharpCode: string,
  jslibCodes: Map<string, string>
): { success: boolean; errors: ValidationError[] } {
  const allErrors: ValidationError[] = [];

  // C# 코드 검증
  const csharpErrors = validateCSharpSyntax(csharpCode, 'AIT.cs');
  allErrors.push(...csharpErrors);

  // jslib 파일들 검증
  for (const [fileName, code] of jslibCodes.entries()) {
    const jslibErrors = validateJavaScriptSyntax(code, fileName);
    allErrors.push(...jslibErrors);
  }

  return {
    success: allErrors.length === 0,
    errors: allErrors,
  };
}

/**
 * 검증 결과 출력
 */
export function printValidationResults(errors: ValidationError[]): void {
  if (errors.length === 0) {
    console.log(picocolors.green('✅ 문법 검증 통과'));
    return;
  }

  console.log(picocolors.yellow(`\n⚠️  ${errors.length}개의 문법 경고/오류 발견:\n`));

  for (const error of errors) {
    console.log(error.message);
    if (error.suggestion) {
      console.log(picocolors.cyan(`💡 제안: ${error.suggestion}\n`));
    }
  }
}
