import type { ComponentProps } from 'react';
import type { TopNavigation } from './TopNavigation';
type AddAccessoryButtonParams = ComponentProps<typeof TopNavigation>['fixedRightButton'];
type AddAccessoryButton = (params: AddAccessoryButtonParams) => void;
type RemoveAccessoryButton = () => void;
export declare function useTopNavigation(): {
    addAccessoryButton: AddAccessoryButton;
    removeAccessoryButton: RemoveAccessoryButton;
};
export {};
