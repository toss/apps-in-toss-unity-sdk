import { defineConfig, mergeConfig, type Plugin } from 'vite';
import { openSync, readSync, closeSync } from 'fs';
import { join } from 'path';

//// SDK_PLUGINS_START - DO NOT EDIT THIS SECTION ////
/**
 * Unity WebGL .unityweb 파일의 압축 방식을 헤더에서 감지
 * @param filePath 파일 경로
 * @returns 감지된 압축 방식 ('br' | 'gzip') 또는 null
 */
function detectUnityWebCompression(filePath: string): 'br' | 'gzip' | null {
  try {
    // 파일 헤더의 처음 64바이트만 읽음
    const fd = openSync(filePath, 'r');
    const buffer = Buffer.alloc(64);
    const bytesRead = readSync(fd, buffer, 0, 64, 0);
    closeSync(fd);

    const header = buffer.toString('ascii');

    // decompressionFallback=ON: Unity가 텍스트 매직 헤더를 기록
    if (header.includes('(brotli)')) return 'br';
    if (header.includes('(gzip)')) return 'gzip';

    // decompressionFallback=OFF: 매직 헤더 없는 raw 압축 스트림.
    // .unityweb는 항상 압축 산출물이므로(비압축 빌드는 .unityweb를 만들지 않음)
    // gzip magic(0x1f 0x8b)이면 gzip, 그 외에는 brotli(매직 없음)로 간주한다.
    if (bytesRead >= 2 && buffer[0] === 0x1f && buffer[1] === 0x8b) return 'gzip';
    return 'br';
  } catch {
    return null;
  }
}

/**
 * Unity WebGL 산출물 경로 패턴으로 Content-Type을 설정.
 * .wasm.→application/wasm (instantiateStreaming 활성), .js.→javascript, .data.→octet-stream.
 * .unityweb / .br / .gz 모두 ".wasm." 같은 중간 패턴을 포함하므로 한 함수로 커버된다.
 */
function setUnityContentType(
  res: { setHeader(name: string, value: string): void },
  url: string,
): void {
  if (url.includes('.wasm.')) {
    res.setHeader('Content-Type', 'application/wasm');
  } else if (url.includes('.js.')) {
    res.setHeader('Content-Type', 'application/javascript');
  } else if (url.includes('.data.')) {
    res.setHeader('Content-Type', 'application/octet-stream');
  }
}

/**
 * Unity WebGL 압축 파일용 Content-Encoding 헤더 플러그인
 *
 * Unity 6부터 .unityweb 확장자로 압축 파일이 통합됨.
 * 이 플러그인은 파일 헤더를 읽어 압축 방식을 감지하고,
 * 적절한 Content-Encoding 헤더를 설정하여 브라우저가 직접 압축 해제하도록 함.
 *
 * 헤더가 없으면 Unity의 JavaScript 디컴프레서가 처리하지만,
 * 헤더가 있으면 브라우저가 직접 처리하여 시작 시간이 단축됨.
 */
function unityWebContentEncodingPlugin(): Plugin {
  const compressionCache = new Map<string, 'br' | 'gzip' | null>();

  function createMiddleware(baseDir: string) {
    return (
      req: { url?: string },
      res: { setHeader(name: string, value: string): void },
      next: () => void,
    ) => {
      const url = req.url || '';

      // 로컬 개발 시 Unity Build 파일 캐시 방지
      // Unity WebGL은 IndexedDB(UnityCache)에 빌드 파일을 해시 기반으로 캐시함.
      // 빌드가 바뀌면 캐시된 해시와 불일치하여 "Unknown data format" 에러 발생.
      // no-store로 항상 서버에서 새로 받도록 강제.
      if (url.includes('/Build/')) {
        res.setHeader('Cache-Control', 'no-store');
      }

      if (url.endsWith('.unityweb')) {
        const filePath = join(process.cwd(), baseDir, url);

        // 캐시 확인 또는 감지
        let encoding = compressionCache.get(filePath);
        if (encoding === undefined) {
          encoding = detectUnityWebCompression(filePath);
          compressionCache.set(filePath, encoding);
        }

        if (encoding) {
          res.setHeader('Content-Encoding', encoding);
        }

        // Content-Type 설정 (instantiateStreaming 활성화 위해 .wasm은 application/wasm)
        setUnityContentType(res, url);
      }
      // 레거시 .br 파일 (Unity 2021/2022 — Unity 6는 .unityweb 사용)
      else if (url.endsWith('.br')) {
        res.setHeader('Content-Encoding', 'br');
        setUnityContentType(res, url);
      }
      // 레거시 .gz 파일 (Unity 2021/2022 — Unity 6는 .unityweb 사용)
      else if (url.endsWith('.gz')) {
        res.setHeader('Content-Encoding', 'gzip');
        setUnityContentType(res, url);
      }

      next();
    };
  }

  return {
    name: 'unity-web-content-encoding',
    configureServer(server) {
      server.middlewares.use(createMiddleware('public'));
    },
    configurePreviewServer(server) {
      server.middlewares.use(createMiddleware('dist/web'));
    },
  };
}
//// SDK_PLUGINS_END ////

//// SDK_GENERATED_START - DO NOT EDIT THIS SECTION ////
const sdkConfig = defineConfig({
  // Unity WebGL 압축 파일 헤더 처리 플러그인
  plugins: [unityWebContentEncodingPlugin()],
  // Apps in Toss 플랫폼에서 서브 경로 호스팅을 위해 상대 경로 사용
  base: './',
  server: {
    host: process.env.AIT_VITE_HOST || '%AIT_VITE_HOST%',
    port: parseInt(process.env.AIT_VITE_PORT || '%AIT_VITE_PORT%', 10),
    strictPort: true, // 포트 충돌 시 서버 실행 실패
  },
  build: {
    // Unity WebGL 빌드와 호환되도록 설정
    target: 'es2015',
    // 우리 브릿지/템플릿 코드의 sourcemap(.js.map)을 생성하지 않는다. Vite 기본값도
    // false 지만, 배포 번들에 불필요한 .js.map 이 새어 들어가지 않도록 명시로 고정한다.
    // (.ait 에 담기는 필수 .js.map 은 @apps-in-toss/cli 가 만드는 RN 브릿지 번들
    //  bundle.{platform}.js.map 뿐이며, 이는 배포 서버가 필수 번들 파일로 요구하므로
    //  건드리지 않는다. 우리 쪽에서 추가로 map 을 만들지 않게만 관리한다.)
    sourcemap: false,
    // 빌드 출력 설정
    rollupOptions: {
      output: {
        // 해시를 포함하지 않는 파일명으로 출력 (예측 가능한 이름)
        entryFileNames: 'assets/[name].js',
        chunkFileNames: 'assets/[name].js',
        assetFileNames: 'assets/[name][extname]',
      },
    },
  },
});
//// SDK_GENERATED_END ////

//// USER_CONFIG_START ////
const userConfig = defineConfig({
  // 여기에 사용자 커스텀 설정을 추가하세요
  // 예: plugins: [vue()],
});
//// USER_CONFIG_END ////

export default mergeConfig(sdkConfig, userConfig);
