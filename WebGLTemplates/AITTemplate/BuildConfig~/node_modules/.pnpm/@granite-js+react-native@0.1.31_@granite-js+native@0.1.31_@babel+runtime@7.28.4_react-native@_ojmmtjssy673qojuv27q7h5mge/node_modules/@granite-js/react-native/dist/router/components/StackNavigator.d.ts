export declare const StackNavigator: {
    Navigator: import("react").ComponentType<Omit<import("@react-navigation/routers").DefaultRouterOptions<string> & {
        id?: string;
        children: React.ReactNode;
        screenListeners?: Partial<{
            transitionStart: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "transitionStart">;
            transitionEnd: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "transitionEnd">;
            focus: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "focus">;
            blur: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "blur">;
            state: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "state">;
            beforeRemove: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "beforeRemove">;
        }> | ((props: {
            route: import("@react-navigation/core").RouteProp<import("@react-navigation/routers").ParamListBase, string>;
            navigation: any;
        }) => Partial<{
            transitionStart: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "transitionStart">;
            transitionEnd: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "transitionEnd">;
            focus: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "focus">;
            blur: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "blur">;
            state: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "state">;
            beforeRemove: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "beforeRemove">;
        }>) | undefined;
        screenOptions?: import("@react-navigation/native-stack").NativeStackNavigationOptions | ((props: {
            route: import("@react-navigation/core").RouteProp<import("@react-navigation/routers").ParamListBase, string>;
            navigation: any;
        }) => import("@react-navigation/native-stack").NativeStackNavigationOptions) | undefined;
    } & import("@react-navigation/routers").StackRouterOptions, "children" | "id" | "initialRouteName" | "screenListeners" | "screenOptions"> & import("@react-navigation/routers").DefaultRouterOptions<string> & {
        id?: string;
        children: React.ReactNode;
        screenListeners?: Partial<{
            transitionStart: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "transitionStart">;
            transitionEnd: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "transitionEnd">;
            focus: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "focus">;
            blur: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "blur">;
            state: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "state">;
            beforeRemove: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "beforeRemove">;
        }> | ((props: {
            route: import("@react-navigation/core").RouteProp<import("@react-navigation/routers").ParamListBase, string>;
            navigation: any;
        }) => Partial<{
            transitionStart: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "transitionStart">;
            transitionEnd: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "transitionEnd">;
            focus: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "focus">;
            blur: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "blur">;
            state: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "state">;
            beforeRemove: import("@react-navigation/core").EventListenerCallback<import("@react-navigation/native-stack").NativeStackNavigationEventMap & import("@react-navigation/core").EventMapCore<import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>>, "beforeRemove">;
        }>) | undefined;
        screenOptions?: import("@react-navigation/native-stack").NativeStackNavigationOptions | ((props: {
            route: import("@react-navigation/core").RouteProp<import("@react-navigation/routers").ParamListBase, string>;
            navigation: any;
        }) => import("@react-navigation/native-stack").NativeStackNavigationOptions) | undefined;
    }>;
    Screen: <RouteName extends string>(_: import("@react-navigation/core").RouteConfig<import("@react-navigation/routers").ParamListBase, RouteName, import("@react-navigation/routers").StackNavigationState<import("@react-navigation/routers").ParamListBase>, import("@react-navigation/native-stack").NativeStackNavigationOptions, import("@react-navigation/native-stack").NativeStackNavigationEventMap>) => null;
};
