export declare abstract class GraniteEventDefinition<O = undefined, R = void> {
    abstract name: string;
    /**
     * Method to implement event logic.
     * @param options - Event options (undefined if not used)
     * @param onEvent - Callback called when the event is triggered.
     * @param onError - Callback called on error (error type: unknown)
     */
    abstract listener(options: O, onEvent: (response: R) => void, onError: (error: unknown) => void): void;
    /**
     * Additional logic to execute when removing the event listener.
     * If needed in the plugin, override this method and it will be called
     * when the listener is removed.
     */
    abstract remove(): void;
}
/**
 * GraniteEvent class registers event definition instances passed to the constructor.
 * The addEventListener method only allows registered event names, and the types of passed
 * options/callbacks are automatically inferred.
 *
 * Each subscription is uniquely identified by (event name + options).
 */
export declare class GraniteEvent<Defs extends GraniteEventDefinition<unknown, unknown>> {
    private definitions;
    private subscriptionGroups;
    constructor(defs: Defs[]);
    addEventListener<E extends Defs['name']>(eventName: E, listener: Extract<Defs, {
        name: E;
    }> extends GraniteEventDefinition<infer O, infer R> ? [O] extends [undefined] ? {
        onEvent: (response: R) => void;
        onError?: (error: unknown) => void;
    } : {
        options: O;
        onEvent: (response: R) => void;
        onError?: (error: unknown) => void;
    } : never): () => void;
    emit<E extends Defs['name']>(eventName: E, data: Extract<Defs, {
        name: E;
    }> extends GraniteEventDefinition<unknown, infer R> ? R : never): void;
    private _getKey;
}
