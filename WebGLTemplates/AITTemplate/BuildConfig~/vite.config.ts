import { defineConfig, mergeConfig, type ConfigEnv, type Plugin, type UserConfig } from 'vite';
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

/**
 * devtools mock/패널 활성화 여부를 Vite ConfigEnv + 환경변수로 판정한다.
 *
 * 판정 규칙 (Editor → Vite 환경변수 계약):
 * - `vite build`/`vite preview`(command !== 'serve' 또는 isPreview) → 항상 비활성.
 *   프로덕션 산출물에 mock/패널이 섞여 들어가면 안 됨.
 * - `AIT_DEVTOOLS` 미설정 또는 빈 문자열 → 활성 (수동 `pnpm dev` 실행 시 기본 ON).
 * - `AIT_DEVTOOLS`가 "0" 또는 "false"(대소문자 무관) → 비활성.
 * - 그 외(주로 "1"/"true" — Editor가 Dev 서버 실행 시 항상 명시) → 활성.
 */
function isAitDevtoolsEnabled(env: ConfigEnv): boolean {
  if (env.command !== 'serve' || env.isPreview) return false;

  const raw = process.env.AIT_DEVTOOLS;
  if (raw === undefined || raw === '') return true;

  const normalized = raw.trim().toLowerCase();
  if (normalized === '0' || normalized === 'false') return false;

  return true;
}

/**
 * devtools 플로팅 패널을 index.html에 직접 주입하는 플러그인.
 *
 * @apps-in-toss/devtools의 패널 자동 주입기는 진입점 파일명이
 * `main|index|entry|app.[tj]sx?` 패턴에 매칭될 때만 동작한다. Unity 템플릿의
 * 진입점은 `unity-bridge.ts`라 이 패턴에 걸리지 않으므로, transformIndexHtml
 * 훅으로 index.html에 직접 스크립트 태그를 심어 우회한다.
 *
 * `AIT_DEVTOOLS_PANEL`이 "0"이면(Editor의 config.devtools.panel=false) 주입을 skip한다.
 *
 * 스크립트는 인라인 module이 아니라 `src="/@id/..."`로 주입한다. Vite dev server는
 * `src=`로 네트워크 요청되는 모듈만 bare specifier를 재작성하며, HTML에 텍스트로
 * 박힌 인라인 module script는 변환 대상이 아니라 브라우저가 bare specifier를 그대로
 * 해석하려다 실패한다. `/@id/` prefix는 Vite dev server가 임의 모듈 id를
 * resolveId/load/transform 파이프라인으로 처리하게 하는 코어 지원 경로다.
 */
function aitDevtoolsPanelPlugin(): Plugin {
  return {
    name: 'ait-devtools-panel-inject',
    apply: 'serve',
    transformIndexHtml(html) {
      if (process.env.AIT_DEVTOOLS_PANEL === '0') return html;

      return {
        html,
        tags: [
          {
            tag: 'script',
            attrs: { type: 'module', src: '/@id/@apps-in-toss/devtools/panel' },
            injectTo: 'body',
          },
        ],
      };
    },
  };
}
/**
 * Unity WASM 스트리밍 컴파일용 네이티브 fetch 보호 플러그인.
 *
 * 근본 원인: SDK 자체 번들 Dev Console(vConsole, Runtime/devconsole/vconsole.min.js)의
 * Network 패널이 `new VConsole(...)` 생성 시 자동으로 window.fetch를 `Proxy(fetch, ...)`로
 * 교체하고, 응답도 `new Proxy(response, ...)`로 감싸 반환한다. 이 Proxy는 instanceof/duck
 * typing은 모두 통과하지만 V8의 WebAssembly.compileStreaming/instantiateStreaming 내부
 * 브랜드 체크(엔진 내부 슬롯 확인, Proxy로는 위장 불가)를 통과하지 못해
 * "wasm streaming compile failed" → ArrayBuffer 폴백(다운로드/컴파일 오버랩 상실)이 발생한다.
 *
 * Dev Console은 `enableDebugConsole` Unity 빌드 프로필 플래그로 켜지며, 이 vite 레벨의
 * AIT_DEVTOOLS/AIT_DEVTOOLS_PANEL과는 완전히 독립적이다(실측 확인: AIT_DEVTOOLS=0 이어도
 * 재현됨) — 따라서 이 가드는 devtoolsEnabled 여부와 무관하게 항상 설치한다.
 *
 * head-prepend + order:'pre'로 문서 최초 스크립트로 실행시켜 native fetch를 스냅샷하고,
 * window.fetch를 accessor property로 재정의해 이후 어떤 스크립트가 `window.fetch = X`를
 * 대입하더라도(vConsole 포함, 설치 타이밍 무관) 실제로는 "delegate"에만 저장되도록 가로챈다.
 * Build 산출물(.unityweb/.wasm/.data, /Build/ 경로) 요청만 항상 캡처해둔 네이티브 fetch로
 * 우회시키고, 그 외 요청은 delegate(= vConsole 등이 마지막으로 설치한 patched fetch)로
 * 위임해 Network 탭 가시성을 유지한다.
 */
