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
  if(!decompress)return{compressed:true,info:info};           /* Unity already chose the media-element path */
  if(!cfg.force||!info||!(info.dur>0)||info.loop)return null;  /* unknown container / loop tags (media element ignores loop points) -> stock PCM */
  if(!(info.dur>=cfg.minSec))return null;                      /* short clips keep the low-latency PCM path */
  try{var ok=new Audio().canPlayType(info.mime.split(";")[0]);if(!ok)return null}catch(e){return null}
  return{compressed:true,info:info,forced:true}};
WEBAudio.aitLog=function(d,len,decompress){
  try{var i=d.info||{};console.log("[AIT-Audio] compressed playback "+(d.forced?"forced":"kept")+": "+(len/1048576).toFixed(2)+"MB mime="+i.mime+" dur="+(i.dur>0?i.dur.toFixed(1):"?")+"s ch="+i.ch+" unityDecompress="+(decompress?1:0))}catch(e){}};
