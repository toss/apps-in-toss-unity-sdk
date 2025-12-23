import { GraniteEvent, GraniteEventDefinition } from './abstract';
import { BackEventControls } from '../use-back-event/useBackEvent';
declare class BackEvent extends GraniteEventDefinition<void, void> {
    private backEventControls;
    name: "backEvent";
    ref: {
        remove: () => void;
    };
    constructor(backEventControls: BackEventControls);
    remove(): void;
    listener(_: void, onEvent: (response: void) => void): void;
}
export declare function useGraniteEvent(): GraniteEvent<BackEvent>;
export {};
