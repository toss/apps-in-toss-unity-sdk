// 컨테이너 안에서 0.0.0.0:9223 -> 127.0.0.1:9222 로 CDP 를 전달한다
// (헤드리스 Chromium 은 remote-debugging-address 를 무시하고 루프백에만 바인드한다).
import net from 'net';
net.createServer((c) => {
  const u = net.connect(9222, '127.0.0.1');
  c.pipe(u); u.pipe(c);
  const end = () => { c.destroy(); u.destroy(); };
  c.on('error', end); u.on('error', end); c.on('close', end); u.on('close', end);
}).listen(9223, '0.0.0.0');
