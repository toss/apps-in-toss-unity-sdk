(function(){
 if(window.__lm) return; var T0=performance.now(); var MB=1048576;
 var lm=window.__lm={mems:[],grows:[],wasm:{},tex:0,texBytes:0,draws:0,errors:[],drawLast:0};
 function L(tag,o){ try{ console.log('[LM] '+tag+' '+Math.round(performance.now())+' '+(o===undefined?'':(typeof o==='string'?o:JSON.stringify(o)))); }catch(e){} }
 window.__lmLog=L;
 try{ new PerformanceObserver(function(l){ l.getEntries().forEach(function(e){ if(/\/Build\/|\.wasm|\.data|framework|loader/.test(e.name)) L('res',{n:e.name.split('/').pop().slice(0,40),end:Math.round(e.responseEnd),dur:Math.round(e.duration),enc:e.encodedBodySize,dec:e.decodedBodySize}); }); }).observe({type:'resource',buffered:true}); }catch(e){}
 var OMc=WebAssembly.Memory; function reg(m){ lm.reg&&lm.reg(m); }
 // wasm compile/instantiate phases
 try{ ['compile','compileStreaming','instantiate','instantiateStreaming'].forEach(function(m){ var o=WebAssembly[m]; if(typeof o!=='function')return; WebAssembly[m]=function(){ var s=performance.now(); L('wasm.'+m+'.start'); var r=o.apply(this,arguments); return r.then(function(x){ L('wasm.'+m+'.done',{ms:Math.round(performance.now()-s)}); try{ var ins=x&&x.instance?x.instance:x; if(ins&&ins.exports){ for(var k in ins.exports){ if(ins.exports[k] instanceof OMc) reg(ins.exports[k]); } } }catch(e){} return x; },function(e){ L('wasm.'+m+'.FAIL',String(e&&e.message||e).slice(0,200)); throw e; }); }; }); }catch(e){}
 // memory ctor + grow
 var seenM=new WeakSet(); function reg(m){ try{ if(m&&!seenM.has(m)){ seenM.add(m); lm.mems.push(new WeakRef(m)); } }catch(e){} } lm.reg=reg;
 try{ var OM=WebAssembly.Memory; var og=OM.prototype.grow;
  OM.prototype.grow=function(d){ reg(this); var from=this.buffer.byteLength; var s=performance.now(); try{ var r=og.apply(this,arguments); var to=this.buffer.byteLength; lm.grows.push([Math.round(s),Math.round(performance.now()-s),from,to,1]); L('grow',{fromMB:Math.round(from/MB),toMB:Math.round(to/MB),ms:Math.round(performance.now()-s)}); return r; }catch(e){ lm.grows.push([Math.round(s),0,from,from+d*65536,0,String(e.message).slice(0,120)]); L('grow.FAIL',{fromMB:Math.round(from/MB),wantMB:Math.round((from+d*65536)/MB),err:String(e&&e.message).slice(0,160)}); throw e; } };
  window.WebAssembly.Memory=new Proxy(OM,{construct:function(t,a,nt){ var r=Reflect.construct(t,a,nt===window.WebAssembly.Memory?t:nt); try{ lm.mems.push(new WeakRef(r)); L('memctor',{init:a[0]&&a[0].initial,max:a[0]&&a[0].maximum,shared:!!(a[0]&&a[0].shared)}); }catch(e){} return r; }});
 }catch(e){}
 window.__lmHeapMB=function(){ var b=0; lm.mems.forEach(function(w){ var m=w.deref(); if(m){ try{ b=Math.max(b,m.buffer.byteLength); }catch(e){} } }); return Math.round(b/MB*10)/10; };
 // gl upload counters
 function wrapGL(proto){ if(!proto) return; ['texImage2D','texSubImage2D','texImage3D','texStorage2D','compressedTexImage2D','compressedTexImage3D','compressedTexSubImage2D'].forEach(function(m){ var o=proto[m]; if(typeof o!=='function')return; proto[m]=function(){ lm.tex++; try{ var a=arguments[arguments.length-1]; if(a&&a.byteLength) lm.texBytes+=a.byteLength; }catch(e){} return o.apply(this,arguments); }; }); }
 try{ wrapGL(window.WebGLRenderingContext&&WebGLRenderingContext.prototype); wrapGL(window.WebGL2RenderingContext&&WebGL2RenderingContext.prototype); }catch(e){}
 // draw counter (all draws; default fb or not) for liveness
 try{ ['drawElements','drawArrays','drawElementsInstanced','drawArraysInstanced'].forEach(function(m){ [window.WebGLRenderingContext,window.WebGL2RenderingContext].forEach(function(C){ if(!C)return; var o=C.prototype[m]; if(typeof o!=='function')return; C.prototype[m]=function(){ lm.draws++; return o.apply(this,arguments); }; }); }); }catch(e){}
 // rAF-based frame counter
 lm.raf=0; (function f(){ lm.raf++; requestAnimationFrame(f); })();
 // errors
 window.addEventListener('error',function(e){ lm.errors.push('error: '+String(e.message).slice(0,200)); L('ERR',String(e.message).slice(0,200)); });
 window.addEventListener('unhandledrejection',function(e){ var m=String(e.reason&&(e.reason.message||e.reason)).slice(0,200); lm.errors.push('unhandled: '+m); L('UNHANDLED',m); });
 var ca=window.alert; window.alert=function(m){ L('ALERT',String(m).slice(0,300)); };
 // 1Hz snapshot streamed to console so the host has the last known state if the renderer dies
 setInterval(function(){ var jh=0; try{ jh=Math.round(performance.memory.usedJSHeapSize/MB); }catch(e){} L('snap',{heapMB:window.__lmHeapMB(),js:jh,draws:lm.draws,raf:lm.raf,tex:lm.tex,texMB:Math.round(lm.texBytes/MB),ttff:window.__TTFF__===null?null:Math.round(window.__TTFF__)}); },1000);
})();
