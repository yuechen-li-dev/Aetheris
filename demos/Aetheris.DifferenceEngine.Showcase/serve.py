"""Foreground static production preview, intentionally without a backend."""
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
from functools import partial
import pathlib
import sys

root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else 'artifacts/local/demos/difference-engine-showcase/dist').resolve()
port = int(sys.argv[2]) if len(sys.argv) > 2 else 8776
class Handler(SimpleHTTPRequestHandler):
    extensions_map = {**SimpleHTTPRequestHandler.extensions_map, '.mjs': 'text/javascript', '.js': 'text/javascript'}
    def end_headers(self):
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()
print(f'http://127.0.0.1:{port}/', flush=True)
ThreadingHTTPServer(('127.0.0.1', port), partial(Handler, directory=str(root))).serve_forever()
