import type { WebViewProps } from '@granite-js/native/react-native-webview';
import { WebView } from '@granite-js/native/react-native-webview';
import type { TopNavigationProps } from '../top-navigation/TopNavigation';
export type PartnerWebViewScreenProps = WebViewProps & {
    header: TopNavigationProps & {
        withBackButton?: boolean;
    };
};
export declare const PartnerWebViewScreen: import("react").ForwardRefExoticComponent<import("react-native-webview/lib/WebViewTypes").IOSWebViewProps & import("react-native-webview/lib/WebViewTypes").AndroidWebViewProps & import("react-native-webview/lib/WebViewTypes").WindowsWebViewProps & {
    header: TopNavigationProps & {
        withBackButton?: boolean;
    };
} & import("react").RefAttributes<WebView<{}>>>;
