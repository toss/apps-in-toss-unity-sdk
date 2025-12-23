export interface PresenceContextProps {
    id: string;
    present: boolean;
    register: (id: string) => () => void;
    onExitComplete?: (id: string) => void;
}
export declare const PresenceContext: import("react").Context<PresenceContextProps | null>;
