"use strict";import{useEffect as r}from"react";export const useTimer=({duration:e,callback:t})=>{r(()=>{const o=setTimeout(()=>{t()},e*1e3);return()=>{clearTimeout(o)}},[])};
