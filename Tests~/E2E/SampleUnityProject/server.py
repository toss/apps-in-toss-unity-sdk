#!/usr/bin/env python3
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

                if 'baselineTest' in results:
                    summary.extend(self.format_test_result("Baseline Test", results['baselineTest']))

                if 'physicsTest' in results:
                    summary.extend(self.format_test_result("Physics Stress Test", results['physicsTest']))

                if 'renderingTest' in results:
                    summary.extend(self.format_test_result("Rendering Benchmark", results['renderingTest']))

                if 'combinedTest' in results:
                    summary.extend(self.format_test_result("Combined Test", results['combinedTest']))

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
    
    def format_test_result(self, title, test):
        lines = []
        lines.append(f"{title}:")
        lines.append(f"  Avg FPS: {test.get('avgFps', 0):.2f}")
        lines.append(f"  Min FPS: {test.get('minFps', 0):.2f}")
        lines.append(f"  Max FPS: {test.get('maxFps', 0):.2f}")
        lines.append(f"  Memory: {test.get('memoryUsedMB', 0):.2f} MB")
        lines.append(f"  Samples: {test.get('sampleCount', 0)}")
        lines.append("")
        return lines

    def log_message(self, format, *args):
        # HTTP 로그 숨기기
        pass

with socketserver.TCPServer(("", PORT), BenchmarkHandler) as httpd:
    print(f"Server running on http://localhost:{PORT}", file=sys.stderr, flush=True)
    print("Waiting for benchmark results...", file=sys.stderr, flush=True)
    print("", file=sys.stderr, flush=True)
    try:
        httpd.serve_forever()
    except SystemExit:
        pass
