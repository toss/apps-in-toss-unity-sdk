export declare enum KeypadStatus {
    ALPHABET = 0,
    ALPHABET_CAPITALIZED = 1,
    SPECIAL = 2
}
export declare function useKeypad(): {
    mode: KeypadStatus;
    keys: {
        keys: import("./keys").RequiredExcept<import("./keys").SecureKey, "subText">[];
        emptyCellCount: number;
    }[];
    shuffledKeys: {
        0: {
            keys: import("./keys").RequiredExcept<import("./keys").SecureKey, "subText">[];
            emptyCellCount: number;
        }[];
        1: {
            keys: import("./keys").RequiredExcept<import("./keys").SecureKey, "subText">[];
            emptyCellCount: number;
        }[];
        2: {
            keys: import("./keys").RequiredExcept<import("./keys").SecureKey, "subText">[];
            emptyCellCount: number;
        }[];
    };
    toggleShift: () => void;
    toggleSpecialAndAlphabet: () => void;
    reorderEmptyCells: () => void;
};
