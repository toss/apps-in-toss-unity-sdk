import type { NativeStackNavigationOptions } from '@granite-js/native/@react-navigation/native-stack';
/**
 * @public
 * @category Navigation
 * @kind constant
 * @name TransparentNavigationScreenOptions
 * @description
 * `TransparentNavigationScreenOptions`는 `react-navigation`의 `NativeStackNavigationOptions`을 사용하여 투명한 네비게이션 헤더를 쉽게 설정할 수 있도록 도와주는 옵션 객체예요. 이 옵션을 사용하면 네비게이션 헤더가 투명하게 설정되어, 배경 콘텐츠와 자연스럽게 어우러지는 UI를 구현할 수 있어요.
 *
 * 헤더의 스타일을 투명하게 만들기 위해 `headerTransparent: true` 속성과 `backgroundColor: 'transparent'` 스타일이 적용되어 있어, 헤더가 배경과 어우러지면서도 터치 영역과 같은 기본 기능은 유지돼요.
 *
 * 이 옵션은 `@react-navigation/native-stack`의 `NativeStackNavigationOptions` 타입을 기반으로 하며, 네비게이션 화면의 헤더를 투명하게 설정할 때 유용해요.
 *
 * @type {NativeStackNavigationOptions}
 *
 * @property {boolean} headerTransparent 네비게이션 헤더를 투명하게 설정하는 속성이에요. `true`로 설정되어 헤더가 투명하게 보이도록 해요.
 * @property {object} headerStyle 네비게이션 헤더의 스타일을 정의해요. 여기서는 `backgroundColor: 'transparent'`로 설정되어, 배경색이 투명하게 적용돼요.
 *
 * @example
 * ```tsx
 * import React from 'react';
 * import { createNativeStackNavigator } from '@react-navigation/native-stack';
 * import { TransparentNavigationScreenOptions } from '@granite/tuba';
 * import MyScreen from './MyScreen';
 *
 * const Stack = createNativeStackNavigator();
 *
 * function MyNavigator() {
 *   return (
 *     <Stack.Navigator screenOptions={TransparentNavigationScreenOptions}>
 *       <Stack.Screen name="MyScreen" component={MyScreen} />
 *     </Stack.Navigator>
 *   );
 * }
 *
 * export default MyNavigator;
 * ```
 *
 * @remarks
 * - 이 옵션은 `NativeStackNavigator`에 사용되어야 하며, 투명한 헤더를 적용해 네비게이션 바를 자연스럽게 보이도록 만들어요.
 * - `headerTransparent: true`로 설정하면 헤더가 투명해지지만, 네비게이션 바는 그대로 유지되어 터치 가능하고, 뒤로 가기 버튼 같은 기본 기능도 유지돼요.
 * - 배경과 헤더가 자연스럽게 어우러지는 화면을 만들고 싶을 때 유용하게 사용할 수 있어요.
 *
 * @see [react-navigation NativeStackNavigationOptions](https://reactnavigation.org/docs/native-stack-navigator/#options)
 */
export declare const TransparentNavigationScreenOptions: NativeStackNavigationOptions;
