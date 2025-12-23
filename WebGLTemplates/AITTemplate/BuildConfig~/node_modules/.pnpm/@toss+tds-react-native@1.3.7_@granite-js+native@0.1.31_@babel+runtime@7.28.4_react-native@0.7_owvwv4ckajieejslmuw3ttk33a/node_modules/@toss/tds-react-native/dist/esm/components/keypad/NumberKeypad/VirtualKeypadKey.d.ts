export declare enum SPECIAL_KEY {
    blank = "blank",
    backspace = "backspace"
}
type Props = {
    char: string;
    onKeyPressIn: (value: string) => void;
};
export declare const VirtualKeypadKey: import("react").NamedExoticComponent<Props>;
export {};
