"use strict";import{Dimensions as p}from"react-native";const{width:M,height:m}=p.get("window");export const generateRadiusRectanglePath=({x:n,y:e,width:t,height:o,radius:$})=>{const c=Math.min($,t/2),L=Math.min($,o/2);return`
  M ${n} ${e+L} 
  Q ${n} ${e} ${n+c} ${e}
  L ${n+t-c} ${e}
  Q ${n+t} ${e} ${n+t} ${e+L}
  L ${n+t} ${e+o-L}
  Q ${n+t} ${e+o} ${n+t-c} ${e+o}
  L ${n+c} ${e+o}
  Q ${n} ${e+o} ${n} ${e+o-L}
  Z
`},generateWindowPath=()=>`
  M 0 0
  L ${M} 0
  L ${M} ${m}
  L 0 ${m}
  Z
  `,generateOverlayPath=({width:n,height:e,x:t,y:o,radius:$})=>`M${M},${m}H0V0H${M}V${m}ZM${t+$},${o}a${$},${$},0,0,0-${$},${$}V${e+o-$}a${$},${$},0,0,0,${$},${$}H${n+t-$}a${$},${$},0,0,0,${$}-${$}V${o+$}a${$},${$},0,0,0-${$}-${$}Z`;
