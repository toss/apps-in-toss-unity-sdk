"use strict";Object.defineProperty(exports,"__esModule",{value:!0}),exports.mergeRefs=mergeRefs;function mergeRefs(...r){return t=>{r.forEach(e=>{typeof e=="function"?e(t):e!=null&&(e.current=t)})}}
