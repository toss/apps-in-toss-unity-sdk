import type { ReactNode } from 'react';
export interface AnimateRallyPresenceProps {
    children: ReactNode;
    preserveUntilCompleteAll?: boolean;
    onExitEnd?: () => void;
}
export declare function AnimateRallyPresence(props: AnimateRallyPresenceProps): import("react/jsx-runtime").JSX.Element;
