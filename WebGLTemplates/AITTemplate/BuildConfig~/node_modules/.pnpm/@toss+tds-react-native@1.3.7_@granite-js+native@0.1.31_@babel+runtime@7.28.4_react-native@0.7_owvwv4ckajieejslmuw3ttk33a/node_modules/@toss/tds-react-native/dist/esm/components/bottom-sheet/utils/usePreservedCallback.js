"use strict";import{useMemo as t,useRef as n}from"react";export function usePreservedCallback(r){const e=n(r);return e.current!==r&&(e.current=r),t(()=>(...u)=>e.current(...u),[])}
