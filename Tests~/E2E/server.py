#!/usr/bin/env python3
"""
Apps in Toss SDK E2E Benchmark - HTTP Server
Unity WebGL에서 POST로 벤치마크 결과를 받아서 JSON으로 출력
"""
import http.server
import socketserver
import json
import sys
import signal

PORT = 8000
received_results = False

class BenchmarkHandler(http.server.SimpleHTTPRequestHandler):
    def do_POST(self):
        global received_results
        if self.path == '/benchmark_results':
            content_length = int(self.headers['Content-Length'])
            post_data = self.rfile.read(content_length)

            try:
                results = json.loads(post_data.decode('utf-8'))

                # 사람이 읽을 수 있는 요약 (stderr)
                summary = []
                summary.append("\n" + "="*50)
                summary.append("BENCHMARK RESULTS")
                summary.append("="*50)
                summary.append("")

                # 벤치마크 결과 포맷팅
                if 'avgFPS' in results:
                    summary.append("Performance Metrics:")
                    summary.append(f"  Avg FPS: {results.get('avgFPS', 0):.2f}")
                    summary.append(f"  Min FPS: {results.get('minFPS', 0):.2f}")
                    summary.append(f"  Max FPS: {results.get('maxFPS', 0):.2f}")
                    summary.append(f"  Memory: {results.get('memoryUsageMB', 0):.2f} MB")
                    summary.append(f"  Run Time: {results.get('totalRunTime', 0):.2f} s")
                    summary.append("")

                summary.append("="*50)
                summary.append("")

                # Summary to stderr
                print("\n".join(summary), file=sys.stderr, flush=True)

                # JSON to stdout (for piping to jq, etc.)
                print(json.dumps(results, indent=2), flush=True)

                received_results = True

                # 응답
                self.send_response(200)
                self.send_header('Content-type', 'application/json')
                self.send_header('Access-Control-Allow-Origin', '*')
                self.end_headers()
                self.wfile.write(b'{"status": "success"}')

                # 결과 받았으니 서버 종료
                def shutdown_server():
                    sys.exit(0)

                import threading
                threading.Timer(2.0, shutdown_server).start()

            except Exception as e:
                print(f"Error processing results: {e}", file=sys.stderr, flush=True)
                self.send_response(500)
                self.end_headers()
        else:
            self.send_response(404)
            self.end_headers()

    def do_OPTIONS(self):
        self.send_response(200)
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Methods', 'POST, OPTIONS')
        self.send_header('Access-Control-Allow-Headers', 'Content-Type')
        self.end_headers()

    def log_message(self, format, *args):
        # HTTP 로그 숨기기 (stderr 깔끔하게)
        pass

with socketserver.TCPServer(("", PORT), BenchmarkHandler) as httpd:
    print(f"Server running on http://localhost:{PORT}", file=sys.stderr, flush=True)
    print("Waiting for benchmark results...", file=sys.stderr, flush=True)
    print("", file=sys.stderr, flush=True)
    try:
        httpd.serve_forever()
    except SystemExit:
        pass
