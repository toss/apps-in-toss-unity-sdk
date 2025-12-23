export declare enum KeyboardStatusLevel {
    /** @description 키보드가 나타나지 않은 상태 */
    None = 0,
    /** @description 키보드가 숨겨진 뒤 상태 */
    DidHide = 1,
    /** @description 키보드가 숨겨져야 하는 상태 */
    WillHide = 2,
    /** @description 키보드가 보여져야 하는 상태 */
    WillShow = 3,
    /** @description 키보드가 보여진 뒤 상태 */
    DidShow = 4
}
/**
 * @name useKeyboardOpenStatus;
 * @description 키보드가 보여지고 숨겨지는 상태를 level 로 나타낸 Hook
 *
 * 키보드가 보여질 때 상태 순서
 * 1. None (= 0)
 * 2. WillShow (= 3)
 * 3. DidShow (= 4)
 */
export declare function useKeyboardStatusLevel(): KeyboardStatusLevel;
/**
 * @name useKeyboardOpened
 * @description 키보드가 열렸느지 여부
 */
export declare function useKeyboardOpened(): boolean;
/**
 * @name useKeyboardHeight
 * @description 키보드 높이
 */
export declare function useKeyboardHeight(): number;
