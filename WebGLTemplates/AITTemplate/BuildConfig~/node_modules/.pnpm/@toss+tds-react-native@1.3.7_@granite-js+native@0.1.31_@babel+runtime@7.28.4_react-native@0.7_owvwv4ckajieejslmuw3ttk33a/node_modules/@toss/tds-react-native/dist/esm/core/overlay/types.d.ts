/**
 * @category Types
 * @kind typedef
 * @name CreateOverlayElement
 * @description
 * `CreateOverlayElement`는 특정 컴포넌트를 오버레이 형태로 생성하고 관리하는 함수예요. `useOverlayBase` hook과 함께 사용해요.
 * 오버레이의 열림 상태를 제어하고, 필요할 때 오버레이를 닫거나 제거할 수 있어요.
 *
 * @property {boolean} isOpen  - 오버레이가 띄워져 있는지 여부를 나타내는 상태예요. `open()`이 호출되면 `isOpen`이 `true`로 설정돼요. 이 값을 확인해서 오버레이로 띄울 컴포넌트의 열림 상태를 제어할 수 있어요.
 * @property {() => void} close - 오버레이를 숨기기 위해 호출하는 함수예요. 이 함수를 호출하면 `isOpen`이 `false`로 설정됩니다. 주로 오버레이 컴포넌트의 `onClose` prop으로 사용돼요.
 * @property {() => void} exit - 오버레이를 화면에서 완전히 제거(unmount)하기 위해 호출하는 함수예요. 이 함수가 호출되면 오버레이 컴포넌트가 `View` 트리에서 제거돼요.
 */
export type CreateOverlayElement = (props: {
    isOpen: boolean;
    close: () => void;
    exit: () => void;
}) => JSX.Element;