function aitNativeFetchGuardPlugin(): Plugin {
  return {
    name: 'ait-native-fetch-guard',
    apply: 'serve',
    transformIndexHtml: {
      order: 'pre',
      handler() {
        const script = [
          '(function () {',
          '  if (typeof window === "undefined" || typeof window.fetch !== "function") return;',
          '  var nativeFetch = window.fetch;',
          '  var delegate = nativeFetch;',
          '  function isBuildAssetUrl(input) {',
          '    var url = "";',
          '    if (typeof input === "string") url = input;',
          '    else if (input && typeof input.url === "string") url = input.url;',
          '    else return false;',
          '    return /\\/Build\\//.test(url) || /\\.(unityweb|wasm|data)(\\?|$)/.test(url);',
          '  }',
          '  function stableFetch() {',
          '    var fn = isBuildAssetUrl(arguments[0]) ? nativeFetch : delegate;',
          '    return fn.apply(window, arguments);',
          '  }',
          '  try {',
          '    Object.defineProperty(window, "fetch", {',
          '      configurable: true,',
          '      enumerable: true,',
          '      get: function () { return stableFetch; },',
          '      set: function (v) { delegate = v; },',
          '    });',
          '  } catch (e) {',
          '    console.warn("[AIT] native fetch guard install failed:", e);',
          '  }',
          '})();',
        ].join('\n');

        return [
          {
            tag: 'script',
            attrs: {},
            injectTo: 'head-prepend' as const,
            children: script,
          },
        ];
      },
    },
  };
}
//// SDK_PLUGINS_END ////

//// SDK_GENERATED_START - DO NOT EDIT THIS SECTION ////
async function sdkConfig(env: ConfigEnv): Promise<UserConfig> {
  const devtoolsEnabled = isAitDevtoolsEnabled(env);

  // devtools 플러그인은 활성화된 경우에만 동적으로 로드한다. 프로덕션 빌드
  // (devtoolsEnabled=false)에서는 import 자체를 건너뛰어 번들에 devtools 코드가
  // 섞여 들어가지 않도록 한다.
  const devtoolsPlugins: Plugin[] = [];
  if (devtoolsEnabled) {
    try {
      const { vite: aitDevtools } = await import('@apps-in-toss/devtools/unplugin');
      devtoolsPlugins.push(
        aitDevtools({
          mock: true,
          // 패널 주입은 위 aitDevtoolsPanelPlugin()이 index.html에 직접 담당한다.
          panel: false,
          mcp: process.env.AIT_DEVTOOLS_MCP === '1',
          tunnel: process.env.AIT_DEVTOOLS_TUNNEL === '1',
          webViewType: 'game',
        }) as Plugin,
        aitDevtoolsPanelPlugin(),
      );
    } catch (err) {
      // devtools 미설치(예: SDK 업데이트 직후 node_modules 재설치 누락) 시
      // 조용히 건너뛰고 실 SDK로 계속 진행한다 — 프로덕션 vite build는 이 import
      // 실패와 무관하게(devtoolsEnabled=false라 애초에 이 블록에 들어오지 않음)
      // 항상 죽지 않아야 하고, 개발 모드에서도 mock 없이 서버가 뜰 수 있어야 한다.
      console.warn('[AIT] @apps-in-toss/devtools 로드 실패, mock 없이 진행합니다:', err);
    }
  }

  // devtools 플러그인이 실제로 로드된 경우에만 pre-bundle 대상에 포함시킨다.
  // - include: '@apps-in-toss/devtools/panel'을 미리 pre-bundle해 두지 않으면
  //   panel import가 실 SDK(alias 대상)보다 먼저 최적화되며 mock이 무시될 수 있음
  // - exclude: web-framework/web-analytics를 pre-bundle 대상에서 빼서 devtools의
  //   alias가 걸리기 전에 실 SDK가 먼저 구워지는 것을 방지. 세션 중간에
  //   re-optimize가 발생하면 브라우저 전체 리로드가 걸리므로 이를 예방하는 목적도 있음
  const devtoolsLoaded = devtoolsPlugins.length > 0;

  return {
    // Unity WebGL 압축 파일 헤더 처리 플러그인. devtools는 항상 맨 앞에 위치
    // (web-framework → devtools mock alias가 다른 플러그인보다 먼저 걸려야 함)
    // native-fetch-guard는 devtoolsEnabled와 무관하게(enableDebugConsole은 별도 게이트)
    // 항상 포함 — apply:'serve'라 build/preview에는 어차피 적용 안 됨.
    plugins: [aitNativeFetchGuardPlugin(), ...devtoolsPlugins, unityWebContentEncodingPlugin()],
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
    ...(devtoolsLoaded
      ? {
          optimizeDeps: {
            include: ['@apps-in-toss/devtools/panel'],
            exclude: ['@apps-in-toss/web-framework', '@apps-in-toss/web-analytics'],
          },
        }
      : {}),
  };
}
//// SDK_GENERATED_END ////

//// USER_CONFIG_START ////
const userConfig = defineConfig({
  // 여기에 사용자 커스텀 설정을 추가하세요
  // 예: plugins: [vue()],
});
//// USER_CONFIG_END ////

export default defineConfig(async (env) => mergeConfig(await sdkConfig(env), userConfig as UserConfig));
