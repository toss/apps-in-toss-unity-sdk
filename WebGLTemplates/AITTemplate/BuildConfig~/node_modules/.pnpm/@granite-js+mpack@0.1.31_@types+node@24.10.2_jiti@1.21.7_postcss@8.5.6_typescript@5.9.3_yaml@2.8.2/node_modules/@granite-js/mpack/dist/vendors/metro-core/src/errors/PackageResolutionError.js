"use strict";
const { formatFileCandidates } = require("metro-resolver");
class PackageResolutionError extends Error {
  originModulePath;
  packageError;
  targetModuleName;
  constructor(opts) {
    const perr = opts.packageError;
    super(
      `While trying to resolve module \`${opts.targetModuleName}\` from file \`${opts.originModulePath}\`, the package \`${perr.packageJsonPath}\` was successfully found. However, this package itself specifies a \`main\` module field that could not be resolved (\`${perr.mainPrefixPath}\`. Indeed, none of these files exist:

  * ${formatFileCandidates(perr.fileCandidates)}
  * ${formatFileCandidates(perr.indexCandidates)}`
    );
    Object.assign(this, opts);
  }
}
module.exports = PackageResolutionError;
