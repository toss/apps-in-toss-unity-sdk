"use strict";export function batchDerivedTokenGenerators(e){return r=>Object.fromEntries(Object.entries(e).map(([t,n])=>[t,n.create(r)]))}
