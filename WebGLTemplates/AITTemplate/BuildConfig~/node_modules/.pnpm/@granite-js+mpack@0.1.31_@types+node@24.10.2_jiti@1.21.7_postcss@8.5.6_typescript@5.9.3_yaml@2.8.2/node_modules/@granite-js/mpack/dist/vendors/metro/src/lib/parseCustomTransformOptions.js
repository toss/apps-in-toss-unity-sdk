"use strict";
const nullthrows = require("nullthrows");
const PREFIX = "transform.";
module.exports = function parseCustomTransformOptions(urlObj) {
  const customTransformOptions = /* @__PURE__ */ Object.create(null);
  const query = nullthrows(urlObj.query);
  Object.keys(query).forEach((key) => {
    if (key.startsWith(PREFIX)) {
      customTransformOptions[key.substr(PREFIX.length)] = query[key];
    }
  });
  return customTransformOptions;
};
