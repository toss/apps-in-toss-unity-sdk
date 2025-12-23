import { ReactNode } from 'react';
export declare function CanGoBackGuard({ children, canGoBack, onBack, setIosSwipeGestureEnabled, }: {
    canGoBack: boolean;
    children: ReactNode;
    onBack?: () => void;
    setIosSwipeGestureEnabled?: ({ isEnabled }: {
        isEnabled: boolean;
    }) => void;
}): import("react/jsx-runtime").JSX.Element;
