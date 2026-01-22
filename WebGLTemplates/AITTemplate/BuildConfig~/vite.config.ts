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
    readSync(fd, buffer, 0, 64, 0);
    closeSync(fd);

    const header = buffer.toString('ascii');

    if (header.includes('(brotli)')) return 'br';
    if (header.includes('(gzip)')) return 'gzip';
    return null;
  } catch {
    return null;
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

  return {
    name: 'unity-web-content-encoding',
    configureServer(server) {
      server.middlewares.use((req, res, next) => {
        const url = req.url || '';

        if (url.endsWith('.unityweb')) {
          // public/ 폴더 기준으로 파일 경로 계산
          const filePath = join(process.cwd(), 'public', url);

          // 캐시 확인 또는 감지
          let encoding = compressionCache.get(filePath);
          if (encoding === undefined) {
            encoding = detectUnityWebCompression(filePath);
            compressionCache.set(filePath, encoding);
          }

          if (encoding) {
            res.setHeader('Content-Encoding', encoding);
          }

          // Content-Type 설정
          if (url.includes('.wasm.')) {
            res.setHeader('Content-Type', 'application/wasm');
          } else if (url.includes('.js.')) {
            res.setHeader('Content-Type', 'application/javascript');
          } else if (url.includes('.data.')) {
            res.setHeader('Content-Type', 'application/octet-stream');
          }
        }
        // 레거시 .br 파일 (Unity 2021/2022)
        else if (url.endsWith('.br')) {
          res.setHeader('Content-Encoding', 'br');
        }
        // 레거시 .gz 파일 (Unity 2021/2022)
        else if (url.endsWith('.gz')) {
          res.setHeader('Content-Encoding', 'gzip');
        }

        next();
      });
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
