type SafeToRemove = () => void;
type UsePresenceParameter = {
    presence: boolean;
};
type UsePresence = {
    present: boolean;
    safeToRemove?: SafeToRemove | null;
};
export declare function usePresence({ presence }: UsePresenceParameter): UsePresence;
export {};
