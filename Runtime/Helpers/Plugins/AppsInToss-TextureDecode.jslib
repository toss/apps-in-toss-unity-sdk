/**
 * Apps in Toss Unity SDK - Texture Decode JavaScript Bridge
 * 스트리밍 텍스처의 PNG/JPG 를 브라우저(fetch + createImageBitmap)에서 풀어 Unity 의 GL 텍스처 자리에 직접 올린다.
 * 압축 바이트와 디코드 버퍼가 wasm 힙에 들어오지 않는다(wasm 메모리는 줄지 않으므로 힙 high-water 를 막는 것이 목적).
 *
 * 스텁은 non-readable 압축 텍스처(WebGL 2 에서 texStorage2D 불변 저장소)라 texSubImage2D 로 RGBA 를 올릴 수 없다.
 * 그래서 Swap 은 새 WebGL 텍스처를 만들어 level 0 을 texImage2D(RGBA8)로 올리고, 이전 텍스처의 샘플러 파라미터를 복사한 뒤
 * GL.textures[name] 을 새 텍스처로 교체하고 이전 것을 지운다. Unity 네이티브는 숫자 id 만 들고 있어 id 는 그대로다.
 *
 * C# 은 콜백 없이 폴링한다: Start 로 시작 → Poll 이 0(진행 중)이 아닐 때까지 대기 → Width/Height → Swap.
 * 실패 코드는 C# AITStreamingTexture.BrowserFailureText 와 같다.
 *   Poll: 0 = 진행 중, 1 = 디코드 완료, 음수 = 실패(-1 fetch/HTTP, -2 이미지 아님(br 미해제 등), -3 디코드 실패, -4 예외, -5 알 수 없는 요청)
 *   Swap: 1 = 성공, 음수 = 실패(-10 GL 없음, -11 텍스처 이름 무효, -12 업로드/GL 오류, -5 요청 없음)
 */
