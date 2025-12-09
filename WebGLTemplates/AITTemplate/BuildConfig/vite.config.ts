import { defineConfig } from 'vite';

export default defineConfig({
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
