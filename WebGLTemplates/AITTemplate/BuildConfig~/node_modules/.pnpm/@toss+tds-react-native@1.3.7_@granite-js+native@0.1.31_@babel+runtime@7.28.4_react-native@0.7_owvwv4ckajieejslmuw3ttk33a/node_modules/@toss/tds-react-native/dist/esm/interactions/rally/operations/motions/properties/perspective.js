"use strict";import{parseMotionValue as n}from"../../../helper";export function getPerspective(t,{layout:e,beforeValue:o}){return n((typeof t=="function"?t(e.width):t)??0)?.number??o}
