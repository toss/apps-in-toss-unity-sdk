/**
 * TypeScript 빌드 시 생성되는 $1, $2 등의 접미사 제거
 * 예: Location$1 -> Location
 */
export function cleanTypeName(name: string): string {
  const renamed = localTypeRenames.get(name);
  if (renamed) return renamed;
  return name.replace(/\$\d+$/, '');
}

/**
 * 번들이 같은 공개 이름을 두 선언에 겹쳐 쓰는 경우의 로컬 이름 -> 공개 export 이름.
 *
 * 롤업이 이름 충돌을 `$1` 접미사로 피한 뒤 접미사 쪽을 공개 이름으로 내보내면
 * (`ContactsViralParams$1 as ContactsViralParams`, `ContactsViralParams as ContactsViralOption`),
 * 접미사를 떼는 것만으로는 두 타입이 같은 이름이 된다. 충돌하는 쪽(접미사 없는 로컬 이름)을
 * 자기 export 이름으로 옮긴다. 충돌하지 않는 export 별칭(`AttachOptions as TossAdsAttachOptions`)은
 * 건드리지 않는다.
 */
const localTypeRenames = new Map<string, string>();

export function registerLocalTypeRenames(exported: ReadonlyMap<string, readonly unknown[]>): void {
  localTypeRenames.clear();
  for (const [exportName, decls] of exported) {
    for (const decl of decls) {
      const localName = (decl as { getName?: () => string | undefined }).getName?.();
      if (!localName || localName === exportName) continue;
      const stripped = localName.replace(/\$\d+$/, '');
      // 접미사를 뗀 이름이 다른 선언의 공개 이름이면 충돌이다
      const owner = exported.get(stripped);
      if (stripped === localName && owner && !owner.includes(decl)) {
        localTypeRenames.set(localName, exportName);
      }
    }
  }
}

/**
 * camelCase를 PascalCase로 변환
 * 예: appLogin -> AppLogin, startUpdateLocation -> StartUpdateLocation
 */
export function toPascalCase(str: string): string {
  if (!str) return str;
  return str.charAt(0).toUpperCase() + str.slice(1);
}
