import { type ParamListBase } from '@granite-js/native/@react-navigation/native';
import { NativeStackNavigationOptions, NativeStackNavigationProp } from '@granite-js/native/@react-navigation/native-stack';
import type { StandardSchemaV1 } from '@standard-schema/spec';
import { defaultParserParams } from './utils/defaultParserParams';
import { type InferOutput, type InferInput } from './utils/standardSchema';
export interface RouteOptions<T extends Readonly<object | undefined>> {
    parserParams?: (params: Record<string, unknown>) => Record<string, unknown>;
    validateParams?: ((params: Readonly<object | undefined>) => T) | StandardSchemaV1<any, T>;
    component: React.FC<any>;
    screenOptions?: NativeStackNavigationOptions | ((context: {
        params: T;
    }) => NativeStackNavigationOptions);
}
export interface RegisterScreenInput {
}
export interface RegisterScreen {
}
export type NavigationProps = NativeStackNavigationProp<keyof RegisterScreenInput extends never ? ParamListBase : RegisterScreenInput>;
export declare function useNavigation(): NavigationProps;
export type RouteHooksOptions<TScreen extends keyof RegisterScreen> = {
    from: TScreen;
    strict?: true;
} | {
    strict: false;
    from?: never;
};
export declare const routeMap: Map<keyof RegisterScreenInput, {
    options: Omit<RouteOptions<any>, "component" | "screenOptions">;
    component: React.FC<any>;
    screenOptions?: NativeStackNavigationOptions | ((context: {
        params: any;
    }) => NativeStackNavigationOptions);
}>;
export declare function useMatchOptions<TScreen extends keyof RegisterScreen>(options: RouteHooksOptions<TScreen>): {
    parserParams: ((params: Record<string, unknown>) => Record<string, unknown>) | typeof defaultParserParams;
    validateParams?: StandardSchemaV1<any, any> | ((params: Readonly<object | undefined>) => any) | undefined;
} | null;
export declare function useParams<TScreen extends keyof RegisterScreen>(options: {
    from: TScreen;
    strict?: true;
}): RegisterScreen[TScreen];
export declare function useParams(options: {
    strict: false;
}): Readonly<object | undefined>;
export declare function createRoute<TSchema extends StandardSchemaV1<any, any>>(path: keyof RegisterScreenInput, options: Omit<RouteOptions<any>, 'validateParams'> & {
    validateParams: TSchema;
}): {
    _path: keyof RegisterScreenInput;
    useNavigation: typeof useNavigation;
    useParams: () => InferOutput<TSchema>;
    _inputType?: InferInput<TSchema>;
    _outputType?: InferOutput<TSchema>;
};
export declare function createRoute<T extends Readonly<object | undefined>>(path: keyof RegisterScreenInput, options: RouteOptions<T>): {
    _path: keyof RegisterScreenInput;
    useNavigation: typeof useNavigation;
    useParams: () => T;
    _inputType?: T;
    _outputType?: T;
};
