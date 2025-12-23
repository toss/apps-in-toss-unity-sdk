"use strict";export function*repeatCycle(e,i,{gutter:f}={}){for(let l=0;l<i;l++)yield e[l%e.length],l<i-1&&f!=null&&(yield f)}
