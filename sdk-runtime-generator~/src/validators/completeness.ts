import { ParsedAPI, GeneratedCode, ValidationError } from '../types.js';
import picocolors from 'picocolors';

/**
 * API 완전성 검증
 *
 * 모든 소스 API가 생성되었는지 확인합니다.
 * 누락된 API가 하나라도 있으면 에러를 발생시킵니다.
 */
export function validateCompleteness(
  sourceAPIs: ParsedAPI[],
  generatedCodes: GeneratedCode[]
): { success: boolean; errors: ValidationError[] } {
  const errors: ValidationError[] = [];

  // 생성된 API 이름 목록
  const generatedNames = new Set(generatedCodes.map(g => g.api.name));

  // 누락된 API 찾기
  const missingAPIs = sourceAPIs.filter(api => !generatedNames.has(api.name));

  if (missingAPIs.length > 0) {
    // 파일별로 그룹화
    const byFile = new Map<string, ParsedAPI[]>();
    for (const api of missingAPIs) {
      if (!byFile.has(api.file)) {
        byFile.set(api.file, []);
      }
      byFile.get(api.file)!.push(api);
    }

    // 에러 메시지 생성
    const fileList = Array.from(byFile.entries())
      .map(([file, apis]) => {
        const fileName = file.split('/').pop() || file;
        const apiList = apis.map(api => `  - ${api.name}()`).join('\n');
        return `\n📄 ${fileName}\n${apiList}`;
      })
      .join('\n');

    errors.push({
      type: 'missing',
      message: picocolors.red(`
❌ 생성 실패: 누락된 API 발견

누락된 API (${missingAPIs.length}개):
${fileList}

🛠️  조치 필요:
복잡한 타입이나 패턴이 감지되었을 수 있습니다.

1. tools/generate-unity-sdk/src/generators/ 업데이트
2. 복잡한 타입은 src/templates/에 수동 템플릿 추가
3. 생성 후 다시 실행

생성 중단됨.
      `),
      suggestion: '수동 템플릿 작성이 필요한 API가 있는지 확인하세요.',
    });
  }

  return {
    success: errors.length === 0,
    errors,
  };
}

/**
 * 생성 결과 요약 출력
 */
export function printSummary(sourceAPIs: ParsedAPI[], generatedCodes: GeneratedCode[]): void {
  const totalAPIs = sourceAPIs.length;
  const generatedAPIs = generatedCodes.length;
  const percentage = ((generatedAPIs / totalAPIs) * 100).toFixed(1);

  console.log(picocolors.cyan('\n📋 생성 요약:'));
  console.log(`  - 전체 API: ${picocolors.bold(totalAPIs.toString())}개`);
  console.log(`  - 생성 완료: ${picocolors.bold(generatedAPIs.toString())}개 (${percentage}%)`);

  if (generatedAPIs === totalAPIs) {
    console.log(picocolors.green(`  ✅ 모든 API 생성 완료!`));
  } else {
    console.log(picocolors.yellow(`  ⚠️  ${totalAPIs - generatedAPIs}개 API 누락`));
  }

  // 카테고리별 통계
  const categories = new Map<string, number>();
  for (const code of generatedCodes) {
    const cat = code.api.category;
    categories.set(cat, (categories.get(cat) || 0) + 1);
  }

  console.log(picocolors.cyan('\n📊 카테고리별:'));
  for (const [category, count] of Array.from(categories.entries()).sort()) {
    console.log(`  - ${category}: ${count}개`);
  }
}
