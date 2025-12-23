import * as react from 'react';
import { ComponentType, ComponentProps, ForwardRefExoticComponent, PropsWithoutRef, RefAttributes, ComponentRef, ReactElement, PropsWithChildren } from 'react';
import * as react_jsx_runtime from 'react/jsx-runtime';
import { View } from 'react-native';
import { ImpressionArea } from '@granite-js/react-native';

interface LogPayload {
    log_type: string;
    log_name: string;
    params: LoggerParams;
}
type SerializedLoggerParams = string | number | boolean | null | undefined;
interface LoggerParams {
    [key: string]: SerializedLoggerParams;
}
interface EventLoggerParams {
    log_name?: string;
    event_type: 'impression' | 'click';
    text?: string;
    left_text?: string;
    right_text?: string;
    parent?: Parent;
}
type EventTypeExcludedParams = Omit<EventLoggerParams, 'event_type'> & LoggerParams;
type Parent = string & {
    __type: 'parent';
};
interface LoggerProps {
    loggerParams?: LoggerParams;
    log?: boolean;
    stopPropagation?: boolean;
    preventDefault?: boolean;
}
type ComponentWithLogging<C extends ComponentType<any>, AdditionalProps = object, SkippedStatics extends keyof C = never, Props = ComponentProps<C> & LoggerProps & AdditionalProps> = (C extends ForwardRefExoticComponent<any> ? ForwardRefExoticComponent<PropsWithoutRef<Props> & RefAttributes<ComponentRef<C>>> : ComponentType<Props>) & NonReactStatics<C, {
    [key in SkippedStatics]: true;
}>;
type NonReactStatics<S extends ComponentType<any>, C extends Record<string, true> = Record<string, true>> = {
    [key in Exclude<keyof S, 'childContextTypes' | 'contextType' | 'contextTypes' | 'defaultProps' | 'displayName' | 'getDefaultProps' | 'getDerivedStateFromError' | 'getDerivedStateFromProps' | 'mixins' | 'propTypes' | 'type' | 'name' | 'length' | 'prototype' | 'caller' | 'callee' | 'arguments' | 'arity' | keyof C>]: S[key];
};

interface LoggingAreaProps extends ComponentProps<typeof View> {
    children: ReactElement;
    params?: EventTypeExcludedParams & LoggerParams;
}
/**
 * @public
 * @category 분석
 * @name LoggingArea
 * @description 여러 컴포넌트의 텍스트를 하나로 묶어서 로그를 남길 수 있어요. 지정한 영역이 노출되거나 클릭 했을 때 로그를 수집할 수 있어요.
 * @example
 * ### 여러 컴포넌트를 하나의 영역으로 묶어서 분석하는 예시
 *
 * ```tsx
 * import { Analytics } from '@apps-in-toss/framework';
 * import { View, Text } from 'react-native';
 *
 *
 * // 영역 안의 노출이나 클릭 정보를 자동으로 수집해요.
 * function TrackElements() {
 *   return (
 *     <Analytics.Area>
 *       <View>
 *         <Text>Hello</Text>
 *         <Text>World!</Text>
 *       </View>
 *     </Analytics.Area>
 *   );
 * }
 * ```
 */
declare function LoggingArea({ children, params: _params, ...props }: LoggingAreaProps): react_jsx_runtime.JSX.Element;

type ImpressionType = 'always' | 'with-fallback' | 'on-mount' | 'never';
type ImpressionProps = ComponentProps<typeof ImpressionArea>;
type LoggingImpressionProps = ImpressionProps & {
    params: (EventTypeExcludedParams & LoggerParams) | (() => EventTypeExcludedParams & LoggerParams);
    enabled?: boolean;
    withRef?: boolean;
    impression?: ImpressionType;
};
/**
 * @public
 * @category 분석
 * @name LoggingImpression
 * @description 요소가 뷰포트에 표시되었는지 판단하고 로그를 남기는 컴포넌트예요. 예를 들어, 스크롤 아래에 있는 요소가 뷰포트에 표시되었을 때를 감지해 로그를 남겨요.
 * @example
 * ### 컴포넌트의 노출 정보를 자동으로 수집하는 예시
 *
 * ```tsx
 * import { Analytics } from '@apps-in-toss/framework';
 *
 * // 영역 안의 노출 정보를 자동으로 수집해요.
 * function TrackElements() {
 *   return (
 *     <Analytics.Impression>
 *       <Text>Hello</Text>
 *     </Analytics.Impression>
 *   );
 * }
 * ```
 */
declare function LoggingImpression({ enabled, impression: impressionType, ...props }: LoggingImpressionProps): react_jsx_runtime.JSX.Element;

