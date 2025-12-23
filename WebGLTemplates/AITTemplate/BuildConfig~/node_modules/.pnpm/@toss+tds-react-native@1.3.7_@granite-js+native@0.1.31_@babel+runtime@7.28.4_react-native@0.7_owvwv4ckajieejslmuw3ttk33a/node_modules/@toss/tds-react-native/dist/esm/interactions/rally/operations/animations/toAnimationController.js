"use strict";export function toAnimationController(r,{progress:t,endProgress:s}){return{...r,start(){return new Promise(e=>{r.start(n=>{e(n.finished)})})},seekProgress(e){t.setValue(e*s)}}}
