import { GraniteEvent, GraniteEventDefinition } from '@granite-js/react-native';
type NavigationAccessoryEventResponse = {
    id: string;
};
declare class NavigationAccessoryEvent extends GraniteEventDefinition<undefined, NavigationAccessoryEventResponse> {
    name: "navigationAccessoryEvent";
    remove(): void;
    listener(options: undefined, onEvent: (response: NavigationAccessoryEventResponse) => void): void;
}
export declare const tdsEvent: GraniteEvent<NavigationAccessoryEvent>;
export {};
