export type RequiredExcept<T, E extends keyof T> = Required<Omit<T, E>> & Pick<T, E>;
export declare enum SPECIAL_KEY {
    space = "space",
    backspace = "backspace"
}
export declare enum CONTROL_KEY {
    shift = "shift",
    characterType = "characterType",
    submit = "submit"
}
export type SecureKey = {
    /** @default value와 같은 값 */
    label?: string;
    value: string | number;
    /** @default 1 */
    width?: number;
    subText?: string;
    templateKey?: string;
};
export type SecureKeyRow = {
    emptyCellCount: number;
    keys: SecureKey[];
};
export type SecureKeyRowRequired = {
    emptyCellCount: number;
    keys: Array<RequiredExcept<SecureKey, 'subText'>>;
};
export declare const englishCharacterKeys: SecureKeyRow[];
export declare const specialCharacterKeys: SecureKeyRow[];
export declare const CONTROL_KEY_SHIFT_WIDTH = 2;
export declare const CONTROL_KEY_CHARACTER_TYPE_WIDTH = 2;
export declare const CONTROL_KEY_SPACE_WIDTH = 5;
export declare const CONTROL_KEY_SUBMIT_WIDTH = 5;
export declare const controlKeyRow: SecureKeyRow;
export declare function fillInDefaults(rows: SecureKeyRow[]): SecureKeyRowRequired[];
export declare function capitalize(rows: SecureKeyRowRequired[]): SecureKeyRowRequired[];
