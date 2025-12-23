"use strict";export function*repeat(f,i,{gutter:l}={}){for(let e=0;e<i;e++)yield f,e<i-1&&l!=null&&(yield l)}
