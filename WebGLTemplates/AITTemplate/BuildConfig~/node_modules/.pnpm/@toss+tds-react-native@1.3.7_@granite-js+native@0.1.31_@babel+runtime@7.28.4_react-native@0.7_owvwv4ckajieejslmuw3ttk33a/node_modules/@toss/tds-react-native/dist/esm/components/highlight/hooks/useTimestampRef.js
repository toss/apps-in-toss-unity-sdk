"use strict";import{useEffect as r,useRef as f}from"react";export function useTimestampRef({active:t}){const e=f(0);return r(()=>{t?e.current=new Date().getTime():e.current=0},[t]),e}
