/** @tossdocs-ignore */
/**
 * @deprecated This feature is now available in the es-toolkit package as `keyBy`.
 */
export declare function createMapByKey<Entity, KeyName extends keyof Entity>(objects: Entity[], key: KeyName): Map<Entity[KeyName], Entity>;
