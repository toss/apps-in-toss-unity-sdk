/**
 * Apps in Toss Unity SDK - Texture Decode JavaScript Bridge
 * 스트리밍 텍스처의 PNG/JPG 를 브라우저(fetch + createImageBitmap)에서 풀어 Unity 의 GL 텍스처에 직접 올린다.
 * 압축 바이트와 디코드 버퍼가 wasm 힙에 들어오지 않는다(wasm 메모리는 줄지 않으므로 힙 high-water 를 막는 것이 목적).
 *
 * C# 은 콜백 없이 폴링한다: Fetch 로 시작 → Poll 이 0(진행 중)이 아닐 때까지 대기 → Width/Height 로 크기 확인 →
 * (C# 이 스텁 크기·포맷을 맞춘 뒤) Upload. 실패 코드는 C# AITStreamingTexture.BrowserFailureText 와 같다.
 *   Poll:   0 = 진행 중, 1 = 디코드 완료, 음수 = 실패(-1 fetch/HTTP, -2 이미지 아님(br 미해제 등), -3 디코드 실패, -4 예외, -5 알 수 없는 요청)
 *   Upload: 1 = 성공, 음수 = 실패(-10 GL 없음, -11 텍스처 이름 무효, -12 업로드 예외/GL 오류, -5 요청 없음)
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
     * glTexName 이 Unity GL 테이블의 유효한 WebGLTexture 인지 확인하고 4x4 RGBA 를 올려 본다(최초 1회 자기 검증용).
     * 1 = 성공, 음수 = 실패.
     */
    __AITTexDecode_SelfTest: function(glTexName) {
        try {
            var gl = (typeof GLctx !== 'undefined' && GLctx) ? GLctx : (Module && Module.ctx);
            if (!gl) return -10;
            var tex = GL.textures[glTexName];
            if (!tex || !gl.isTexture(tex)) return -11;
            var prev = gl.getParameter(gl.TEXTURE_BINDING_2D);
            var ok = 1;
            try {
                while (gl.getError() !== gl.NO_ERROR) {}
                gl.bindTexture(gl.TEXTURE_2D, tex);
                gl.texSubImage2D(gl.TEXTURE_2D, 0, 0, 0, 4, 4, gl.RGBA, gl.UNSIGNED_BYTE, new Uint8Array(64));
                if (gl.getError() !== gl.NO_ERROR) ok = -12;
            } finally {
                gl.bindTexture(gl.TEXTURE_2D, prev);
            }
            return ok;
        } catch (e) {
            return -12;
        }
    },

    /**
     * url 을 fetch 해 ImageBitmap 으로 디코드한다(reqId 로 보관). 이미지 매직(PNG/JPG)이 아니면 -2.
     * orientation: Unity 텍스처는 행 0 이 아래이므로 flipY 로 디코드해 둔다. 알파는 straight(premultiplyAlpha none), ICC 변환 없음.
     */
    __AITTexDecode_Fetch: function(urlPtr, reqId) {
        var url = UTF8ToString(urlPtr);
        var W = (typeof window !== 'undefined') ? window : self;
        var st = W.__aitTexDecode = W.__aitTexDecode || {};
        st[reqId] = { status: 0, bmp: null, w: 0, h: 0 };
        var rec = st[reqId];
        (async function() {
            try {
                var resp = await fetch(url);
                if (!resp.ok) { rec.status = -1; return; }
                var blob = await resp.blob();
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
     * 디코드된 ImageBitmap 을 GL.textures[glTexName] 의 level 0 에 올린다(texSubImage2D). hasMips 가 0 이 아니면 mip 을 다시 만든다.
     * Unity 의 바인딩 상태를 건드리지 않도록 이전 TEXTURE_2D 바인딩을 복원하고, ImageBitmap 은 항상 close 한다. 요청은 정리된다.
     */
    __AITTexDecode_Upload: function(reqId, glTexName, hasMips) {
        var W = (typeof window !== 'undefined') ? window : self;
        var st = W.__aitTexDecode;
        var rec = st && st[reqId];
        if (!rec || !rec.bmp) return -5;
        var result = 1;
        try {
            var gl = (typeof GLctx !== 'undefined' && GLctx) ? GLctx : (Module && Module.ctx);
            if (!gl) { result = -10; }
            else {
                var tex = GL.textures[glTexName];
                if (!tex || !gl.isTexture(tex)) { result = -11; }
                else {
                    var prev = gl.getParameter(gl.TEXTURE_BINDING_2D);
                    try {
                        while (gl.getError() !== gl.NO_ERROR) {}
                        gl.bindTexture(gl.TEXTURE_2D, tex);
                        gl.texSubImage2D(gl.TEXTURE_2D, 0, 0, 0, gl.RGBA, gl.UNSIGNED_BYTE, rec.bmp);
                        if (hasMips) { gl.generateMipmap(gl.TEXTURE_2D); }
                        if (gl.getError() !== gl.NO_ERROR) { result = -12; }
                    } finally {
                        gl.bindTexture(gl.TEXTURE_2D, prev);
                    }
                }
            }
        } catch (e) {
            result = -12;
        }
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
