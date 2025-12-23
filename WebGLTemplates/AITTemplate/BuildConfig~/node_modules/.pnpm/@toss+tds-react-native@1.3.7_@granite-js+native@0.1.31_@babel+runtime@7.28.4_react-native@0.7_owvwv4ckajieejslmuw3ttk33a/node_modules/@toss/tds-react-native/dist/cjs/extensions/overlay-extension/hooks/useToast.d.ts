type ToastButton = {
    /**
     * `Toast` 컴포넌트의 버튼에 표시될 텍스트에요.
     */
    text: string;
    /**
     * `Toast` 컴포넌트의 버튼을 클릭할 때 실행될 함수에요.
     */
    onPress: () => void;
};
type ToastPosition = 'top' | 'bottom';
type ToastIcon = {
    /**
     * `Toast` 컴포넌트에 표시될 아이콘의 이름이에요.
     *
     * lottie와 함께 사용할 수 없어요.
     */
    icon?: string;
    /**
     * `Toast` 컴포넌트에 표시될 아이콘의 모양을 설정해요.
     *
     * @default undefined
     */
    iconType?: 'circle' | 'square';
};
type ToastLottie = {
    /**
     * `Toast` 컴포넌트에 표시될 로티 애니메이션의 URL이에요.
     *
     * icon과 함께 사용할 수 없어요.
     */
    lottie?: string;
};
type BaseToastOptions = {
    /**
     * `Toast` 컴포넌트의 토스트 메시지의 위치를 설정해요.
     *
     * @default 'bottom'
     */
    type?: ToastPosition;
    /**
     * `Toast` 컴포넌트에 표시될 버튼이에요.
     */
    button?: ToastButton;
    /**
     * `Toast` 컴포넌트가 자동으로 사라질 때까지의 시간(ms)이에요.
     *
     * 버튼이 있는 경우 기본값은 5000ms, 없는 경우 3000ms이에요.
     */
    duration?: number;
    /**
     * `Toast` 컴포넌트가 닫히기 시작할 때 호출되는 콜백 함수에요.
     *
     * 아래와 같은 상황에서 사용할 수 있어요:
     * - `duration` 시간이 지나서 자동으로 닫힐 때
     * - 사용자가 `Toast` 컴포넌트를 드래그해서 닫을 때
     * - 수동으로 `open` 값을 `false`로 변경할 때
     */
    onClose?: () => void;
    /**
     * `Toast` 컴포넌트가 완전히 사라지고 애니메이션이 끝난 후에 호출되는 콜백 함수에요.
     *
     * 아래와 같은 상황에서 사용할 수 있어요:
     * - `Toast` 컴포넌트가 완전히 사라진 후 다음 작업을 진행해야 할 때
     */
    onExited?: () => void;
    /**
     * `Toast` 컴포넌트가 화면에 나타나고 애니메이션이 끝난 후에 호출되는 콜백 함수에요.
     *
     * 아래와 같은 상황에서 사용할 수 있어요:
     * - `Toast` 컴포넌트가 화면에 나타나고 다음 작업을 진행해야 할 때
     **/
    onEntered?: () => void;
};
type OpenToastOptions = BaseToastOptions & ((ToastIcon & {
    lottie?: never;
}) | (ToastLottie & {
    icon?: never;
    iconType?: never;
}));
interface UseToastOptions {
    /** @default true */
    closeOnDestroy?: boolean;
}
export declare function useToast(options?: UseToastOptions): {
    close: () => void;
    open: (message: string, { duration, button, type, lottie, icon, iconType, onExited, onClose, ...options }?: OpenToastOptions) => void;
};
export {};
