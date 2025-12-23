import type { SecureKeyRowRequired } from './keys';
export declare function withShuffledEmptyCells(row: SecureKeyRowRequired): {
    keys: import("./keys").RequiredExcept<import("./keys").SecureKey, "subText">[];
    emptyCellCount: number;
};
