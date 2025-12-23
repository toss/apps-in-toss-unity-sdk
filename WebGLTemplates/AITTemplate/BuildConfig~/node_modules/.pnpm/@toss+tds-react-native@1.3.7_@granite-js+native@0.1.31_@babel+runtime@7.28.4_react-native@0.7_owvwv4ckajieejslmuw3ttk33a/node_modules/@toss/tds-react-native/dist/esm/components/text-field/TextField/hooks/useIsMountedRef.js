"use strict";import{useLayoutEffect as t,useRef as u}from"react";export function useIsMountedRef(){const e=u({mounted:!1}).current;return t(()=>(e.mounted=!0,()=>{e.mounted=!1}),[e]),e}
