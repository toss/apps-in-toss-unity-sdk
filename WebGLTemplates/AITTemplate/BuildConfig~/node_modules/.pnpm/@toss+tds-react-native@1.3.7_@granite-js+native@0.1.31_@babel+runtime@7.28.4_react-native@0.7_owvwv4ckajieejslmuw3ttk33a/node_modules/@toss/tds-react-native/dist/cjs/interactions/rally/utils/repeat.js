"use strict";Object.defineProperty(exports,"__esModule",{value:!0}),exports.repeat=repeat;function*repeat(i,r,{gutter:t}={}){for(let e=0;e<r;e++)yield i,e<r-1&&t!=null&&(yield t)}
