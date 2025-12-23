"use strict";export class BaseDerivedTokenGenerator{cache=new Map;create(e){if(this.cache.has(e))return this.cache.get(e);const c=this.calculate(e);return this.cache.set(e,c),c}}
