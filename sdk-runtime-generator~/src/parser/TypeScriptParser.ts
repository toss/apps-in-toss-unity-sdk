import { Project, SourceFile } from 'ts-morph';
import * as fs from 'fs';
import { ParsedAPI, ParsedProperty, ParsedTypeDefinition } from '../types.js';
import { parseBundle, type BundleExportRecord } from './bundle-parser.js';
import { parseTypeDefinitionsFromFile } from './type-definition-parser.js';
import { parseBundleTypeByName } from './bundle-types.js';
import { registerUnionAliases } from './union-alias.js';
import { registerLocalTypeRenames } from './utils.js';
import { isInlineAnonymousObjectParam } from '../generators/csharp/type-collector.js';

/**
 * web-framework 3.x 번들(`dist/index.d.ts`)을 유일한 입력으로 API와 타입 정의를 추출한다.
 */
export class TypeScriptParser {
  private project: Project;
  private bundleSource: SourceFile;
  private _inventory: BundleExportRecord[] = [];

  /**
   * @param bundleFile web-framework 번들 index.d.ts 절대 경로
   * @throws 번들 파일이 없으면 예외 (다른 입력으로 대체하지 않는다)
   */
  constructor(private readonly bundleFile: string) {
    if (!fs.existsSync(bundleFile)) {
      throw new Error(
        `web-framework 번들 index.d.ts를 찾을 수 없습니다: ${bundleFile}\n` +
          `이 SDK는 @apps-in-toss/web-framework 3.x의 dist/index.d.ts만 입력으로 씁니다.\n` +
          `설치된 web-framework 버전이 3.x인지 확인하고 'pnpm install'을 다시 실행하세요.`,
      );
    }
    this.project = new Project({
      skipAddingFilesFromTsConfig: true,
      compilerOptions: {
        // nullable 타입 감지를 위해 strictNullChecks 활성화
        strictNullChecks: true,
      },
    });
    this.bundleSource = this.project.addSourceFileAtPath(bundleFile);
    registerLocalTypeRenames(this.bundleSource.getExportedDeclarations());
    registerUnionAliases(this.bundleSource);
  }

  /** 번들 export 인벤토리 (parseAPIs 이후 채워진다) */
  get inventory(): BundleExportRecord[] {
    return this._inventory;
  }

  /**
   * 번들 타입 정의 중 on-demand로 필요한 타입 하나를 파싱 (API 시그니처가 참조하지만
   * 정의 파싱 경로에서 만들어지지 않은 타입의 해결용)
   */
  parseBundleType(typeName: string): ParsedTypeDefinition | null {
    return parseBundleTypeByName(this.bundleSource, typeName);
  }

  /** 번들의 모든 export를 C# 진입점으로 분류하고 API 목록을 돌려준다 */
  async parseAPIs(): Promise<ParsedAPI[]> {
    const { apis, inventory } = parseBundle(this.bundleSource);
    this._inventory = inventory;
    return apis;
  }

  /**
   * 모든 타입 정의 파싱 (enum, interface)
   */
  async parseTypeDefinitions(): Promise<ParsedTypeDefinition[]> {
    const typeMap = new Map<string, ParsedTypeDefinition>();
    parseTypeDefinitionsFromFile(this.bundleSource, typeMap);
    // 콜백 파라미터가 인라인 익명 객체인 인터페이스(예: processProductGrant: (params: { orderId }) => ...)는
    // 타입 정의 경로로 생성하면 `Func<object, bool>`로 뭉개진다. API 파라미터 경로(generateTypes)가
    // `<Parent><Callback>Param` 명명 클래스를 만들어 주므로, 이 인터페이스는 그쪽에 맡긴다.
    return Array.from(typeMap.values()).filter(
      def => !(def.kind === 'interface' && hasInlineAnonymousCallbackParam(def.properties ?? [])),
    );
  }
}

/** 프로퍼티(중첩 객체 포함)에 첫 파라미터가 인라인 익명 객체인 콜백이 있는지 */
function hasInlineAnonymousCallbackParam(props: ParsedProperty[], depth = 0): boolean {
  if (depth > 3) return false;
  return props.some(prop => {
    const t = prop.type;
    if (t.kind === 'function' && isInlineAnonymousObjectParam(t.functionParams?.[0])) return true;
    if (t.kind === 'object' && t.properties) return hasInlineAnonymousCallbackParam(t.properties, depth + 1);
    return false;
  });
}
