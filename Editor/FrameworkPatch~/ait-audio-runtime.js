// AIT audio runtime payload - injected into the Unity WebGL framework by AITFrameworkPatcher (build time).
// This file is plain ES5, ASCII only, and has no dependency besides the framework's own WEBAudio / HEAPU8 globals.
// Contents: aitAudioProbe (container header probe: mime, duration, channels, rate, loop tags) + WEBAudio.aitDecide.
// The patcher appends `WEBAudio.aitCfg={force:<0|1>,minSec:<seconds>};` after this text (literals baked at patch time).
// Full-line // comments are stripped before injection (the framework is minified; a stray // must never swallow code).
// Local check (outside Unity): new Function(fs.readFileSync(thisFile,'utf8')+';return aitAudioProbe')()
// AIT audio container probe. Pure function, no DOM. Returns null when the container is not recognised.
// {mime, dur (sec), ch, rate}. All parsing is bounds-checked; any exception -> null (caller keeps stock behaviour).
function aitAudioProbe(u){ // u: Uint8Array (a VIEW over the wasm heap is fine, nothing is copied)
  try{
    var n=u.length,dv=new DataView(u.buffer,u.byteOffset,n),loopTag=false;
    (function(){var m=Math.min(n,16384),a="";for(var q=0;q<m;q++){var c=u[q];a+=c>=97&&c<=122?String.fromCharCode(c-32):c>=65&&c<=90?String.fromCharCode(c):"."}
      loopTag=a.indexOf("LOOPSTART")>=0||a.indexOf("LOOP_START")>=0||a.indexOf("LOOPEND")>=0||a.indexOf("LOOPLENGTH")>=0})();
    function s4(o){return String.fromCharCode(u[o],u[o+1],u[o+2],u[o+3])}
    if(n<12)return null;
    // ---- WAV (RIFF/WAVE) ----
    if(s4(0)==="RIFF"&&s4(8)==="WAVE"){
      var o=12,ch=0,rate=0,brate=0,dsz=0;
      while(o+8<=n){
        var id=s4(o),sz=dv.getUint32(o+4,true);
        if(id==="fmt "&&o+24<=n){ch=dv.getUint16(o+10,true);rate=dv.getUint32(o+12,true);brate=dv.getUint32(o+16,true)}
        else if(id==="smpl"&&o+44<=n&&dv.getUint32(o+36,true)>0){loopTag=true}
        else if(id==="data"){dsz=Math.min(sz,n-o-8);break}
        o+=8+sz+(sz&1);
      }
      if(!ch||!brate)return null;
      for(var t=o+8+dsz+(dsz&1);t+8<=n;){var id2=s4(t),sz2=dv.getUint32(t+4,true);if(id2==="smpl"&&t+44<=n&&dv.getUint32(t+36,true)>0)loopTag=true;t+=8+sz2+(sz2&1)}
      return{mime:"audio/wav",dur:dsz/brate,ch:ch,rate:rate,loop:loopTag};
    }
    // ---- Ogg (Vorbis / Opus) ----
    if(s4(0)==="OggS"){
      var segs=u[26],p=27+segs,ch2=0,rate2=0,pre=0,opus=false;
      if(s4(p+1)==="vorb"&&u[p]===1){ch2=u[p+11];rate2=dv.getUint32(p+12,true)}
      else if(s4(p)==="Opus"&&s4(p+4)==="Head"){opus=true;ch2=u[p+9];pre=dv.getUint16(p+10,true);rate2=48000}
      else return null;
      var gp=-1;
      for(var i=n-14;i>=Math.max(0,n-65536);i--){
        if(u[i]===79&&u[i+1]===103&&u[i+2]===103&&u[i+3]===83){ // "OggS" (last page)
          var lo=dv.getUint32(i+6,true),hi=dv.getUint32(i+10,true);
          if(hi!==0xFFFFFFFF||lo!==0xFFFFFFFF){gp=hi*4294967296+lo}
          break;
        }
      }
      if(gp<0||!rate2)return null;
      return{mime:opus?"audio/ogg; codecs=opus":"audio/ogg",dur:(gp-pre)/rate2,ch:ch2,rate:rate2,loop:loopTag};
    }
    // ---- MP4 / M4A (ISO BMFF) ----
    if(s4(4)==="ftyp"){
      var dur3=0,ts=0,ch3=2,rate3=44100;
      (function walk(o,end,depth){
        while(o+8<=end){
          var sz=dv.getUint32(o),t=s4(o+4),hdr=8;
          if(sz===1){sz=dv.getUint32(o+12);hdr=16} // 64-bit size, low word is enough here
          if(sz===0)sz=end-o;
          if(sz<hdr||o+sz>end+0)sz=Math.min(sz,end-o);
          if(t==="moov"||t==="trak"||t==="mdia"||t==="minf"||t==="stbl"){if(depth<8)walk(o+hdr,o+sz,depth+1)}
          else if(t==="mvhd"){
            var ver=u[o+hdr];
            if(ver===1){ts=dv.getUint32(o+hdr+20);dur3=dv.getUint32(o+hdr+24)*4294967296+dv.getUint32(o+hdr+28)}
            else{ts=dv.getUint32(o+hdr+12);dur3=dv.getUint32(o+hdr+16)}
          }else if(t==="stsd"){
            var e=o+hdr+8; // version/flags(4)+entry_count(4)
            if(e+36<=end&&s4(e+4)==="mp4a"){ch3=dv.getUint16(e+24);rate3=dv.getUint16(e+32)}
          }
          if(sz<8)break;
          o+=sz;
        }
      })(0,n,0);
      if(!ts||!dur3)return null;
      return{mime:"audio/mp4",dur:dur3/ts,ch:ch3,rate:rate3,loop:loopTag};
    }
    // ---- MP3 (ID3v2 + frames) / raw ADTS AAC ----
    var o4=0;
    if(s4(0).slice(0,3)==="ID3"){o4=10+((u[6]&127)<<21|(u[7]&127)<<14|(u[8]&127)<<7|(u[9]&127))}
    if(u[o4]===255&&(u[o4+1]&246)===240)return aitAdts(u,n,o4);
    var lim=Math.min(n-4,o4+65536);
    for(var j=o4;j<lim;j++){
      if(u[j]!==255||(u[j+1]&224)!==224)continue;
      var b1=u[j+1],b2=u[j+2],b3=u[j+3];
      var layer=(b1>>1)&3,mpv=(b1>>3)&3;
      if(layer===0||mpv===1)continue;
      var brI=(b2>>4)&15,srI=(b2>>2)&3;
      if(brI===0||brI===15||srI===3)continue;
      var v1=(mpv===3);
      var tbl=[[0,32,64,96,128,160,192,224,256,288,320,352,384,416,448],  // V1 L1
               [0,32,48,56,64,80,96,112,128,160,192,224,256,320,384],    // V1 L2
               [0,32,40,48,56,64,80,96,112,128,160,192,224,256,320],     // V1 L3
               [0,32,48,56,64,80,96,112,128,144,160,176,192,224,256],    // V2 L1
               [0,8,16,24,32,40,48,56,64,80,96,112,128,144,160]];        // V2 L2/L3
      var row=layer===3?(v1?0:3):(v1?(layer===2?1:2):4);
      var kbps=tbl[row][brI];
      var rt=[44100,48000,32000][srI]/(v1?1:(mpv===2?2:4));
      var chn=((b3>>6)&3)===3?1:2;
      var spf=layer===3?384:(layer===2||v1)?1152:576;
      var flen=layer===3?((12*kbps*1000/rt|0)+((b2>>1)&1))*4:((spf/8*kbps*1000/rt|0)+((b2>>1)&1));
      if(j+flen+1<n&&!(u[j+flen]===255&&(u[j+flen+1]&224)===224))continue; // next frame must also sync
      var next=j+4+(v1?(chn===1?17:32):(chn===1?9:17)); // Xing/Info offset (after 4 byte header + side info)
      var frames=0,tag=s4(next);
      if(tag==="Xing"||tag==="Info"){if(u[next+7]&1)frames=dv.getUint32(next+8)}
      else if(s4(j+36)==="VBRI"){frames=dv.getUint32(j+14)}
      var dur4=frames?frames*spf/rt:(n-j-(s4(n-128)==="TAG\u0000"?0:0))*8/(kbps*1000);
      return{mime:"audio/mpeg",dur:dur4,ch:chn,rate:rt,approx:!frames,loop:loopTag};
    }
    return null;
  }catch(e){return null}
}
function aitAdts(u,n,k){
  var sr=[96000,88200,64000,48000,44100,32000,24000,22050,16000,12000,11025,8000][(u[k+2]>>2)&15];
  var cc=((u[k+2]&1)<<2)|(u[k+3]>>6),p2=k,fr=0;
  while(p2+7<=n&&u[p2]===255&&(u[p2+1]&246)===240){var fl=((u[p2+3]&3)<<11)|(u[p2+4]<<3)|(u[p2+5]>>5);if(fl<7)break;p2+=fl;fr++;if(fr>200000)break}
  if(fr<2||!sr)return null;
  return{mime:"audio/aac",dur:fr*1024/sr,ch:cc||2,rate:sr,loop:false};
}
WEBAudio.aitDecide=function(ptr,length,decompress){
  var u=HEAPU8.subarray(ptr,ptr+length),info=aitAudioProbe(u),cfg=WEBAudio.aitCfg;
  if(!decompress){
    /* Unity already chose the media-element path. 6000.0.85f1+ stock decodes everything on Chromium (window.chrome) because
       Chrome never frees MediaElementAudioSourceNode; keep that for short/unknown clips and only keep long clips compressed. */
    if(window.chrome&&!(info&&info.dur>=cfg.minSec))return null;
    return{compressed:true,info:info}}
  if(!cfg.force||!info||!(info.dur>0)||info.loop)return null;  /* unknown container / loop tags (media element ignores loop points) -> stock PCM */
  if(!(info.dur>=cfg.minSec))return null;                      /* short clips keep the low-latency PCM path */
  try{var ok=new Audio().canPlayType(info.mime.split(";")[0]);if(!ok)return null}catch(e){return null}
  return{compressed:true,info:info,forced:true}};
