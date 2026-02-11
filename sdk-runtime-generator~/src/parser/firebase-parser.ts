/**
 * Firebase TypeScript 정의 파서
 *
 * Firebase JS SDK의 .d.ts 파일에서 API 정의를 추출하여
 * C# 코드 생성에 필요한 구조로 변환합니다.
 *
 * Phase 1: Analytics + Auth
 * Phase 2: Firestore + Realtime Database (별도 PR)
 * Phase 3: Remote Config + Storage + Messaging (별도 PR)
 */

import { FIREBASE_CATEGORIES } from '../categories.js';

/**
 * Firebase API 정의 (생성기에 전달되는 구조)
 */
export interface FirebaseAPIDefinition {
  /** API 이름 (예: logEvent) */
  name: string;
  /** C# 메서드 이름 (PascalCase, 예: LogEvent) */
  csharpName: string;
  /** Firebase 카테고리 (예: Firebase.Analytics) */
  category: string;
  /** Firebase 서비스 (예: analytics, auth) */
  service: string;
  /** 파라미터 목록 */
  parameters: FirebaseParameter[];
  /** 반환 타입 */
  returnType: FirebaseReturnType;
  /** Promise 반환 여부 */
  isAsync: boolean;
  /** 구독 패턴 (onAuthStateChanged 등) */
  isSubscription: boolean;
  /** JSDoc 설명 */
  description?: string;
}

/**
 * Firebase 파라미터 정의
 */
export interface FirebaseParameter {
  name: string;
  /** C# 타입 */
  csharpType: string;
  /** TypeScript 타입 */
  tsType: string;
  /** 선택적 파라미터 여부 */
  optional: boolean;
  /** JSON 직렬화 필요 여부 */
  needsSerialization: boolean;
  /** 설명 */
  description?: string;
}

/**
 * Firebase 반환 타입 정의
 */
export interface FirebaseReturnType {
  /** C# 타입 */
  csharpType: string;
  /** TypeScript 타입 */
  tsType: string;
  /** void 여부 */
  isVoid: boolean;
}

/**
 * Firebase 타입 정의 (C# class/enum 생성용)
 */
export interface FirebaseTypeDefinition {
  name: string;
  kind: 'class' | 'enum';
  properties?: FirebaseTypeProperty[];
  enumValues?: string[];
  description?: string;
}

/**
 * Firebase 타입 프로퍼티
 */
export interface FirebaseTypeProperty {
  name: string;
  csharpType: string;
  jsonName: string;
  nullable: boolean;
  description?: string;
}

/**
 * Firebase API 정의를 하드코딩으로 제공합니다.
 *
 * Firebase JS SDK의 .d.ts 파일은 복잡한 제네릭과 오버로드를 사용하므로
 * ts-morph 파싱 대신 수동으로 API를 정의합니다.
 * 이는 기존 web-framework 파서와 다른 접근 방식이지만,
 * Firebase API가 안정적이고 변경이 드물기 때문에 실용적입니다.
 */
