import type { View } from 'react-native';
export declare const useOnPressOutside: ({ ref, onPressOutside, interactive, }: {
    ref: React.RefObject<View>;
    onPressOutside?: () => void;
    interactive?: boolean;
}) => void;
