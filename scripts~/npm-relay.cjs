#!/usr/bin/env node
/**
 * npm 레지스트리 로컬 중계 서버
 *
 * 기본 레지스트리가 public npm보다 늦게 새 버전을 받거나, npmjs.org에 SOCKS 프록시로만
 * 닿는 네트워크에서 lockfile 그대로 설치하기 위한 개발자용 도구다. 127.0.0.1에서
 * HTTP 레지스트리를 열고, 들어온 요청을 curl로 프록시를 거쳐 upstream에서 받아 돌려준다.
 * pnpm의 fetch는 SOCKS 프록시를 지원하지 않아서 이렇게 중계한다.
 *
 * 사용법:
 *   # 명령 하나를 중계 레지스트리로 실행하고, 끝나면 서버도 내린다(권장)
 *   node scripts~/npm-relay.cjs -- pnpm install --frozen-lockfile
 *
 *   # 서버만 띄우기(Ctrl+C로 종료)
 *   node scripts~/npm-relay.cjs --port 48731
 *   pnpm_config_registry=http://127.0.0.1:48731/ pnpm install --frozen-lockfile
 *
 * 옵션 / env:
 *   --port N      (NPM_RELAY_PORT)      서버만 띄울 때 포트. 기본 48731. 명령 실행 모드는 빈 포트를 쓴다.
 *   --proxy URL   (NPM_RELAY_PROXY)     curl -x 값. 기본 socks5h://127.0.0.1:1080
 *   --upstream U  (NPM_RELAY_UPSTREAM)  기본 https://registry.npmjs.org
 *
 * 주의: lockfile을 만들거나 고치는 용도로 쓰지 않는다. lockfile은 regen-lockfiles.yml로만
 * 재생성한다. 이 도구는 이미 커밋된 lockfile을 설치할 때만 쓴다.
 */

"use strict";

const http = require("http");
const { spawn } = require("child_process");

function parseArgs(argv) {
  const opts = {
    port: Number(process.env.NPM_RELAY_PORT || 48731),
    proxy: process.env.NPM_RELAY_PROXY || "socks5h://127.0.0.1:1080",
    upstream: (process.env.NPM_RELAY_UPSTREAM || "https://registry.npmjs.org").replace(/\/+$/, ""),
    command: null,
  };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--") {
      opts.command = argv.slice(i + 1);
      break;
    } else if (a === "--port") opts.port = Number(argv[++i]);
    else if (a === "--proxy") opts.proxy = argv[++i];
    else if (a === "--upstream") opts.upstream = argv[++i].replace(/\/+$/, "");
    else if (a === "-h" || a === "--help") {
      console.log("사용법: node scripts~/npm-relay.cjs [--port N] [--proxy URL] [--upstream URL] [-- <명령...>]");
      process.exit(0);
    } else {
      console.error(`알 수 없는 인자: ${a}`);
      process.exit(2);
    }
  }
  if (opts.command && opts.command.length === 0) {
    console.error("'--' 뒤에 실행할 명령이 없습니다.");
    process.exit(2);
  }
  return opts;
}

function createRelay(opts) {
  return http.createServer((req, res) => {
    if (req.method !== "GET" && req.method !== "HEAD") {
      res.writeHead(405);
      res.end();
      return;
    }
    const curl = spawn("curl", [
      "-sS",
      "-f",
      "--max-time",
      "120",
      "-x",
      opts.proxy,
      "-H",
      `Accept: ${req.headers.accept || "*/*"}`,
      `${opts.upstream}${req.url}`,
    ]);
    let started = false;
    curl.stdout.on("data", (chunk) => {
      if (!started) {
        res.writeHead(200);
        started = true;
      }
      res.write(chunk);
    });
    curl.stderr.on("data", (d) => process.stderr.write(`[npm-relay] ${req.url}: ${d}`));
    // curl -f는 upstream 4xx/5xx를 exit 22로 돌려준다. 본문이 나가기 전 실패는 404로 알려
    // pnpm이 해당 요청을 실패로 처리하게 한다.
    curl.on("close", (code) => {
      if (!started) res.writeHead(code === 0 ? 200 : 404);
      res.end();
    });
    req.on("close", () => curl.kill());
  });
}

function main() {
  const opts = parseArgs(process.argv.slice(2));
  const server = createRelay(opts);
  const port = opts.command ? 0 : opts.port;

  server.listen(port, "127.0.0.1", () => {
    const url = `http://127.0.0.1:${server.address().port}/`;
    if (!opts.command) {
      console.log(`npm-relay: ${url} → ${opts.upstream} (proxy ${opts.proxy})`);
      return;
    }
    console.log(`npm-relay: ${url} 로 실행: ${opts.command.join(" ")}`);
    const child = spawn(opts.command[0], opts.command.slice(1), {
      stdio: "inherit",
      env: { ...process.env, pnpm_config_registry: url, npm_config_registry: url },
    });
    child.on("error", (err) => {
      console.error(`명령 실행 실패: ${err.message}`);
      server.close();
      process.exit(127);
    });
    child.on("exit", (code, signal) => {
      server.close();
      process.exit(signal ? 1 : code ?? 1);
    });
  });
}

main();
