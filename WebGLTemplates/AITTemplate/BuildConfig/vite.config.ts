import { defineConfig } from 'vite';

export default defineConfig({
  server: {
    host: true,
    strictPort: false, // 포트 충돌 시 자동으로 다음 가용 포트 사용
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
