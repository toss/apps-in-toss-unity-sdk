import { defineConfig, mergeConfig } from 'vite';
import { unityBridgePlugin } from './vite-plugin-unity-bridge';

//// SDK_GENERATED_START - DO NOT EDIT THIS SECTION ////
const sdkConfig = defineConfig({
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
  plugins: [
    // 커스텀 브릿지 플러그인 (bridges/ 폴더에서 TypeScript → C# + jslib 자동 생성)
    unityBridgePlugin({
      bridgesDir: './bridges',
      outputDir: './generated',
    }),
  ],
});
//// USER_CONFIG_END ////

export default mergeConfig(sdkConfig, userConfig);
