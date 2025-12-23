import type { Layout } from '../types';
type AddLayoutPaddingProps = {
    layout: Layout;
    padding: {
        paddingTop: number;
        paddingRight: number;
        paddingBottom: number;
        paddingLeft: number;
    };
};
export declare const addLayoutPadding: ({ layout, padding }: AddLayoutPaddingProps) => {
    x: number;
    y: number;
    width: number;
    height: number;
};
export {};
