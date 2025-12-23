import type { WebViewProps } from '@granite-js/native/react-native-webview';
import { WebView } from '@granite-js/native/react-native-webview';
import type { Icon } from '../../components/icon';
type Icon = {
    source: {
        uri: string;
    };
    name?: never;
} | {
    name: string;
    source?: never;
};
type Header = {
    title?: string;
    icon?: Icon;
    withBackButton?: boolean;
};
export type ExternalWebViewScreenProps = WebViewProps & {
    header?: Header;
};
export declare const ExternalWebViewScreen: import("react").ForwardRefExoticComponent<import("react-native-webview/lib/WebViewTypes").IOSWebViewProps & import("react-native-webview/lib/WebViewTypes").AndroidWebViewProps & import("react-native-webview/lib/WebViewTypes").WindowsWebViewProps & {
    header?: Header;
} & import("react").RefAttributes<WebView<{}>>>;
export {};
