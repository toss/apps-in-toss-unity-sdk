"use strict";export function transformOriginXY({x:r,y:t,width:a,height:o}){const n={x:.5,y:.5};return{translateX:[a*(r-n.x),a*(n.x-r)],translateY:[o*(t-n.y),o*(n.y-t)]}}
