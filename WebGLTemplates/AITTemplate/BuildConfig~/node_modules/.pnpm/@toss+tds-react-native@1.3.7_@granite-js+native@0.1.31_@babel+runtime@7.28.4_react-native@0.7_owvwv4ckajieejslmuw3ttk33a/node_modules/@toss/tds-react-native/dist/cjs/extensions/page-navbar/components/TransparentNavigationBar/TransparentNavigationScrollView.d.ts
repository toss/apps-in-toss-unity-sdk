import type { ScrollView } from 'react-native';
/**
 * @private
 * @category Components
 * @kind component
 * @name TransparentNavigationScrollView
 * @description
 * `TransparentNavigationScrollView`는 스크롤을 감지하여 네비게이션 헤더의 배경에 블러 효과를 적용하는 컴포넌트예요. 이 컴포넌트는 스크롤에 따라 헤더의 투명도를 애니메이션으로 조절하고, iOS의 `BlurView`를 활용하여 시각적으로 부드럽고 매끄러운 네비게이션 경험을 제공합니다.
 *
 * 이 컴포넌트는 `IOScrollView`를 기반으로 하며, 스크롤 이벤트를 감지해 헤더의 블러 효과를 동적으로 제어합니다. `scrollEventThrottle`을 통해 스크롤 이벤트 빈도를 설정할 수 있고, 블러 효과가 적용되지 않을 경우, 배경색을 `adaptive.background`로 설정할 수 있어요.
 *
 * `react-navigation`의 `headerBackground` 옵션에서 발생하는 블러 렌더링 문제를 우회하기 위해 스크롤 뷰 뒤에 `BlurView`를 추가해서 정상적으로 블러가 적용되도록 설계되었어요.
 *
 * @param {ComponentProps<typeof IOScrollView>} props `TransparentNavigationScrollView`에 전달할 속성들이에요. `IOScrollView`의 속성을 그대로 사용할 수 있어요.
 * @param {number} [props.scrollEventThrottle=300] 스크롤 이벤트를 얼마나 자주 발생시킬지 설정하는 속성이에요. 단위는 milliseconds 이고 기본값은 `300ms`예요.
 * @param {(e: NativeSyntheticEvent<NativeScrollEvent>) => void} [props.onScroll] 스크롤이 발생할 때 호출되는 콜백 함수예요. 스크롤의 위치에 따라 블러 효과를 동적으로 적용해요.
 * @param {Ref<ScrollView>} ref `ScrollView` 인스턴스에 접근하기 위한 ref 객체에요. 이를 통해 `ScrollView`의 메서드에 접근할 수 있어요.
 *
 * @returns {JSX.Element} 스크롤 이벤트와 블러 효과를 포함한 `ScrollView`를 렌더링하는 JSX 엘리먼트를 반환해요. 헤더의 투명도가 스크롤에 따라 부드럽게 애니메이션 처리되며, 배경에 블러 효과가 추가됩니다.
 *
 * @example
 * ```tsx
 * import { TransparentNavigationScrollView } from '@granite/core';
 * import { Text, View, StyleSheet } from 'react-native';
 *
 * function MyScrollViewComponent() {
 *   return (
 *     <TransparentNavigationScrollView>
 *       <View style={styles.content}>
 *         <Text>Scroll to see the Blur effect on the header!</Text>
 *       </View>
 *     </TransparentNavigationScrollView>
 *   );
 * }
 *
 * const styles = StyleSheet.create({
 *   content: {
 *     height: 1000,
 *     justifyContent: 'center',
 *     alignItems: 'center',
 *   },
 * });
 *
 * export default MyScrollViewComponent;
 * ```
 *
 * @remarks
 * - 이 컴포넌트는 `react-navigation`과 함께 사용될 때 헤더의 배경에 블러 효과를 자연스럽게 적용해요. 스크롤 시 헤더가 블러 처리되어 더욱 세련된 UI를 만들 수 있어요.
 * - iOS에서만 블러 효과가 제대로 동작하며, Android에서는 블러가 적용되지 않을 수 있어요. 이 경우 배경색이 자동으로 `adaptive.background`로 설정돼요.
 * - `scrollEventThrottle`을 통해 스크롤 이벤트 빈도를 제어할 수 있어요. 기본값은 300ms로, 적절한 이벤트 빈도를 설정해 성능을 조절할 수 있어요.
 * - `BlurView`가 `ScrollView`보다 나중에 렌더링되어야 블러가 제대로 적용되므로, 컴포넌트 내에서 순서를 조정하여 이 문제를 해결했어요.
 *
 * @see [react-native BlurView Issue](https://github.com/Kureev/react-native-blur/issues/189)
 */
export declare const TransparentNavigationScrollView: import("react").ForwardRefExoticComponent<Omit<import("@granite-js/react-native").IOComponentProps & import("react-native").ScrollViewProps & import("react").RefAttributes<ScrollView>, "ref"> & import("react").RefAttributes<ScrollView>>;