export function parseFirebaseAPIs(): {
  apis: FirebaseAPIDefinition[];
  types: FirebaseTypeDefinition[];
} {
  const apis: FirebaseAPIDefinition[] = [];
  const types: FirebaseTypeDefinition[] = [];

  // =====================================================
  // Firebase Init
  // =====================================================
  apis.push({
    name: 'initializeApp',
    csharpName: 'Initialize',
    category: 'Firebase.Init',
    service: 'app',
    parameters: [],
    returnType: { csharpType: 'void', tsType: 'void', isVoid: true },
    isAsync: true,
    isSubscription: false,
    description: 'Firebase를 초기화합니다. firebase-bridge.ts에 설정된 config을 사용합니다.',
  });

  // =====================================================
  // Firebase Analytics
  // =====================================================
  apis.push({
    name: 'logEvent',
    csharpName: 'LogEvent',
    category: 'Firebase.Analytics',
    service: 'analytics',
    parameters: [
      { name: 'eventName', csharpType: 'string', tsType: 'string', optional: false, needsSerialization: false, description: '이벤트 이름' },
      { name: 'eventParams', csharpType: 'string', tsType: 'Record<string, any>', optional: true, needsSerialization: true, description: '이벤트 파라미터 (JSON 문자열)' },
    ],
    returnType: { csharpType: 'void', tsType: 'void', isVoid: true },
    isAsync: false,
    isSubscription: false,
    description: 'Analytics 이벤트를 기록합니다.',
  });

  apis.push({
    name: 'setUserId',
    csharpName: 'SetUserId',
    category: 'Firebase.Analytics',
    service: 'analytics',
    parameters: [
      { name: 'userId', csharpType: 'string', tsType: 'string', optional: false, needsSerialization: false, description: '사용자 ID' },
    ],
    returnType: { csharpType: 'void', tsType: 'void', isVoid: true },
    isAsync: false,
    isSubscription: false,
    description: 'Analytics 사용자 ID를 설정합니다.',
  });

  apis.push({
    name: 'setUserProperties',
    csharpName: 'SetUserProperties',
    category: 'Firebase.Analytics',
    service: 'analytics',
    parameters: [
      { name: 'properties', csharpType: 'string', tsType: 'Record<string, string>', optional: false, needsSerialization: true, description: '사용자 속성 (JSON 문자열)' },
    ],
    returnType: { csharpType: 'void', tsType: 'void', isVoid: true },
    isAsync: false,
    isSubscription: false,
    description: 'Analytics 사용자 속성을 설정합니다.',
  });

  apis.push({
    name: 'setAnalyticsCollectionEnabled',
    csharpName: 'SetAnalyticsCollectionEnabled',
    category: 'Firebase.Analytics',
    service: 'analytics',
    parameters: [
      { name: 'enabled', csharpType: 'bool', tsType: 'boolean', optional: false, needsSerialization: false, description: '수집 활성화 여부' },
    ],
    returnType: { csharpType: 'void', tsType: 'void', isVoid: true },
    isAsync: false,
    isSubscription: false,
    description: 'Analytics 데이터 수집을 활성화/비활성화합니다.',
  });

  // =====================================================
  // Firebase Auth
  // =====================================================
  apis.push({
    name: 'signInAnonymously',
    csharpName: 'SignInAnonymously',
    category: 'Firebase.Auth',
    service: 'auth',
    parameters: [],
    returnType: { csharpType: 'FirebaseUser', tsType: 'User', isVoid: false },
    isAsync: true,
    isSubscription: false,
    description: '익명으로 로그인합니다.',
  });

  apis.push({
    name: 'signInWithCustomToken',
    csharpName: 'SignInWithCustomToken',
    category: 'Firebase.Auth',
    service: 'auth',
    parameters: [
      { name: 'token', csharpType: 'string', tsType: 'string', optional: false, needsSerialization: false, description: '커스텀 토큰' },
    ],
    returnType: { csharpType: 'FirebaseUser', tsType: 'User', isVoid: false },
    isAsync: true,
    isSubscription: false,
    description: '커스텀 토큰으로 로그인합니다.',
  });

  apis.push({
    name: 'signOut',
    csharpName: 'SignOut',
    category: 'Firebase.Auth',
    service: 'auth',
    parameters: [],
    returnType: { csharpType: 'void', tsType: 'void', isVoid: true },
    isAsync: true,
    isSubscription: false,
    description: '로그아웃합니다.',
  });

  apis.push({
    name: 'onAuthStateChanged',
    csharpName: 'OnAuthStateChanged',
    category: 'Firebase.Auth',
    service: 'auth',
    parameters: [],
    returnType: { csharpType: 'FirebaseUser', tsType: 'User | null', isVoid: false },
    isAsync: false,
    isSubscription: true,
    description: '인증 상태 변경을 구독합니다. 로그인/로그아웃 시 콜백이 호출됩니다.',
  });

  // =====================================================
  // Types
  // =====================================================
  types.push({
    name: 'FirebaseUser',
    kind: 'class',
    description: 'Firebase 인증 사용자 정보',
    properties: [
      { name: 'Uid', csharpType: 'string', jsonName: 'uid', nullable: false, description: '사용자 고유 ID' },
      { name: 'Email', csharpType: 'string', jsonName: 'email', nullable: true, description: '이메일 주소' },
      { name: 'DisplayName', csharpType: 'string', jsonName: 'displayName', nullable: true, description: '표시 이름' },
      { name: 'PhotoURL', csharpType: 'string', jsonName: 'photoURL', nullable: true, description: '프로필 사진 URL' },
      { name: 'PhoneNumber', csharpType: 'string', jsonName: 'phoneNumber', nullable: true, description: '전화번호' },
      { name: 'IsAnonymous', csharpType: 'bool', jsonName: 'isAnonymous', nullable: false, description: '익명 사용자 여부' },
      { name: 'EmailVerified', csharpType: 'bool', jsonName: 'emailVerified', nullable: false, description: '이메일 인증 여부' },
    ],
  });

  return { apis, types };
}

/**
 * API 이름으로 Firebase 카테고리를 찾습니다.
 */
export function getFirebaseCategory(apiName: string): string | null {
  for (const [category, apis] of Object.entries(FIREBASE_CATEGORIES)) {
    if (apis.includes(apiName)) {
      return category;
    }
  }
  return null;
}

/**
 * toPascalCase 변환 (camelCase → PascalCase)
 */
export function toPascalCase(name: string): string {
  return name.charAt(0).toUpperCase() + name.slice(1);
}
