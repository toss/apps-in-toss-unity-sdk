/**
 * ts-morph를 사용한 실제 JavaScript 문법 검증
 */

import { Project, ScriptKind, DiagnosticCategory, ScriptTarget, ModuleKind, SyntaxKind } from 'ts-morph';
import path from 'path';
import os from 'os';
import fs from 'fs/promises';

export interface JavaScriptValidationResult {
  valid: boolean;
  errors: SyntaxError[];
  warnings: SyntaxWarning[];
}

export interface SyntaxError {
  line: number;
  column: number;
  code: number;
  message: string;
}

export interface SyntaxWarning {
  line: number;
  column: number;
  code: number;
  message: string;
}

/**
 * ts-morph를 사용한 실제 JavaScript 문법 검증
 */
export function validateJavaScriptSyntax(code: string, filename: string = 'source.js'): JavaScriptValidationResult {
  // 임시 프로젝트 생성
  const project = new Project({
    useInMemoryFileSystem: true,
    compilerOptions: {
      target: ScriptTarget.ES2015,
      module: ModuleKind.None,
      allowJs: true,
      checkJs: false, // 타입 체크는 하지 않고 문법만 검증
      noEmit: true,
      noImplicitAny: false,
    },
  });

  // 소스 파일 추가
  // ts-morph는 .jslib 확장자를 인식하지 못하므로 .js로 변경
  const normalizedFilename = filename.endsWith('.jslib')
    ? filename.replace(/\.jslib$/, '.js')
    : filename;

  const sourceFile = project.createSourceFile(normalizedFilename, code, {
    scriptKind: ScriptKind.JS,
  });

  const errors: SyntaxError[] = [];
  const warnings: SyntaxWarning[] = [];

  // Pre-emit diagnostics (문법 검증)
  const diagnostics = sourceFile.getPreEmitDiagnostics();

  for (const diagnostic of diagnostics) {
    const start = diagnostic.getStart();
    const { line, column } = start
      ? sourceFile.getLineAndColumnAtPos(start)
      : { line: 0, column: 0 };

    // getMessageText()가 string | DiagnosticMessageChain을 반환할 수 있음
    const messageText = diagnostic.getMessageText();
    const message = typeof messageText === 'string'
      ? messageText
      : JSON.stringify(messageText);

    const item = {
      line,
      column,
      code: diagnostic.getCode(),
      message,
    };

    if (diagnostic.getCategory() === DiagnosticCategory.Error) {
      errors.push(item);
    } else if (diagnostic.getCategory() === DiagnosticCategory.Warning) {
      warnings.push(item);
    }
  }

  return {
    valid: errors.length === 0,
    errors,
    warnings,
  };
}

/**
 * mergeInto 패턴의 JavaScript 코드 검증
 *
 * Unity jslib 파일은 다음 패턴을 사용:
 * mergeInto(LibraryManager.library, { ... });
 */
export function validateMergeIntoSyntax(code: string): JavaScriptValidationResult {
  // mergeInto 함수 호출을 래핑하여 검증
  const wrappedCode = `
    var LibraryManager = { library: {} };
    function mergeInto(target, source) { Object.assign(target, source); }
    var Module = {};
    var UTF8ToString = function(ptr) { return ""; };
    var SendMessage = function(obj, method, param) {};
    var HEAPF64 = [];
    var HEAP32 = [];
    ${code}
  `;

  return validateJavaScriptSyntax(wrappedCode, 'jslib.js');
}

/**
 * JavaScript 함수 시그니처 추출 (AST 기반)
 */
export function extractFunctionSignatures(code: string): Map<string, string[]> {
  const signatures = new Map<string, string[]>();

  const project = new Project({
    useInMemoryFileSystem: true,
    compilerOptions: {
      target: ScriptTarget.ES2015,
      allowJs: true,
    },
  });

  const sourceFile = project.createSourceFile('source.js', code, {
    scriptKind: ScriptKind.JS,
  });

  // 객체 리터럴 내부의 메서드 찾기
  sourceFile.forEachDescendant((node) => {
    if (node.getKind() === SyntaxKind.PropertyAssignment) {
      const propAssignment = node.asKind(SyntaxKind.PropertyAssignment);
      if (!propAssignment) return;

      const name = propAssignment.getName();
      const initializer = propAssignment.getInitializer();

      if (initializer) {
        const kind = initializer.getKind();
        if (kind === SyntaxKind.FunctionExpression || kind === SyntaxKind.ArrowFunction) {
          const func = initializer.asKind(SyntaxKind.FunctionExpression) || initializer.asKind(SyntaxKind.ArrowFunction);
          if (func) {
            const params = func.getParameters().map(p => p.getName());
            signatures.set(name, params);
          }
        }
      }
    }
  });

  return signatures;
}

/**
 * 검증 결과를 콘솔에 출력
 */
export function printValidationResult(result: JavaScriptValidationResult): void {
  if (result.valid) {
    console.log('✅ JavaScript validation successful');
  } else {
    console.log('❌ JavaScript validation failed');
  }

  if (result.errors.length > 0) {
    console.log('\nErrors:');
    for (const error of result.errors) {
      console.log(`  Line ${error.line}, Column ${error.column}: [${error.code}] ${error.message}`);
    }
  }

  if (result.warnings.length > 0) {
    console.log('\nWarnings:');
    for (const warning of result.warnings) {
      console.log(`  Line ${warning.line}, Column ${warning.column}: [${warning.code}] ${warning.message}`);
    }
  }
}
