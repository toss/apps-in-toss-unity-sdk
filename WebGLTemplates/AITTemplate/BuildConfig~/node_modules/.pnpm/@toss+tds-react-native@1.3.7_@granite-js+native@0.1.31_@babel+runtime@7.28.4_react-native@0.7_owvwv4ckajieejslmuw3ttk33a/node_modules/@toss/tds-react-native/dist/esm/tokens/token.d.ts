/**
 * NOTE: 웹 토큰 통합 전 임시 매핑이에요. 추후 var 연동 예정.
 */
declare const tokenMap: {
    readonly 'line-height-xs': 1.252;
    readonly 'line-height-s': 1.35;
    readonly 'line-height-m': 1.5;
};
export type TokenName = keyof typeof tokenMap;
export declare function token(name: TokenName): number;
export {};
