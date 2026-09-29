import { SourceFile, SyntaxKind } from 'ts-morph';
import { ParsedAPI, ParsedType } from '../types.js';
import { toPascalCase } from './utils.js';
import { parseType } from './type-parser.js';

/**
 * 이벤트 타입 정의에서 이벤트 목록 동적 파싱
 * 예: TdsEvent, GraniteEvent, AppsInTossEvent 타입 별칭에서 이벤트 추출
 */
export function parseEventTypeDefinition(
  typeName: string,
  sourceFile: SourceFile
): { eventName: string; hasData: boolean; dataType?: string }[] {
  const events: { eventName: string; hasData: boolean; dataType?: string }[] = [];

  // 타입 별칭 찾기
  const exportedDeclarations = sourceFile.getExportedDeclarations();
  const typeDeclarations = exportedDeclarations.get(typeName);

  if (!typeDeclarations || typeDeclarations.length === 0) {
    return events;
  }

  const typeAlias = typeDeclarations[0];
  if (typeAlias.getKind() !== SyntaxKind.TypeAliasDeclaration) {
    return events;
  }

  // 타입의 프로퍼티 추출 (이벤트 이름들)
  const type = typeAlias.getType();
  const properties = type.getProperties();

  for (const prop of properties) {
    const eventName = prop.getName();
    const propType = prop.getTypeAtLocation(typeAlias);

    // onEvent 프로퍼티 찾기
    const onEventProp = propType.getProperty('onEvent');
    if (!onEventProp) continue;

    const onEventType = onEventProp.getTypeAtLocation(typeAlias);
    const callSignatures = onEventType.getCallSignatures();

    if (callSignatures.length > 0) {
      const params = callSignatures[0].getParameters();
      const hasData = params.length > 0;

      let dataType: string | undefined;
      if (hasData && params[0]) {
        const paramType = params[0].getTypeAtLocation(typeAlias);
        // 인라인 객체 타입인 경우 이벤트명 기반으로 타입 이름 생성
        const typeText = paramType.getText();
        if (typeText.startsWith('{')) {
          // 인라인 객체: TdsNavigationAccessoryEventData 형태로 생성 (네임스페이스 접두사 포함)
          // typeName에서 'Event' 접미사를 제거하여 네임스페이스 접두사 추출 (TdsEvent -> Tds)
          const namespacePrefix = typeName.replace(/Event$/, '');
          dataType = `${namespacePrefix}${toPascalCase(eventName)}Data`;
        } else {
          // 명명된 타입 사용
          dataType = typeText;
        }
      }

      events.push({ eventName, hasData, dataType });
    }
  }

  return events;
}

/**
 * 이벤트 네임스페이스 파싱 (addEventListener 패턴이 있는 객체)
 * 각 이벤트를 별도의 Subscribe 메서드로 생성
 */
export function parseEventNamespace(namespaceName: string, sourceFile: SourceFile): ParsedAPI[] {
  const apis: ParsedAPI[] = [];
  // 이벤트 타입 이름을 동적으로 결정: tdsEvent -> TdsEvent (첫 글자 대문자화)
  const typeName = toPascalCase(namespaceName);

  // 동적으로 이벤트 정의 파싱
  const eventDefs = parseEventTypeDefinition(typeName, sourceFile);

  for (const eventDef of eventDefs) {
    // C# 메서드 이름: 네임스페이스 + Subscribe + PascalCase 이벤트명
    // 예: tdsEvent.addEventListener('navigationAccessoryEvent', ...)
    //     -> TdsEventSubscribeNavigationAccessoryEvent
    const pascalNamespace = toPascalCase(namespaceName);
    const pascalEventName = toPascalCase(eventDef.eventName);
    const fullName = `${pascalNamespace}Subscribe${pascalEventName}`;

    // 이벤트 데이터 타입
    let eventDataType: ParsedType | undefined;
    if (eventDef.hasData && eventDef.dataType) {
      eventDataType = {
        name: eventDef.dataType,
        kind: 'object',
        raw: eventDef.dataType,
      };
    }

    // 반환 타입은 Action (구독 해제 함수)
    const returnType: ParsedType = {
      name: 'Action',
      kind: 'function',
      raw: '() => void',
    };

    apis.push({
      name: fullName,
      pascalName: fullName,
      originalName: 'addEventListener',
      category: 'AppEvents',
      file: sourceFile.getFilePath(),
      description: `${namespaceName}.${eventDef.eventName} 이벤트를 구독합니다.`,
      parameters: [], // 이벤트 API는 콜백 파라미터를 C# 템플릿에서 직접 생성
      returnType,
      isAsync: false, // 이벤트 구독은 동기적
      hasPermission: false,
      namespace: namespaceName,
      // 이벤트 API 전용 필드
      isEventSubscription: true,
      eventName: eventDef.eventName,
      eventDataType,
    });
  }

  return apis;
}
