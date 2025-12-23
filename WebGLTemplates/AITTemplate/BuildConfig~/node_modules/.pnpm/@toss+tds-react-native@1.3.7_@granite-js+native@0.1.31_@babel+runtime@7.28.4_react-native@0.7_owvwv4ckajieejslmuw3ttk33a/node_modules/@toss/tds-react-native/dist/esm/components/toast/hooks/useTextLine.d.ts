type TextLine = 'single' | 'multiple';
export declare const useTextLine: () => {
    textLine: TextLine;
    updateTextLine: (event: import("react-native").NativeSyntheticEvent<import("react-native").TextLayoutEventData>) => void;
};
export {};
