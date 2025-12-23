"use strict";
const nullthrows = require("nullthrows");
const PREFIX = "resolver.";
module.exports = function parseCustomResolverOptions(urlObj) {
  const customResolverOptions = /* @__PURE__ */ Object.create(null);
  const query = nullthrows(urlObj.query);
  Object.keys(query).forEach((key) => {
    if (key.startsWith(PREFIX)) {
      customResolverOptions[key.substr(PREFIX.length)] = query[key];
    }
  });
  return customResolverOptions;
};
