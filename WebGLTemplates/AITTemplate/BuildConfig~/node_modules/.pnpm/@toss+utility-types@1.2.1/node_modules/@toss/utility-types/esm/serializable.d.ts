/** @tossdocs-ignore */
declare type Primitive = undefined | null | boolean | number | symbol | string;
export declare type Serializable = Primitive | Date | {
    toJSON: () => string;
} | readonly Serializable[] | Readonly<{
    [key: string]: Serializable;
}>;
export {};
