import type { BridgeProps } from './Bridge';
interface UseBridgeOptions {
    /** @default true */
    closeOnDestroy?: boolean;
}
/**
 *
 * `Bridge` 컴포넌트를 띄우는 훅이에요.
 * - `open` 이 호출되고 200ms 후에 `Bridge` 컴포넌트가 노출돼요
 * - `open` 이후에 `variant` 가 `basic` 일 때는 2.0s, `point` 일 때는 2.3s 후에 제거돼요
 * - `close` 가 호출되면 `Bridge` 컴포넌트가 즉시 제거돼요
 */
export declare function useBridge({ closeOnDestroy }?: UseBridgeOptions): {
    close: () => void;
    open: ({ onExited, onClose, color, ...options }: BridgeProps & {
        color?: string;
    }) => void;
};
export {};
