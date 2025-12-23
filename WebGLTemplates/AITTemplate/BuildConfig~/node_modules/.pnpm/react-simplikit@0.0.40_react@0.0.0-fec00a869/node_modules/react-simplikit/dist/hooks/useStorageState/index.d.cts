import { SetStateAction } from 'react';

type Storage = {
    get(key: string): string | null;
    set(key: string, value: string): void;
    remove(key: string): void;
    clear(): void;
};

type ToObject<T> = T extends unknown[] | Record<string, unknown> ? T : never;
type Serializable<T> = T extends string | number | boolean ? T : ToObject<T>;
type StorageStateOptions<T> = {
    storage?: Storage;
    defaultValue?: T;
};
type StorageStateOptionsWithDefaultValue<T> = StorageStateOptions<T> & {
    defaultValue: T;
};
type StorageStateOptionsWithSerializer<T> = StorageStateOptions<T> & {
    serializer: (value: Serializable<T>) => string;
    deserializer: (value: string) => Serializable<T>;
};
type SerializableGuard<T extends readonly any[]> = T[0] extends any ? T : T[0] extends never ? 'Received a non-serializable value' : T;
/**
 * @description
 * `useStorageState` is a React that functions like `useState` but persists the state value in browser storage.
 * The value is retained across page reloads and can be shared between tabs when using `localStorage`.
 *
 * @param {string} key - The key used to store the value in storage.
 * @param {Object} [options] - Configuration options for storage behavior.
 * @param {Storage} [options.storage=localStorage] - The storage type (`localStorage` or `sessionStorage`). Defaults to `localStorage`.
 * @param {T} [options.defaultValue] - The initial value if no existing value is found.
 * @param {Function} [options.serializer] - A function to serialize the state value to a string.
 * @param {Function} [options.deserializer] - A function to deserialize the state value from a string.
 *
 * @returns {readonly [state: Serializable<T> | undefined, setState: (value: SetStateAction<Serializable<T> | undefined>) => void, refreshState: () => void]} A tuple:
 * - state `Serializable<T> | undefined` - The current state value retrieved from storage;
 * - setState `(value: SetStateAction<Serializable<T> | undefined>) => void` - A function to update and persist the state;
 * - refreshState `() => void` - A function to refresh the state from storage;
 * @example
 * // Counter with persistent state
 * import { useStorageState } from 'react-simplikit';
 *
 * function Counter() {
 *   const [count, setCount] = useStorageState<number>('counter', {
 *     defaultValue: 0,
 *   });
 *
 *   return <button onClick={() => setCount(prev => prev + 1)}>Count: {count}</button>;
 * }
 */
declare function useStorageState<T>(key: string): SerializableGuard<readonly [Serializable<T> | undefined, (value: SetStateAction<Serializable<T> | undefined>) => void, () => void]>;
declare function useStorageState<T>(key: string, options: StorageStateOptionsWithDefaultValue<T>): SerializableGuard<readonly [Serializable<T>, (value: SetStateAction<Serializable<T>>) => void, () => void]>;
declare function useStorageState<T>(key: string, options: StorageStateOptions<T>): SerializableGuard<readonly [Serializable<T> | undefined, (value: SetStateAction<Serializable<T> | undefined>) => void, () => void]>;
declare function useStorageState<T>(key: string, options: StorageStateOptionsWithSerializer<T>): SerializableGuard<readonly [Serializable<T> | undefined, (value: SetStateAction<Serializable<T> | undefined>) => void, () => void]>;

export { type Serializable, useStorageState };
