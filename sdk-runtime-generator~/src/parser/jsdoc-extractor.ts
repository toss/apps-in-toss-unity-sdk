/**
 * JSDoc에서 파라미터 설명 추출
 */
export function extractParamDescriptions(jsDoc: any): Map<string, string> {
  const paramMap = new Map<string, string>();
  if (!jsDoc) return paramMap;

  const tags = jsDoc.getTags();
  for (const tag of tags) {
    if (tag.getTagName() === 'param') {
      const comment = tag.getCommentText();
      const name = tag.getName?.();
      if (name && comment) {
        paramMap.set(name, comment.trim());
      }
    }
  }
  return paramMap;
}

/**
 * JSDoc에서 @returns 태그 설명 추출
 */
export function extractReturnsDescription(jsDoc: any): string | undefined {
  if (!jsDoc) return undefined;

  const tags = jsDoc.getTags();
  const returnsTag = tags.find((tag: any) => tag.getTagName() === 'returns');
  if (returnsTag) {
    const comment = returnsTag.getCommentText();
    return comment ? comment.trim() : undefined;
  }
  return undefined;
}

/**
 * JSDoc에서 @example 태그들 추출
 */
export function extractExamples(jsDoc: any): string[] {
  if (!jsDoc) return [];

  const examples: string[] = [];
  const tags = jsDoc.getTags();
  for (const tag of tags) {
    if (tag.getTagName() === 'example') {
      const comment = tag.getCommentText();
      if (comment) {
        examples.push(comment.trim());
      }
    }
  }
  return examples;
}

/**
 * 변수 선언의 텍스트에서 특정 프로퍼티의 JSDoc 추출
 * 프로퍼티 직전의 JSDoc만 추출 (다른 프로퍼티 정의가 중간에 없어야 함)
 */
export function extractJsDocForProperty(varDeclText: string, propertyName: string): string {
  // 프로퍼티 정의 위치 찾기
  const propRegex = new RegExp(`(?:\\[?"?)?${propertyName}(?:"?\\])?\\s*:`, 'g');
  const propMatch = propRegex.exec(varDeclText);
  if (!propMatch) return '';

  const propIndex = propMatch.index;

  // 프로퍼티 앞부분 텍스트
  const textBefore = varDeclText.substring(0, propIndex);

  // 마지막 JSDoc 주석 찾기
  const jsDocMatches = textBefore.match(/\/\*\*[\s\S]*?\*\//g);
  if (!jsDocMatches || jsDocMatches.length === 0) return '';

  const lastJsDoc = jsDocMatches[jsDocMatches.length - 1];

  // 마지막 JSDoc과 프로퍼티 사이에 다른 프로퍼티 정의가 없는지 확인
  const lastJsDocIndex = textBefore.lastIndexOf(lastJsDoc);
  const betweenText = textBefore.substring(lastJsDocIndex + lastJsDoc.length);

  // 프로퍼티 정의 패턴 (다른 프로퍼티가 있으면 해당 JSDoc은 이 프로퍼티의 것이 아님)
  // 패턴: identifier: 또는 "identifier": 또는 [identifier]:
  if (/(?:\[?"?)?[a-zA-Z_$][a-zA-Z0-9_$]*(?:"?\])?\s*:/.test(betweenText.trim())) {
    return '';
  }

  return lastJsDoc;
}
