import type { ReactNode } from 'react';
interface PresenceChildProps {
    children: ReactNode;
    present: boolean;
    preserveUntilCompleteAll?: boolean;
    onExitEnd?: () => void;
}
export declare function PresenceChild(props: PresenceChildProps): import("react/jsx-runtime").JSX.Element;
export {};
