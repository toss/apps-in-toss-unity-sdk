import type { RequiredExcept, SecureKey } from './keys';
type Props = {
    secureKey: RequiredExcept<SecureKey, 'subText'>;
    onPressIn: (value: string) => void;
};
export declare function SecureKeyButton({ secureKey: { value, label, subText }, onPressIn }: Props): import("react/jsx-runtime").JSX.Element;
export {};
