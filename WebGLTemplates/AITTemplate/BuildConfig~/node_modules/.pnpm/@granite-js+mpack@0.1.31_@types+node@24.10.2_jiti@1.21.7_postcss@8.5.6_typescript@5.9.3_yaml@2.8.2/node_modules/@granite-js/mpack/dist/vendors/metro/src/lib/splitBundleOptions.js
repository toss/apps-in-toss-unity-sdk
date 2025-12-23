"use strict";
function splitBundleOptions(options) {
  return {
    entryFile: options.entryFile,
    resolverOptions: {
      customResolverOptions: options.customResolverOptions
    },
    transformOptions: {
      customTransformOptions: options.customTransformOptions,
      dev: options.dev,
      hot: options.hot,
      minify: options.minify,
      platform: options.platform,
      runtimeBytecodeVersion: options.runtimeBytecodeVersion,
      type: "module",
      unstable_transformProfile: options.unstable_transformProfile
    },
    serializerOptions: {
      excludeSource: options.excludeSource,
      inlineSourceMap: options.inlineSourceMap,
      modulesOnly: options.modulesOnly,
      runModule: options.runModule,
      sourceMapUrl: options.sourceMapUrl,
      sourceUrl: options.sourceUrl
    },
    graphOptions: {
      shallow: options.shallow
    },
    onProgress: options.onProgress
  };
}
module.exports = splitBundleOptions;
