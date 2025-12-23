"use strict";Object.defineProperty(exports,"__esModule",{value:!0}),exports.generateOverlayPath=exports.generateWindowPath=exports.generateRadiusRectanglePath=void 0;const react_native_1=require("react-native"),{width:windowWidth,height:windowHeight}=react_native_1.Dimensions.get("window"),generateRadiusRectanglePath=({x:$,y:t,width:o,height:n,radius:e})=>{const a=Math.min(e,o/2),c=Math.min(e,n/2);return`
  M ${$} ${t+c} 
  Q ${$} ${t} ${$+a} ${t}
  L ${$+o-a} ${t}
  Q ${$+o} ${t} ${$+o} ${t+c}
  L ${$+o} ${t+n-c}
  Q ${$+o} ${t+n} ${$+o-a} ${t+n}
  L ${$+a} ${t+n}
  Q ${$} ${t+n} ${$} ${t+n-c}
  Z
`};exports.generateRadiusRectanglePath=generateRadiusRectanglePath;const generateWindowPath=()=>`
  M 0 0
  L ${windowWidth} 0
  L ${windowWidth} ${windowHeight}
  L 0 ${windowHeight}
  Z
  `;exports.generateWindowPath=generateWindowPath;const generateOverlayPath=({width:$,height:t,x:o,y:n,radius:e})=>`M${windowWidth},${windowHeight}H0V0H${windowWidth}V${windowHeight}ZM${o+e},${n}a${e},${e},0,0,0-${e},${e}V${t+n-e}a${e},${e},0,0,0,${e},${e}H${$+o-e}a${e},${e},0,0,0,${e}-${e}V${n+e}a${e},${e},0,0,0-${e}-${e}Z`;exports.generateOverlayPath=generateOverlayPath;
