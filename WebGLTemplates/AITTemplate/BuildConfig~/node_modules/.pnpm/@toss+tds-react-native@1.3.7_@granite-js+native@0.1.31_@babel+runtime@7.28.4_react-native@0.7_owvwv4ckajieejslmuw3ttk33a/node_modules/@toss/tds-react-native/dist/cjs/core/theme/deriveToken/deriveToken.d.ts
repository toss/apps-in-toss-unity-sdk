export declare const deriveToken: (seed: import("..").SeedToken) => {
    readonly button: import("../../../components/button/ButtonDerivedTokenGenerator").ButtonDerivedTheme;
    readonly bridge: import("../../../extensions/bridge/BridgeDerivedTokenGenerator").BridgeDerivedTheme;
};
export type DerivedToken = ReturnType<typeof deriveToken>;