interface LoggingScreenProps {
    params?: LoggerParams;
    log?: boolean;
}
/**
 * @category 분석
 * @name LoggingScreen
 * @description 화면 진입 시점의 분석 정보를 자동으로 로그를 남기는 컴포넌트예요.
 * @example
 * ### 화면 진입 시점의 분석 정보를 자동으로 로그를 남기는 예시
 *
 * ```tsx
 * import { Analytics } from '@apps-in-toss/framework';
 * import { Text } from 'react-native';
 *
 * // 화면 진입 시점의 분석 정보를 자동으로 기록해요.
 * function Page() {
 *   return (
 *     <Analytics.Screen>
 *       <Text>Hello, world!</Text>
 *     </Analytics.Screen>
 *   );
 * }
 * ```
 */
declare function LoggingScreen({ params: _params, ...props }: PropsWithChildren<LoggingScreenProps>): react_jsx_runtime.JSX.Element;
declare namespace LoggingScreen {
    var onMount: ({ children, groupId, params, log, }: PropsWithChildren<LoggingScreenProps & {
        groupId: string;
    }>) => react_jsx_runtime.JSX.Element;
}

interface LoggerConfig {
    debug?: boolean;
    logger: LoggerImpl;
}
type LoggerImpl = (params: {
    log_name: string;
    log_type: string;
    params: Record<string, string | number | boolean | null | undefined | symbol>;
}) => void;

/**
 * @category 분석
 * @name AnalyticsConfig
 * @description 분석 기능을 사용하기 위한 설정 객체예요. `init` 함수에 전달해서 분석 로그의 출력 방식과 디버그 모드 설정을 지정할 수 있어요.
 * @property {LoggerConfig['logger']} logger 분석 로그가 생성될 때 호출되는 함수예요. 예를 들어 로그를 서버로 전송하거나 콘솔에 출력할 수 있어요.
 * @property {LoggerConfig['debug']} [debug] 개발 환경에서 로그를 출력할지 여부를 설정해요. 기본값은 `true` 값이에요.
 */
interface AnalyticsConfig {
    logger: LoggerConfig['logger'];
    debug?: LoggerConfig['debug'];
}
/**
 * @public
 * @category 분석
 * @name init
 * @description 분석 기능을 시작할 때 설정을 적용하는 함수예요. 분석 기능을 사용하기 전에 반드시 호출해야 해요.
 * @param {AnalyticsConfig} options 분석 기능을 사용하기 위한 설정 객체예요.
 */
declare function init(options: AnalyticsConfig): void;
/**
 * @public
 * @category 분석
 * @name Analytics
 * @description 사용자의 행동을 기록하고 분석하기 위한 객체예요. 이 객체를 사용해서 화면 진입, 버튼 클릭, 컴포넌트 노출 등의 이벤트를 자동으로 수집하고 기록할 수 있어요. 서비스 개선이나 사용자 흐름 분석에 필요한 데이터를 수집할 때 사용해요.
 * @property {typeof init} [init] 분석 기능을 시작할 때 설정을 적용하는 함수예요. 앱이 초기화 시점에 한 번 호출해서 설정을 적용해야 해요. 자세한 내용은 [init](/react-native/reference/framework/분석/init.html)를 참고하세요.
 * @property {typeof LoggingPress} [Press] 버튼이나 터치 가능한 컴포넌트의 클릭 이벤트 로그를 남기는 컴포넌트예요. 자세한 내용은 [LoggingPress](/react-native/reference/framework/분석/LoggingPress.html)를 참고하세요.
 * @property {typeof LoggingImpression} [Impression] 사용자가 화면에 특정 컴포넌트를 실제로 보았는지를 판단하고 로그를 남기는 컴포넌트예요. 자세한 내용은 [LoggingImpression](/react-native/reference/분석/LoggingImpression.html)를 참고하세요.
 * @property {typeof LoggingArea} [Area] 여러 컴포넌트를 하나의 영역으로 묶어서 분석할 수 있는 컴포넌트예요. 자세한 내용은 [LoggingArea](/react-native/reference/framework/분석/LoggingArea.html)를 참고하세요.
 */
declare const Analytics: {
    readonly init: typeof init;
    readonly Screen: typeof LoggingScreen;
    readonly Press: react.ForwardRefExoticComponent<LoggingPressProps & react.RefAttributes<unknown>>;
    readonly Impression: typeof LoggingImpression;
    readonly Area: typeof LoggingArea;
};

interface LoggingPressProps {
    children: ReactElement;
    params: (EventTypeExcludedParams & LoggerParams) | (() => EventTypeExcludedParams & LoggerParams);
    enabled?: boolean;
}

interface WithLoggingScreenOptions {
    augmentIntersectionObserver?: boolean;
}
declare function withLoggingScreen<C extends ComponentType<any>, Props = ComponentProps<C>>(Component: C, options?: WithLoggingScreenOptions): ComponentWithLogging<C, {
    groupId?: string;
    hasIOScrollView?: boolean;
}>;

export { Analytics, type AnalyticsConfig, type ComponentWithLogging, type EventLoggerParams, type EventTypeExcludedParams, type ImpressionProps, type ImpressionType, type LogPayload, type LoggerParams, type LoggerProps, type LoggingAreaProps, type LoggingImpressionProps, type LoggingPressProps, type LoggingScreenProps, type NonReactStatics, type Parent, type SerializedLoggerParams, withLoggingScreen };
