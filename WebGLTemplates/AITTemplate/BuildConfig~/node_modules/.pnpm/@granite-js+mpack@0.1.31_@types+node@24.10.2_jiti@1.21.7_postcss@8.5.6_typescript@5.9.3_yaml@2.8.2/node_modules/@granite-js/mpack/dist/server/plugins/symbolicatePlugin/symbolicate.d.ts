import { StackFrame } from './parseStackFrame';
interface CodeFrame {
    content: string;
    location: {
        column: number;
        row: number;
    };
    fileName: string;
}
/**
 * React Native 클라이언트로부터 stack frame 을 전달받아 원본 코드와 매핑한 데이터로 반환
 *
 * @see {@link https://github.com/facebook/react-native/blob/v0.72.6/packages/react-native/Libraries/Core/Devtools/symbolicateStackTrace.js#L32-L47}
 * @see {@link https://github.com/facebook/react-native/blob/v0.72.6/packages/react-native/Libraries/LogBox/Data/LogBoxSymbolication.js#L24-L25}
 */
export declare function symbolicate(rawSourcemap: Uint8Array, stackFrame: StackFrame[]): Promise<{
    stack: {
        column: number;
        lineNumber: number;
        file: string;
        methodName: string | undefined;
    }[];
    codeFrame: CodeFrame | null;
}>;
export {};
