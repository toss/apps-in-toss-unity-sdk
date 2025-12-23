import type { ReactNode } from 'react';
export interface SafeAreaProviderProps {
    children: ReactNode;
}
/**
 * 애플리케이션의 상단에 SafeAreaProvider가 있는지 확인하고,
 * SafeAreaProvider가 없으면 자동으로 SafeAreaProvider를 감싸는 Provider 컴포넌트예요.
 * 이를 통해 SafeAreaContext가 항상 안전하게 제공되도록 보장해요.
 */
export declare function SafeAreaProvider({ children }: SafeAreaProviderProps): import("react/jsx-runtime").JSX.Element;
