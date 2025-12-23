import { GranitePluginCore } from "@granite-js/plugin-core";
import { SentryCliOptions } from "@sentry/cli";

//#region src/types.d.ts
type SentryPluginOptions = WithSentryCliOptions | WithoutSentryCliOptions;
interface WithSentryCliOptions extends SentryCliOptions, BaseSentryPluginOptions {
  useClient?: true;
}
interface WithoutSentryCliOptions extends BaseSentryPluginOptions {
  useClient: false;
}
interface BaseSentryPluginOptions {
  /**
   * @default true
   */
  enabled?: boolean;
}

//#endregion
//#region src/sentryPlugin.d.ts
declare const sentryPlugin: ({
  enabled,
  ...options
}?: SentryPluginOptions) => GranitePluginCore;

//#endregion
export { SentryPluginOptions, sentryPlugin as sentry };