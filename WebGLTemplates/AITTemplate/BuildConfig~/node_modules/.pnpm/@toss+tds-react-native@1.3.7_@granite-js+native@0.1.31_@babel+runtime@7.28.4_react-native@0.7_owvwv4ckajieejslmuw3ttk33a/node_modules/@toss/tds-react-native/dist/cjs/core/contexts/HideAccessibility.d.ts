import type { ReactNode } from 'react';
import type { ViewProps } from 'react-native';
interface HideAccessibilityContextValue {
    isActive: boolean;
    activate: () => void;
    inactivate: () => void;
}
export interface HideAccessibilityProviderProps {
    children: ReactNode;
}
/**
 * 특정 영역과 그 이하의 접근성을 숨기거나 다시 노출할 수 있도록 설정해요.
 * `HideAccessibilityProvider` 컴포넌트는 `HideAccessibilityView` 컴포넌트를 포함하는 컴포넌트를 감싸야 동작해요.
 * 또한, `useHideAccessibility` 훅을 사용하는 컴포넌트도 반드시 감싸고 있어야 해요.
 * 접근성을 제어하는 상태를 관리하고, 하위 컴포넌트에서 이를 제어할 수 있게 해줘요.
 * 주로 바텀싯, 모달, 다이얼로그 같은 화면을 덮는 UI에서 사용해요.
 *
 * @example
 * ```tsx
 * <HideAccessibilityProvider>
 *   <SomeComponent /> // 여기서 useHideAccessibility 를 호출
 *   <HideAccessibilityView>
 *     <Text>Hidden content</Text>
 *   </HideAccessibilityView>
 * </HideAccessibilityProvider>
 * ```
 */
export declare const HideAccessibilityProvider: ({ children }: HideAccessibilityProviderProps) => import("react/jsx-runtime").JSX.Element;
/**
 * 접근성을 제어할 수 있는 훅이에요.
 * `activate`를 호출하면 상위의 `HideAccessibilityView` 컴포넌트와 그 이하가 접근성에서 제외돼요.
 * `inactivate`를 호출하면 다시 접근성에 포함돼요.
 * `HideAccessibilityProvider` 컴포넌트 내부에서만 호출할 수 있고, 그렇지 않으면 에러가 발생해요.
 * 이 동작은 안드로이드 TalkBack이나 iOS VoiceOver에서 요소를 읽지 않도록 설정해줘요.
 *
 * @example
 * ```tsx
 * const { activate, inactivate } = useHideAccessibility();
 *
 * useEffect(() => {
 *   if (open) {
 *     activate();
 *   } else {
 *     inactivate();
 *   }
 *   return inactivate;
 * }, [open]);
 * ```
 */
export declare const useHideAccessibility: () => HideAccessibilityContextValue;
export interface HideAccessibilityViewProps extends ViewProps {
    children: ReactNode;
}
/**
 * `HideAccessibilityView` 컴포넌트는 `importantForAccessibility`와 `accessibilityElementsHidden` 속성을 사용해
 * 안드로이드 TalkBack이나 iOS VoiceOver에서 특정 영역과 그 이하의 요소를 읽지 않도록 설정해요.
 *
 * `HideAccessibilityView` 컴포넌트는 내부적으로 `View`로 감싸고 있으며, 기본적으로 `flex: 1` 스타일이 적용돼요.
 * 커스텀 스타일을 적용할 때도 `flex: 1`을 반드시 포함해야 정상적으로 레이아웃이 동작해요.
 */
export declare const HideAccessibilityView: ({ children, style, ...rest }: HideAccessibilityViewProps) => import("react/jsx-runtime").JSX.Element;
export {};
