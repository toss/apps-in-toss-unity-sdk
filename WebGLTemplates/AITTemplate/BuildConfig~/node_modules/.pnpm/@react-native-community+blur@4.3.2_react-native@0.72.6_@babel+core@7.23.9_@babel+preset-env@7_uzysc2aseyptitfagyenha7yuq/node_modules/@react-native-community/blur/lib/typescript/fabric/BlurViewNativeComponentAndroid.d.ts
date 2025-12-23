import type { ViewProps, HostComponent, ColorValue } from 'react-native';
import type { WithDefault, Int32 } from 'react-native/Libraries/Types/CodegenTypes';
interface NativeProps extends ViewProps {
    blurAmount?: WithDefault<Int32, 10>;
    blurType?: WithDefault<'dark' | 'light' | 'xlight', 'dark'>;
    blurRadius?: Int32;
    downsampleFactor?: Int32;
    overlayColor?: ColorValue;
    enabled?: boolean;
    autoUpdate?: boolean;
}
declare const _default: HostComponent<NativeProps>;
export default _default;
