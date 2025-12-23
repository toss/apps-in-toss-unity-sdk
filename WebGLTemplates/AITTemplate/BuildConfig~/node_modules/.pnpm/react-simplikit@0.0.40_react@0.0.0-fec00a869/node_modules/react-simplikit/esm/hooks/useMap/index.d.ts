/**
 * Defines the type for either a Map or an array of key-value pairs.
 */
type MapOrEntries<K, V> = Map<K, V> | [K, V][];
/**
 * Actions to manipulate the Map state.
 */
type MapActions<K, V> = {
    /** Sets a key-value pair in the map. */
    set: (key: K, value: V) => void;
    /** Sets multiple key-value pairs in the map at once. */
    setAll: (entries: MapOrEntries<K, V>) => void;
    /** Removes a key from the map. */
    remove: (key: K) => void;
    /** Resets the map to its initial state. */
    reset: () => void;
};
/**
 * Return type of the useMap hook.
 * Hides certain methods to prevent direct mutations.
 */
type UseMapReturn<K, V> = [Omit<Map<K, V>, 'set' | 'clear' | 'delete'>, MapActions<K, V>];
/**
 * @description
 * A React hook that manages a key-value Map as state.
 * Provides efficient state management and stable action functions.
 *
 * @param {MapOrEntries<K, V>} initialState - Initial Map state (Map object or array of key-value pairs)
 * @returns {UseMapReturn<K, V>} A tuple containing the Map state and actions to manipulate it
 *
 * @example
 * ```tsx
 * const [userMap, actions] = useMap<string, User>([
 *   ['user1', { name: 'John', age: 30 }]
 * ]);
 *
 * // Using values from the Map
 * const user1 = userMap.get('user1');
 *
 * // Updating the Map
 * actions.set('user2', { name: 'Jane', age: 25 });
 * ```
 */
declare function useMap<K, V>(initialState?: MapOrEntries<K, V>): UseMapReturn<K, V>;

export { useMap };