WEBAudio.aitLog=function(d,len,decompress){
  try{var i=d.info||{};console.log("[AIT-Audio] compressed playback "+(d.forced?"forced":"kept")+": "+(len/1048576).toFixed(2)+"MB mime="+i.mime+" dur="+(i.dur>0?i.dur.toFixed(1):"?")+"s ch="+i.ch+" unityDecompress="+(decompress?1:0))}catch(e){}};
// ---- gapless loop for media-element sounds. HTMLMediaElement.loop re-seeks at the end: ~45 ms gap on Chromium, ~0.5 s on WebKit
// (a native loop restart is as slow as a cold start). A looping source gets one spare element (own MediaElementAudioSourceNode and
// DelayNode, wired to the same outputs). The element's start latency is neither small nor stable on WebKit (5 ms to 0.5 s) and its
// currentTime runs ~0.3 s behind the audio, so timing is done in the audio graph instead:
//  - the audio position is modelled on the wall clock; an AnalyserNode tapped before the delay gives the moment the first sample of a
//    (re)started element reaches the graph, which anchors the model (anchor = wall time at which position p is heard);
//  - the spare is parked at 0 behind a long delay and started `lead` ms before the active element's predicted end; once its first
//    sample is seen, delayTime is set so that sample comes out exactly when the active element's last sample does (the lines before
//    the first sample are zeros, so changing the delay at that moment makes no sound). A lead longer than the real latency is free.
// Unity keeps seeing one source: source.mediaElement is redirected to whichever element is current. Native loop stays on until the
// swap starts, so a missed swap degrades to the old behaviour.
// Opt-out: window.__AIT_PERF.loopSwap=false or ?aitloopswap=0. Fixed lead: __AIT_PERF.loopSwapLeadMs (ms, otherwise adaptive).
WEBAudio.aitLoopFlags=function(){
  var f=WEBAudio.aitLoopF;
  if(f)return f;
  var p=(typeof window==="object"&&window.__AIT_PERF)||{},on=!(p.loopSwap===false||p.loopSwap===0),lead=+p.loopSwapLeadMs;
  try{if(new URLSearchParams(location.search).get("aitloopswap")==="0")on=false}catch(e){}
  return WEBAudio.aitLoopF={on:on,lead:lead>=0&&lead<=1800?lead:-1}};