mergeInto(LibraryManager.library, {
    /**
     * 브라우저 디코드 가능 여부. 2 = WebGL 2 컨텍스트 + createImageBitmap + GL.textures 테이블, 0 = 불가.
     */
    __AITTexDecode_Capability: function() {
        try {
            var gl = (typeof GLctx !== 'undefined' && GLctx) ? GLctx : (Module && Module.ctx);
            if (!gl || typeof WebGL2RenderingContext === 'undefined' || !(gl instanceof WebGL2RenderingContext)) return 0;
            if (typeof createImageBitmap !== 'function' || typeof fetch !== 'function') return 0;
            if (typeof GL === 'undefined' || !GL.textures) return 0;
            return 2;
        } catch (e) {
            return 0;
        }
    },

    /**
     * 교체 경로 자기 검증: glTexName 의 텍스처를 4x4 빨강 RGBA 새 텍스처로 교체하고, 프레임버퍼로 readPixels 해 값을 확인한다.
     * 1 = 성공, 음수 = 실패. 교체 후 GL.textures[name] 은 새 텍스처이므로 Unity 가 지울 때도 일관된다.
     */
    __AITTexDecode_SelfTest: function(glTexName) {
        try {
            var gl = (typeof GLctx !== 'undefined' && GLctx) ? GLctx : (Module && Module.ctx);
            if (!gl) return -10;
            var old = GL.textures[glTexName];
            if (!old || !gl.isTexture(old)) return -11;
            var px = new Uint8Array(4 * 4 * 4);
            for (var i = 0; i < 16; i++) { px[i * 4] = 255; px[i * 4 + 1] = 0; px[i * 4 + 2] = 0; px[i * 4 + 3] = 255; }
            while (gl.getError() !== gl.NO_ERROR) {}
            var prevTex = gl.getParameter(gl.TEXTURE_BINDING_2D);
            var prevFbo = gl.getParameter(gl.FRAMEBUFFER_BINDING);
            var created = gl.createTexture();
            var fbo = null;
            var ok = -12;
            try {
                gl.bindTexture(gl.TEXTURE_2D, created);
                gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, 4, 4, 0, gl.RGBA, gl.UNSIGNED_BYTE, px);
                gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
                fbo = gl.createFramebuffer();
                gl.bindFramebuffer(gl.FRAMEBUFFER, fbo);
                gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, created, 0);
                if (gl.checkFramebufferStatus(gl.FRAMEBUFFER) === gl.FRAMEBUFFER_COMPLETE) {
                    var out = new Uint8Array(4);
                    gl.readPixels(0, 0, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, out);
                    if (out[0] === 255 && out[1] === 0 && out[2] === 0 && out[3] === 255 && gl.getError() === gl.NO_ERROR) ok = 1;
                }
            } finally {
                gl.bindFramebuffer(gl.FRAMEBUFFER, prevFbo);
                if (fbo) gl.deleteFramebuffer(fbo);
                gl.bindTexture(gl.TEXTURE_2D, prevTex);
            }
            if (ok !== 1) { gl.deleteTexture(created); return ok; }
            // 실제 교체와 같은 절차(바인딩 재지정 + 테이블 교체 + 이전 삭제)를 한 번 태운다.
            var units = gl.getParameter(gl.MAX_COMBINED_TEXTURE_IMAGE_UNITS);
            var activeUnit = gl.getParameter(gl.ACTIVE_TEXTURE);
            for (var u = 0; u < units; u++) {
                gl.activeTexture(gl.TEXTURE0 + u);
                if (gl.getParameter(gl.TEXTURE_BINDING_2D) === old) gl.bindTexture(gl.TEXTURE_2D, created);
            }
            gl.activeTexture(activeUnit);
            GL.textures[glTexName] = created;
            gl.deleteTexture(old);
            return gl.getError() === gl.NO_ERROR ? 1 : -12;
        } catch (e) {
            return -12;
        }
    },

    /**
     * 디코드 시작. bytesLen > 0 이면 wasm 메모리의 바이트(bytesPtr)를 JS 로 복사해 디코드하고(서버가 brotli 를 해제하지 않은 경우의
     * managed 폴백), 아니면 url 을 fetch 한다. 이미지 매직(PNG/JPG)이 아니면 -2. 결과 ImageBitmap 은 reqId 로 보관한다.
     * orientation: Unity 텍스처는 행 0 이 아래이므로 flipY 로 디코드한다. 알파는 straight(premultiplyAlpha none), ICC 변환 없음.
     */
    __AITTexDecode_Start: function(urlPtr, bytesPtr, bytesLen, reqId) {
        var url = bytesLen > 0 ? '' : UTF8ToString(urlPtr);
        var inlineBytes = bytesLen > 0 ? HEAPU8.slice(bytesPtr, bytesPtr + bytesLen) : null;
        var W = (typeof window !== 'undefined') ? window : self;
        var st = W.__aitTexDecode = W.__aitTexDecode || {};
        st[reqId] = { status: 0, bmp: null, w: 0, h: 0 };
        var rec = st[reqId];
        (async function() {
            try {
                var blob;
                if (inlineBytes) {
                    blob = new Blob([inlineBytes]);
                } else {
                    var resp = await fetch(url);
                    if (!resp.ok) { rec.status = -1; return; }
                    blob = await resp.blob();
                }
                var head = new Uint8Array(await blob.slice(0, 4).arrayBuffer());
                var isPng = head.length >= 4 && head[0] === 0x89 && head[1] === 0x50 && head[2] === 0x4E && head[3] === 0x47;
                var isJpg = head.length >= 3 && head[0] === 0xFF && head[1] === 0xD8 && head[2] === 0xFF;
                if (!isPng && !isJpg) { rec.status = -2; return; }
                var bmp;
                try {
                    bmp = await createImageBitmap(blob, { imageOrientation: 'flipY', premultiplyAlpha: 'none', colorSpaceConversion: 'none' });
                } catch (e) {
                    rec.status = -3; return;
                }
                if (rec.cancelled) { try { bmp.close(); } catch (e2) {} return; }
                rec.bmp = bmp; rec.w = bmp.width; rec.h = bmp.height; rec.status = 1;
            } catch (e) {
                rec.status = (e && e.name === 'TypeError') ? -1 : -4;
            }
        })();
    },

    __AITTexDecode_Poll: function(reqId) {
        var W = (typeof window !== 'undefined') ? window : self;
        var rec = W.__aitTexDecode && W.__aitTexDecode[reqId];
        return rec ? rec.status : -5;
    },

    __AITTexDecode_Width: function(reqId) {
        var W = (typeof window !== 'undefined') ? window : self;
        var rec = W.__aitTexDecode && W.__aitTexDecode[reqId];
        return rec ? rec.w : 0;
    },

    __AITTexDecode_Height: function(reqId) {
        var W = (typeof window !== 'undefined') ? window : self;
        var rec = W.__aitTexDecode && W.__aitTexDecode[reqId];
        return rec ? rec.h : 0;
    },

    /**
     * 디코드된 ImageBitmap 으로 GL.textures[glTexName] 을 새 텍스처로 교체한다(hasMips != 0 이면 mip 생성).
     * 이전 텍스처가 묶여 있던 모든 텍스처 유닛을 새 텍스처로 다시 묶는다(삭제하면 GL 이 0 으로 풀어 Unity 의 바인딩 캐시와 어긋난다).
     * 실패하면 이전 텍스처는 그대로 남는다. ImageBitmap 은 항상 close, 요청은 정리된다.
     */
    __AITTexDecode_Swap: function(reqId, glTexName, hasMips) {
        var W = (typeof window !== 'undefined') ? window : self;
        var st = W.__aitTexDecode;
        var rec = st && st[reqId];
        if (!rec || !rec.bmp) return -5;
        var result = 1;
        var gl = null;
        var created = null;
        try {
            gl = (typeof GLctx !== 'undefined' && GLctx) ? GLctx : (Module && Module.ctx);
            if (!gl) { result = -10; }
            else {
                var old = GL.textures[glTexName];
                if (!old || !gl.isTexture(old)) { result = -11; }
                else {
                    while (gl.getError() !== gl.NO_ERROR) {}
                    var prevTex = gl.getParameter(gl.TEXTURE_BINDING_2D);
                    var activeUnit = gl.getParameter(gl.ACTIVE_TEXTURE);
                    try {
                        // 이전 텍스처의 샘플러 파라미터를 읽는다(old 를 잠깐 묶는다).
                        gl.bindTexture(gl.TEXTURE_2D, old);
                        var wrapS = gl.getTexParameter(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S);
                        var wrapT = gl.getTexParameter(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T);
                        var minF = gl.getTexParameter(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER);
                        var magF = gl.getTexParameter(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER);
                        var aniso = null, anisoExt = gl.getExtension('EXT_texture_filter_anisotropic');
                        if (anisoExt) aniso = gl.getTexParameter(gl.TEXTURE_2D, anisoExt.TEXTURE_MAX_ANISOTROPY_EXT);

                        created = gl.createTexture();
                        gl.bindTexture(gl.TEXTURE_2D, created);
                        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, gl.RGBA, gl.UNSIGNED_BYTE, rec.bmp);
                        if (hasMips) { gl.generateMipmap(gl.TEXTURE_2D); }
                        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, wrapS);
                        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, wrapT);
                        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, minF);
                        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, magF);
                        if (anisoExt && aniso !== null) gl.texParameterf(gl.TEXTURE_2D, anisoExt.TEXTURE_MAX_ANISOTROPY_EXT, aniso);
                        if (gl.getError() !== gl.NO_ERROR) { result = -12; }
                    } finally {
                        gl.bindTexture(gl.TEXTURE_2D, (prevTex === old && result === 1) ? created : prevTex);
                    }

                    if (result === 1) {
                        var units = gl.getParameter(gl.MAX_COMBINED_TEXTURE_IMAGE_UNITS);
                        for (var u = 0; u < units; u++) {
                            gl.activeTexture(gl.TEXTURE0 + u);
                            if (gl.getParameter(gl.TEXTURE_BINDING_2D) === old) gl.bindTexture(gl.TEXTURE_2D, created);
                        }
                        gl.activeTexture(activeUnit);
                        GL.textures[glTexName] = created;
                        gl.deleteTexture(old);
                        created = null;
                    }
                }
            }
        } catch (e) {
            result = -12;
        }
        if (created && gl) { try { gl.deleteTexture(created); } catch (e4) {} }
        try { rec.bmp.close(); } catch (e3) {}
        delete st[reqId];
        return result;
    },

    /** 요청을 정리한다(폴백 전환·취소). 진행 중이면 완료 시 bitmap 을 닫는다. */
    __AITTexDecode_Release: function(reqId) {
        var W = (typeof window !== 'undefined') ? window : self;
        var st = W.__aitTexDecode;
        var rec = st && st[reqId];
        if (!rec) return;
        rec.cancelled = true;
        if (rec.bmp) { try { rec.bmp.close(); } catch (e) {} rec.bmp = null; }
        delete st[reqId];
    }
});
