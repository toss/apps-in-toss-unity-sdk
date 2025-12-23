type CommonShadowProps = {
    opacity?: number;
    offset?: {
        x?: number;
        y?: number;
    };
    radius: number;
};
export type TokenShadowProps = {
    lightColor: string;
    darkColor: string;
} & CommonShadowProps;
type CustomShadowProps = {
    color: string;
} & CommonShadowProps;
export type Shadow = TokenShadowProps | CustomShadowProps;
export {};
