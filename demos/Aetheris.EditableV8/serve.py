"""Foreground, loopback-only static showcase server. Ctrl+C stops it."""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer

parser = argparse.ArgumentParser()
parser.add_argument("directory")
parser.add_argument("--port", type=int, default=8765)
args = parser.parse_args()

class Handler(SimpleHTTPRequestHandler):
    # Windows registry MIME associations must not turn ES modules into text/plain.
    extensions_map = {**SimpleHTTPRequestHandler.extensions_map, ".mjs": "text/javascript", ".js": "text/javascript"}
    def do_GET(self):
        if "If-Modified-Since" in self.headers:
            del self.headers["If-Modified-Since"]
        super().do_GET()

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

server = ThreadingHTTPServer(("127.0.0.1", args.port), partial(Handler, directory=args.directory))
print(f"Editable V8: http://127.0.0.1:{args.port}/ (Ctrl+C to stop)", flush=True)
try:
    server.serve_forever()
except KeyboardInterrupt:
    pass
finally:
    server.server_close()