WEBAudio.aitLoopStats=WEBAudio.aitLoopStats||{swaps:0,aborts:0,fails:0,late:0};
WEBAudio.aitLoopHook=function(source,clip){
  var F=WEBAudio.aitLoopFlags();
  if(!F.on||!clip||!clip.url||typeof Audio!=="function")return;
  var ctx=WEBAudio.audioContext,S=WEBAudio.aitLoopStats,orig=source.mediaElement,cur=orig,old=null,sb=null,on=false,dead=false,failed=false;
  var endedH=null,durC=null,DURC0=typeof window==="object"&&window.chrome?0:-50,probing=0,startAt=0,timer=0,graceTimer=0,oldTimer=0,conns=[],played=false,anch=null,anchId=0,lead=F.lead>=0?F.lead:(typeof window==="object"&&window.chrome?100:700),Lrs=30;
  var MAXD=2,realConnect=source.connect,realDisconnect=source.disconnect,anA=ctx.createAnalyser(),eA={el:orig,node:source,dn:ctx.createDelay(MAXD),an:anA};
  var EV=["play","pause","seeking","ratechange","durationchange","timeupdate","loadedmetadata","waiting","ended"];
  function now(){return typeof performance==="object"?performance.now():Date.now()}
  function pause(el){try{el.pause()}catch(e){}}
  function seek0(el){try{el.currentTime=0}catch(e){}}
  function spare(){return cur===orig?(sb&&sb.el):orig}
  function entry(el){return el===orig?eA:(sb&&sb.el===el?sb:null)}
  function wire(dn){try{dn.disconnect()}catch(e){}for(var i=0;i<conns.length;i++){try{dn.connect.apply(dn,conns[i])}catch(e){}}}
  /* a delay line keeps the last MAXD seconds it heard and would replay them when the delay grows, and an analyser keeps its last window:
     start from a fresh, empty pair */
  function freshDelay(en){
    var nd=ctx.createDelay(MAXD),na=ctx.createAnalyser();
    na.fftSize=2048;
    try{realConnect.call(en.node,nd);realConnect.call(en.node,na)}catch(e){}
    try{realDisconnect.call(en.node,en.dn);realDisconnect.call(en.node,en.an)}catch(e){}
    try{en.dn.disconnect()}catch(e){}
    en.dn=nd;en.an=na;wire(nd)}
  function setDelay(el,sec){var en=entry(el);if(en){en.dn.delayTime.value=Math.max(0,Math.min(MAXD,sec));el.aitD=Math.max(0,Math.min(MAXD,sec))*1000}}
  anA.fftSize=2048;
  realConnect.call(source,anA);realConnect.call(source,eA.dn);
  source.connect=function(d){
    conns.push(Array.prototype.slice.call(arguments));dead=false;
    eA.dn.connect.apply(eA.dn,arguments);
    if(sb)try{sb.dn.connect.apply(sb.dn,arguments)}catch(e){}
    return d};
  source.disconnect=function(){
    var a=Array.prototype.slice.call(arguments);
    if(!a.length)conns.length=0;else conns=conns.filter(function(c){return c[0]!==a[0]});
    try{eA.dn.disconnect.apply(eA.dn,arguments)}catch(e){}
    if(sb)try{sb.dn.disconnect.apply(sb.dn,arguments)}catch(e){}
    if(!a.length&&played){dead=true;clearTimeout(graceTimer);graceTimer=setTimeout(idle,800)}};
  function listen(el){for(var i=0;i<EV.length;i++)el.addEventListener(EV[i],onEv)}
  function unlisten(el){for(var i=0;i<EV.length;i++)el.removeEventListener(EV[i],onEv)}
  function acquire(){
    if(sb)return sb;
    var pool=clip.aitPool||(clip.aitPool=[]),i,e=null;
    for(i=0;i<pool.length;i++)if(!pool[i].busy){e=pool[i];break}
    if(!e){
      var el=WEBAudio.audioCache&&WEBAudio.audioCache.length>2?WEBAudio.audioCache.pop():new Audio;
      el.autoplay=false;el.preservesPitch=false;el.webkitPreservesPitch=false;
      var node=ctx.createMediaElementSource(el),dn=ctx.createDelay(MAXD),an=ctx.createAnalyser();
      an.fftSize=2048;node.connect(an);node.connect(dn);
      e={el:el,node:node,dn:dn,an:an,busy:false};
      if(pool.length<4)pool.push(e)}
    e.busy=true;sb=e;listen(e.el);wire(e.dn);return e}
  /* park the spare: freshly loaded (a restarted WebKit element drops ~0.35 s of audio and starts with a random delay, a freshly
     loaded one starts in a stable ~0.5 s), paused at 0, behind the longest delay (anything it plays before the swap is hidden) */
  function park(el){
    el.aitParked=true;
    var en=entry(el);
    if(en)freshDelay(en);
    setDelay(el,MAXD);
    try{pause(el);if(el.getAttribute("src")){el.removeAttribute("src");el.load()}el.preload="auto";el.loop=false;el.src=clip.url;el.load()}catch(x){}}
  function prepare(){
    clearTimeout(graceTimer);graceTimer=0;
    acquire();
    var el=spare();
    if(el&&!old&&!el.aitParked)park(el);
    return el}
  function spareOk(b){return !!b&&b.aitParked&&!!b.getAttribute("src")&&b.readyState>=2&&!b.seeking&&b.paused&&b.currentTime<0.05}
  /* ms from t0 (a performance.now stamp taken at play()) until the first non-zero sample is in the graph; -1 when unknown */
  function probe(el,t0,limit,cb){
    var en=entry(el),buf,n,sr=ctx.sampleRate;
    if(!en||!en.an||typeof en.an.getFloatTimeDomainData!=="function")return cb(-1);
    n=en.an.fftSize;buf=new Float32Array(n);
    (function poll(){
      var i,t=now();
      en.an.getFloatTimeDomainData(buf);
      for(i=0;i<n;i++)if(buf[i]>1e-4||buf[i]<-1e-4)break;
      if(i<n)return cb(i===0?-1:t-(n-1-i)/sr*1000-t0);
      if(t-t0>limit||el.paused)return cb(-1);
      setTimeout(poll,4)})()}
  function posNow(){return anch?anch.pos+(now()-anch.wall)*anch.rate/1000:-1}
  /* (re)anchor the model after the current element (re)started at position pos at wall time t0 */
  function anchorStart(el,pos,t0,withProbe){
    var id=++anchId,rate=el.playbackRate||1,D=el.aitD||0;
    anch=null;startAt=t0;el.aitFirst=0;
    if(!withProbe){anch={wall:t0+Lrs+D,pos:pos,rate:rate,t:now()};arm();return}
    probing++;
    probe(el,t0,1500,function(L){
      probing--;
      if(id!==anchId||el!==cur)return;
      anch={wall:t0+(L>=0?L:Lrs)+D,pos:pos,rate:rate,t:now()};
      if(L>=0){el.aitFirst=t0+L;el.aitPos0=pos;el.aitRate=rate}
      arm()})}
  function finishOld(){
    clearTimeout(oldTimer);oldTimer=0;
    var o=old;
    if(!o)return;
    old=null;pause(o);seek0(o);o.aitParked=false;
    if(on&&!dead&&!cur.paused)setTimeout(function(){prepare();arm()},100)}
  function dispose(full){
    finishOld();clearTimeout(timer);timer=0;
    var el=spare();
    if(el&&el.getAttribute("src")){try{pause(el);el.aitParked=false;el.removeAttribute("src");el.load()}catch(e){}}
    if(full&&sb){
      unlisten(sb.el);sb.busy=false;sb.el.aitParked=false;
      try{pause(sb.el);sb.el.removeAttribute("src");sb.el.load()}catch(e){}
      try{sb.dn.disconnect()}catch(e){}
      sb=null}}
  function idle(){
    graceTimer=0;
    if(!cur.paused&&!dead)return;
    dispose(dead)}
  /* start the spare now; tend = wall time at which the active element's last sample is heard */
  function swap(tend){
    var a=cur,b=spare();
    if(old||!spareOk(b))return false;
    var t0=now(),rate=a.playbackRate||1;
    b.playbackRate=a.playbackRate;b.loop=on;a.loop=false;b.aitParked=false;
    b.onended=a.onended||endedH;a.onended=null;endedH=null;
    if(!Object.getOwnPropertyDescriptor(source,"mediaElement")){
      Object.defineProperty(source,"mediaElement",{configurable:true,get:function(){return cur}})}
    old=a;cur=b;anch=null;b.aitSwap=true;startAt=t0;
    var id=++anchId,r;
    try{r=b.play()}catch(e){r=null;failed=true}
    if(r&&r.catch)r.catch(function(){
      failed=true;S.fails++;
      if(cur===b){cur=a;old=null;a.onended=b.onended;b.onended=null;a.loop=on;if(a.ended){seek0(a);try{a.play()}catch(e){}}pause(b);b.aitParked=false}});
    S.swaps++;
    probing++;
    probe(b,t0,Math.min(3000,lead+1500),function(L){
      probing--;
      if(id!==anchId||cur!==b)return;
      var Lm=L>=0?L:lead,delay=(tend-(t0+Lm))/1000;
      setDelay(b,delay);
      if(delay<0)S.late++;
      if(F.lead<0&&L>=0)lead=Math.max(100,Math.min(1800,Math.round(L+70)));
      if(L>=0&&L<60)Lrs=Math.round(L);
      anch={wall:t0+Lm+b.aitD,pos:0,rate:rate,t:now()};
      b.aitFirst=L>=0?t0+L:0;b.aitPos0=0;b.aitRate=rate;
      arm()});
    clearTimeout(oldTimer);oldTimer=setTimeout(finishOld,lead+2500+MAXD*1000);
    return true}
  function arm(){
    clearTimeout(timer);timer=0;
    if(!on||failed||dead||old||!anch)return;
    var a=cur;
    if(a.paused)return;
    var d=a.duration;
    if(!(d>0&&isFinite(d)))return;
    var b=spare();
    if(!b||!b.aitParked){prepare();timer=setTimeout(arm,250);return}
    if(!spareOk(b)){timer=setTimeout(arm,150);return}
    var tend=anch.wall+(d-anch.pos)/anch.rate*1000+(durC===null?DURC0:durC),ms=tend-lead-now();
    if(a.loop&&ms>0)a.loop=false;
    if(ms<=0){
      if(tend-now()>-100&&swap(tend))return;
      S.late++;return}
    timer=setTimeout(arm,ms>80?ms-60:Math.max(2,ms/2))}
  function onEv(e){
    var t=e.target,ty=e.type;
    if(!on)return;
    if(t===old){
      if(ty==="pause"&&!t.ended)finishOld();
      if(ty==="ended"){
        /* length of the audio actually produced vs the element's duration (differs by tens of ms on WebKit) */
        if(t.aitFirst>0&&t.aitRate===(t.playbackRate||1)&&t.duration>0){
          var c=now()-t.aitFirst-(t.duration-t.aitPos0)/t.aitRate*1000;
          if(c>-200&&c<200)durC=durC===null?c:durC*0.5+c*0.5}
        /* the last samples are still travelling through the element's delay line */
        clearTimeout(oldTimer);oldTimer=setTimeout(finishOld,(t.aitD||0)+150)}
      return}
    if(t!==cur){return}
    if(ty==="ended"){
      /* native loop was cut for the swap, so this is not the end of the sound: keep Unity's onended handler away from it */
      if(t.onended){endedH=t.onended;t.onended=null}
      /* the swap window was missed by a few ms (the element's own delay is short on Chromium): swap now, the tail is still in the delay line */
      if(anch&&!old&&!failed&&spareOk(spare())&&swap(now()+(t.aitD||0)))return;
      if(!(anch&&spareOk(spare()))&&!old){
        /* the swap cannot happen any more: loop by hand, like the native loop would */
        t.loop=on;seek0(t);
        try{var rp=t.play();if(rp&&rp.catch)rp.catch(function(){})}catch(e){}
        t.onended=endedH;endedH=null}
      return}
    if(ty==="pause"){
      if(t.ended)return;
      t.aitFirst=0;
      anch=null;anchId++;
      if(old)finishOld();
      if(t.aitD>0){var en=entry(t);if(en){freshDelay(en);setDelay(t,0)}}
      clearTimeout(timer);timer=0;clearTimeout(graceTimer);graceTimer=setTimeout(idle,dead?800:5000);return}
    if(ty==="play"){
      played=true;dead=false;
      if(t.aitSwap){t.aitSwap=false;return}
      anchorStart(t,t.currentTime,now(),!t.aitWarm||t.currentTime<0.05);
      prepare();return}
    if(ty==="seeking"){
      if(!t.paused&&!probing&&now()-startAt>400)anchorStart(t,t.currentTime,now(),t.currentTime<0.05);
      return}
    if(ty==="ratechange"){
      t.aitFirst=0;
      if(anch){var p=posNow();anch={wall:now(),pos:p,rate:t.playbackRate||1,t:now()}}
      arm();return}
    if(ty==="waiting"){
      if(anch&&now()-anch.t>1500){anch=null;anchId++}
      return}
    if(!t.aitWarm&&t.currentTime>1)t.aitWarm=true;
    arm()}
  listen(orig);
  source.aitLoop=function(v){
    v=!!v;
    if(v===on)return;
    on=v;
    if(on){if(!cur.paused){played=true;anchorStart(cur,cur.currentTime,now(),false);prepare()}}
    else{
      if(endedH){cur.onended=endedH;endedH=null}
      clearTimeout(timer);timer=0;clearTimeout(graceTimer);graceTimer=0;anch=null;anchId++;dispose(false)}};
  source.aitLoopState=function(){var sp=spare();return{on:on,ready:spareOk(sp),swapped:cur!==orig,old:!!old,failed:failed,dead:dead,timer:!!timer,probing:probing,durC:durC,lead:lead,anch:!!anch,spare:sp||null,current:cur}};
};
