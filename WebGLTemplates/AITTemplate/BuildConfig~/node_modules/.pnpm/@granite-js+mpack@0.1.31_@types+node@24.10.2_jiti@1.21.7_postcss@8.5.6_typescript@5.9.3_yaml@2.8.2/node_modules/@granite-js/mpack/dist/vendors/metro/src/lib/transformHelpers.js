"use strict";
var import_contextModuleTemplates = require("./contextModuleTemplates");
const path = require("path");
const baseIgnoredInlineRequires = ["React", "react", "react-native"];
async function calcTransformerOptions(entryFiles, bundler, deltaBundler, config, options, resolverOptions) {
  const baseOptions = {
    customTransformOptions: options.customTransformOptions,
    dev: options.dev,
    hot: options.hot,
    inlineRequires: false,
    inlinePlatform: true,
    minify: options.minify,
    platform: options.platform,
    runtimeBytecodeVersion: options.runtimeBytecodeVersion,
    unstable_transformProfile: options.unstable_transformProfile
  };
  if (options.type === "script") {
    return {
      ...baseOptions,
      type: "script"
    };
  }
  const getDependencies = async (path2) => {
    const dependencies = await deltaBundler.getDependencies([path2], {
      resolve: await getResolveDependencyFn(bundler, options.platform, resolverOptions),
      transform: await getTransformFn(
        [path2],
        bundler,
        deltaBundler,
        config,
        {
          ...options,
          minify: false
        },
        resolverOptions
      ),
      transformOptions: options,
      onProgress: null,
      experimentalImportBundleSupport: config.transformer.experimentalImportBundleSupport,
      unstable_allowRequireContext: config.transformer.unstable_allowRequireContext,
      shallow: false
    });
    return Array.from(dependencies.keys());
  };
  const { transform } = await config.transformer.getTransformOptions(
    entryFiles,
    { dev: options.dev, hot: options.hot, platform: options.platform },
    getDependencies
  );
  return {
    ...baseOptions,
    inlineRequires: transform.inlineRequires || false,
    experimentalImportSupport: transform.experimentalImportSupport || false,
    unstable_disableES6Transforms: transform.unstable_disableES6Transforms || false,
    nonInlinedRequires: transform.nonInlinedRequires || baseIgnoredInlineRequires,
    type: "module"
  };
}
function removeInlineRequiresBlockListFromOptions(path2, inlineRequires) {
  if (typeof inlineRequires === "object") {
    return !(path2 in inlineRequires.blockList);
  }
  return inlineRequires;
}
async function getTransformFn(entryFiles, bundler, deltaBundler, config, options, resolverOptions) {
  const { inlineRequires, ...transformOptions } = await calcTransformerOptions(
    entryFiles,
    bundler,
    deltaBundler,
    config,
    options,
    resolverOptions
  );
  return async (modulePath, requireContext) => {
    let templateBuffer;
    if (requireContext) {
      const graph = await bundler.getDependencyGraph();
      const files = graph.matchFilesWithContext(requireContext.from, {
        filter: requireContext.filter,
        recursive: requireContext.recursive
      });
      const template = (0, import_contextModuleTemplates.getContextModuleTemplate)(requireContext.mode, requireContext.from, files);
      templateBuffer = Buffer.from(template);
    }
    return await bundler.transformFile(
      modulePath,
      {
        ...transformOptions,
        type: getType(transformOptions.type, modulePath, config.resolver.assetExts),
        inlineRequires: removeInlineRequiresBlockListFromOptions(modulePath, inlineRequires)
      },
      templateBuffer
    );
  };
}
function getType(type, filePath, assetExts) {
  if (type === "script") {
    return type;
  }
  if (assetExts.indexOf(path.extname(filePath).slice(1)) !== -1) {
    return "asset";
  }
  return "module";
}
async function getResolveDependencyFn(bundler, platform, resolverOptions) {
  const dependencyGraph = await await bundler.getDependencyGraph();
  return (from, to) => dependencyGraph.resolveDependency(from, to, platform ?? null, resolverOptions);
}
module.exports = {
  getTransformFn,
  getResolveDependencyFn
};
