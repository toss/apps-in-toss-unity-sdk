"use strict";import{jsx as m}from"react/jsx-runtime";import{Path as s}from"@granite-js/native/react-native-svg";import{Animated as c}from"../../../interactions/animated";import{generateRadiusRectanglePath as h,generateWindowPath as d}from"../utils";import{getHoleAnimateVariants as l}from"./getHoleAnimateVariants";const P=c.createAnimatedComponent(s);export const SvgHolePath=({step:e,layout:a})=>{const t=l(a),o=n(t.initial),i=n(t.default),r=n(t.exit);return m(P,{clipRule:"evenodd",d:e.interpolate({inputRange:[0,1,2],outputRange:[o,i,r]})})};const n=e=>{const a=h(e);return`
  ${d()}
  ${a}
  `};
