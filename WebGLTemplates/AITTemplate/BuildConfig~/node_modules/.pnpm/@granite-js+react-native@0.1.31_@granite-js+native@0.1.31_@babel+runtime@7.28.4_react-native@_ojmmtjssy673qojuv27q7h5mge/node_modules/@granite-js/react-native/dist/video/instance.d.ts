import type { VideoProperties } from '@granite-js/native/react-native-video';
import { type ComponentType } from 'react';
export type VideoNativeProps = Omit<VideoProperties, 'onAudioFocusChanged'> & {
    onAudioFocusChanged?: (event: {
        hasAudioFocus: boolean;
    }) => void;
};
export declare const Component: ComponentType<VideoNativeProps>;
export declare const isAvailable: boolean;
