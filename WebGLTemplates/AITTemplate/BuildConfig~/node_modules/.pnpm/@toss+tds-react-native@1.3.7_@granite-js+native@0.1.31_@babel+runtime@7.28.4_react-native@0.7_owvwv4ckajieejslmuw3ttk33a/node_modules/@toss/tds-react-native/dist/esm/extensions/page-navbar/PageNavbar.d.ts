import type { ReactNode } from 'react';
import { CompatAccessoryButton } from './CompatAccessoryButton';
import { PageAccessoryButtons } from './PageAccessoryButtons';
import { PageTitle } from './PageTitle';
type PageNavbarPreference = {
    type: 'showAlways';
} | {
    type: 'transparent';
} | {
    type: 'none';
};
interface Props {
    /**
     * @desciription
     * showAlways 는 항상 상단에 navbar 가 존재합니다.
     * transparent 는 투명내비바를 적용합니다.
     * none 은 헤더를 없앱니다.
     * 투명내비바를 쓰는 경우 PageNavbar.TransparentScrollView 를 사용해야합니다.
     *
     * @default type=showAlways
     */
    preference?: PageNavbarPreference;
    children?: ReactNode;
}
/**
 * @public
 * @category Components
 * @kind component
 * @name PageNavbar
 * @description
 * `PageNavbar` 컴포넌트를 사용해서 페이지 상단에 내비게이션 바의 스타일을 설정할 수 있어요. 내비게이션 바의 타입은 `showAlways`, `transparent`, `none` 중에서 선택할 수 있어요.
 *
 * - `showAlways`: 항상 상단에 내비게이션 바를 표시해요.
 * - `transparent`: 투명한 내비게이션 바를 사용해요. 투명 내비게이션 바를 사용할 때는 `PageNavbar.TransparentScrollView`와 함께 사용해야 해요.
 * - `none`: 옵션을 사용하면 내비게이션 바가 완전히 숨겨져 헤더가 없는 페이지를 만들 수 있어요. 예를 들어, Safe Area를 포함한 전체 화면을 만들고 싶을 때 사용해요.
 *
 *
 * @param {Props} props `PageNavbar`에 전달할 속성들이에요.
 * @param {PageNavbarPreference} [props.preference={type: 'showAlways'}] 내비게이션 바의 유형을 설정하는 옵션이에요. `showAlways`, `transparent`, `none` 중 하나를 선택할 수 있어요. 기본값은 `{ type: 'showAlways' }` 에요.
 * @param {ReactNode} [props.children] 내비게이션 바 안에 들어갈 콘텐츠를 정의할 수 있어요. 주로 타이틀과 액세서리 버튼을 포함해요.
 * `PageNavbar.AccessoryTextButton`과 `PageNavbar.AccessoryIconButton`을 사용해 내비게이션 바에 액세서리 버튼을 쉽게 추가할 수 있어요.
 *
 * @returns {JSX.Element} 설정된 내비게이션 바를 포함하는 JSX 엘리먼트를 반환해요.
 *
 * @example
 * ## `showAlways`를 사용해서 항상 상단에 내비게이션 바를 보여주는 예시

 * ```tsx
 * import { PageNavbar } from '@toss/tds-react-native';
 *
 * function Page() {
 *   return (
 *     <PageNavbar>
 *       <PageNavbar.Title>Title</PageNavbar.Title>
 *       <PageNavbar.AccessoryButtons>
 *         <PageNavbar.AccessoryTextButton>설정</PageNavbar.AccessoryTextButton>
 *         <PageNavbar.AccessoryIconButton name="icon-share-dots-mono" />
 *       </PageNavbar.AccessoryButtons>
 *     </PageNavbar>
 *   );
 * }
 * ```
 *
 * @example
 * ## `transparent`를 사용해서 투명한 내비게이션 바를 만드는 예시
 * ```tsx
 * import { PageNavbar } from '@toss/tds-react-native';
 *
 * function Page() {
 *   return (
 *     <PageNavbar preference={{ type: "transparent" }}>
 *       <PageNavbar.Title>Title</PageNavbar.Title>
 *       <PageNavbar.AccessoryButtons>
 *         <PageNavbar.AccessoryTextButton>설정</PageNavbar.AccessoryTextButton>
 *         <PageNavbar.AccessoryIconButton name="icon-share-dots-mono" />
 *       </PageNavbar.AccessoryButtons>
 *
 *       <PageNavbar.TransparentScrollView>
 *         <SomeScrollableContents />
 *       </PageNavbar.TransparentScrollView>
 *     </PageNavbar>
 *   );
 * }
 * ```
 *
 * @example
 * ## `none`을 사용해서 내비게이션 바를 숨기고 SerivceWebView를 전체화면으로 그리는 예시
 * ```tsx
 * import { PageNavbar } from '@toss/tds-react-native';
 *
 * function Page() {
 *   return (
 *     <>
 *       <PageNavbar preference={{ type: "none" }}>
 *       <ServiceWebView {...} />
 *     </>
 *   );
 * }
 * ```
 *
 * @see [React Navigation Header Options](https://reactnavigation.org/docs/stack-navigator/#headertransparent)
 */
export declare function PageNavbar({ children, preference }: Props): import("react/jsx-runtime").JSX.Element;
export declare namespace PageNavbar {
    var Title: typeof PageTitle;
    var AccessoryButtons: typeof PageAccessoryButtons;
    var AccessoryTextButton: typeof import("../../components/navbar/ReactNavigationHelper/Right").RightTextButton;
    var AccessoryIconButton: import("react").ForwardRefExoticComponent<Pick<import("../..").IconProps & import("react").RefAttributes<import("react-native").View>, "type" | "name"> & import("react-native").AccessibilityProps & import("react-native").TouchableOpacityProps & import("react").RefAttributes<import("react-native").TouchableOpacity>>;
    var TransparentScrollView: import("react").ForwardRefExoticComponent<Omit<import("@granite-js/react-native").IOComponentProps & import("react-native").ScrollViewProps & import("react").RefAttributes<import("react-native").ScrollView>, "ref"> & import("react").RefAttributes<import("react-native").ScrollView>>;
    var AccessoryButton: typeof CompatAccessoryButton;
}
export {};
