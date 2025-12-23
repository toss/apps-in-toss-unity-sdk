/// <reference types="react" />
import type { BubblingEventHandler, Int32, Double, Float } from 'react-native/Libraries/Types/CodegenTypes';
import { NativeComponentType } from 'react-native/Libraries/Utilities/codegenNativeComponent';
import type { ProcessedColorValue, ViewProps } from 'react-native';
export type OnAnimationFinishEvent = Readonly<{
    isCancelled: boolean;
}>;
export type AnimationFailureEvent = Readonly<{
    error: string;
}>;
type ColorFilterStruct = Readonly<{
    keypath: string;
    color: ProcessedColorValue;
}>;
type TextFilterIOSStruct = Readonly<{
    keypath: string;
    text: string;
}>;
type TextFilterAndroidStruct = Readonly<{
    find: string;
    replace: string;
}>;
export interface NativeProps extends ViewProps {
    resizeMode?: string;
    renderMode?: string;
    sourceName?: string;
    sourceJson?: string;
    sourceURL?: string;
    sourceDotLottieURI?: string;
    imageAssetsFolder?: string;
    progress?: Float;
    speed?: Double;
    loop?: boolean;
    autoPlay?: boolean;
    enableMergePathsAndroidForKitKatAndAbove?: boolean;
    hardwareAccelerationAndroid?: boolean;
    cacheComposition?: boolean;
    colorFilters?: ReadonlyArray<ColorFilterStruct>;
    dummy?: Readonly<{
        dummy: boolean;
    }>;
    textFiltersAndroid?: ReadonlyArray<TextFilterAndroidStruct>;
    textFiltersIOS?: ReadonlyArray<TextFilterIOSStruct>;
    onAnimationFinish?: BubblingEventHandler<OnAnimationFinishEvent, 'onAnimationFinish'>;
    onAnimationFailure?: BubblingEventHandler<AnimationFailureEvent, 'onAnimationFailure'>;
}
type LottieViewNativeComponentType = NativeComponentType<NativeProps>;
interface NativeCommands {
    play: (viewRef: React.ElementRef<LottieViewNativeComponentType>, startFrame: Int32, endFrame: Int32) => void;
    reset: (viewRef: React.ElementRef<LottieViewNativeComponentType>) => void;
    pause: (viewRef: React.ElementRef<LottieViewNativeComponentType>) => void;
    resume: (viewRef: React.ElementRef<LottieViewNativeComponentType>) => void;
}
export declare const Commands: NativeCommands;
declare const _default: NativeComponentType<NativeProps>;
export default _default;
//# sourceMappingURL=LottieAnimationViewNativeComponent.d.ts.map