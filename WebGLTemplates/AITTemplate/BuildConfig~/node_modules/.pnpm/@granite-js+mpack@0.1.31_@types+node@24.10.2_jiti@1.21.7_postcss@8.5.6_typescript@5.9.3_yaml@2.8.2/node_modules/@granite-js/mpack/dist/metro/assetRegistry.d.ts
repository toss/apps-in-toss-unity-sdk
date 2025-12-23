/**
 * @see https://github.com/facebook/react-native/blob/v0.72.6/packages/assets/registry.js#L13-L24
 */
interface Asset {
    httpServerLocation: string;
    hash: string;
    name: string;
    type: string;
}
declare function registerAsset(asset: Asset): void;
declare function noop(): void;
export { registerAsset, noop as getAssetByID };
