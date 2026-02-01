import * as path from 'path';
import { NAMESPACE_CATEGORY_OVERRIDES } from './constants.js';

/**
 * TypeScript 빌드 시 생성되는 $1, $2 등의 접미사 제거
 * 예: Location$1 -> Location
 */
export function cleanTypeName(name: string): string {
  return name.replace(/\$\d+$/, '');
}

/**
 * camelCase를 PascalCase로 변환
 * 예: appLogin -> AppLogin, startUpdateLocation -> StartUpdateLocation
 */
export function toPascalCase(str: string): string {
  if (!str) return str;
  return str.charAt(0).toUpperCase() + str.slice(1);
}

/**
 * 파일 경로에서 카테고리 추출
 * .d.ts 파일명을 PascalCase로 변환하여 카테고리로 사용
 * 예: appLogin.d.ts -> AppLogin
 */
export function getCategoryFromPath(filePath: string): string {
  const fileName = path.basename(filePath, '.d.ts');
  return fileName.charAt(0).toUpperCase() + fileName.slice(1);
}

/**
 * 네임스페이스 이름을 카테고리로 변환
 * - 특수 매핑이 있으면 사용
 * - 없으면 PascalCase로 변환하여 카테고리로 사용
 */
export function getNamespaceCategory(namespaceName: string): string {
  // 특수 매핑 확인
  if (NAMESPACE_CATEGORY_OVERRIDES[namespaceName]) {
    return NAMESPACE_CATEGORY_OVERRIDES[namespaceName];
  }
  // 기본값: PascalCase로 변환 (partner -> Partner, IAP -> IAP)
  return toPascalCase(namespaceName);
}
