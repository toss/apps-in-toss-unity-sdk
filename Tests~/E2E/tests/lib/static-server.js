// @ts-check
// Content-Encoding을 절대 붙이지 않는 정적 서버.
// vite preview(unityWebContentEncodingPlugin)는 .unityweb에 Content-Encoding을 붙여 주므로
// "호스트가 헤더를 못 주는 환경"을 재현하려면 별도 서버가 필요하다. 이 서버는 바이트를 그대로 내보낸다.
// 이 디렉터리(lib/) 파일은 이름이 *.test.js/*.spec.js가 되면 안 된다(playwright 기본 testMatch에 잡힘).
import * as fs from 'fs';
import * as http from 'http';
import * as path from 'path';

const CONTENT_TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'application/javascript',
  '.mjs': 'application/javascript',
  '.css': 'text/css',
  '.json': 'application/json',
  '.wasm': 'application/wasm',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
};

/**
 * @param {string} rootDir 서빙할 디렉터리(dist/web)
 * @param {number} port
 * @returns {Promise<http.Server>}
 */
export function startStaticServer(rootDir, port) {
  const root = path.resolve(rootDir);
  const server = http.createServer((req, res) => {
    const urlPath = decodeURIComponent((req.url || '/').split('?')[0]);
    let filePath = path.join(root, urlPath.endsWith('/') ? `${urlPath}index.html` : urlPath);
    // 디렉터리 밖 접근 차단
    if (!filePath.startsWith(root)) {
      res.writeHead(403).end();
      return;
    }
    fs.stat(filePath, (err, stat) => {
      if (err || !stat.isFile()) {
        res.writeHead(404).end();
        return;
      }
      // .unityweb은 확장자만으로 형식을 알 수 없으므로 Content-Type을 octet-stream으로 둔다.
      // 호스트가 압축 형식을 모르는 상황 그대로다.
      const ext = path.extname(filePath);
      res.writeHead(200, {
        'Content-Type': CONTENT_TYPES[ext] || 'application/octet-stream',
        'Content-Length': stat.size,
        'Cache-Control': 'no-store',
      });
      if (req.method === 'HEAD') {
        res.end();
        return;
      }
      fs.createReadStream(filePath).pipe(res);
    });
  });
  return new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(port, '127.0.0.1', () => resolve(server));
  });
}
