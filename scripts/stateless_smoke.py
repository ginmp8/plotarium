#!/usr/bin/env python3
"""0.6.0 one-client-call contract tests with an in-process, untrusted HTTP payload source.

Run: python3 scripts/stateless_smoke.py /path/to/Ploteus.Api.dll
The process uses a loopback source origin explicitly allowlisted for this test only.
"""
import base64
import gzip
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import socket
import subprocess
import sys
import threading
import time
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen

ENVELOPE = {"engine": "echarts", "format": "json", "width": 320, "height": 240,
            "spec": {"xAxis": {"type": "category", "data": ["A", "B"]},
                     "yAxis": {"type": "value"}, "series": [{"type": "bar", "data": [1, 2]}]}}
PAYLOAD = json.dumps(ENVELOPE, separators=(",", ":")).encode()


class SourceHandler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def do_GET(self):
        if self.path == "/chart" or self.path.startswith("/definitions/"):
            status, data, mime, extra = 200, PAYLOAD, "application/json", {}
        elif self.path == "/oversize":
            status, data, mime, extra = 200, b" " * (1024 * 1024 + 1), "application/json", {}
        elif self.path == "/redirect":
            status, data, mime, extra = 302, b"redirect", "application/json", {"Location": "/chart"}
        elif self.path == "/bad-json":
            status, data, mime, extra = 200, b"{", "application/json", {}
        elif self.path == "/bad-mime":
            status, data, mime, extra = 200, PAYLOAD, "text/plain", {}
        elif self.path == "/encoded":
            status, data, mime, extra = 200, gzip.compress(PAYLOAD), "application/json", {"Content-Encoding": "gzip"}
        elif self.path == "/slow":
            time.sleep(4)
            status, data, mime, extra = 200, PAYLOAD, "application/json", {}
        else:
            status, data, mime, extra = 404, b"no", "text/plain", {}
        try:
            self.send_response(status)
            self.send_header("Content-Type", mime)
            self.send_header("Content-Length", str(len(data)))
            for k, v in extra.items(): self.send_header(k, v)
            self.end_headers()
            self.wfile.write(data)
        except (BrokenPipeError, ConnectionResetError):
            pass


def free_port():
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        return sock.getsockname()[1]


def call(base, **params):
    url = base + "/v1/render?" + urlencode(params)
    try:
        with urlopen(Request(url, headers={"Accept": "*/*"}), timeout=30) as response:
            return response.status, response.headers, response.read()
    except HTTPError as response:
        return response.code, response.headers, response.read()


def main():
    dll = Path(sys.argv[1]).resolve()
    if not dll.exists(): raise RuntimeError("Missing API DLL")
    dotnet = os.environ.get("PLOTEUS_DOTNET", "dotnet")
    source = ThreadingHTTPServer(("127.0.0.1", 0), SourceHandler)
    source_port = source.server_address[1]
    threading.Thread(target=source.serve_forever, daemon=True).start()
    api_port = free_port()
    base = "http://127.0.0.1:" + str(api_port)
    source_origin = "http://127.0.0.1:" + str(source_port)
    env = os.environ.copy()
    env.update({"ASPNETCORE_URLS": base,
                "ASPNETCORE_ENVIRONMENT":"Development",
                "Ploteus__Browser__DisableSandbox":"true",
                "Ploteus__Sources__PrivateOrigins__0": source_origin,
                "Ploteus__Sources__Providers__reports": source_origin + "/definitions/",
                "Ploteus__Sources__TimeoutSeconds": "2",
                "Ploteus__Browser__ExecutablePath": "/usr/bin/chromium",
                "Ploteus__Browser__AssetsDirectory": str(dll.parent / "assets"),
                "DOTNET_CLI_TELEMETRY_OPTOUT": "1"})
    results = []
    with open(Path.cwd() / "artifacts-060-server.log", "wb") as log:
        process = subprocess.Popen([dotnet, str(dll)], env=env, stdout=log, stderr=subprocess.STDOUT)
        try:
            for _ in range(100):
                if process.poll() is not None: raise RuntimeError("API exited before startup")
                try:
                    with urlopen(base + "/health/live", timeout=1) as res:
                        if res.status == 200: break
                except (OSError, URLError): time.sleep(0.15)
            else: raise RuntimeError("API failed to start")

            def check(name, expected_status, required=None, mime=None, **params):
                try:
                    status, headers, content = call(base, **params)
                    ok = status == expected_status
                    if required is not None: ok &= required in content
                    if mime is not None: ok &= headers.get_content_type() == mime
                    results.append({"name": name, "status": "pass" if ok else "fail", "http": status, "bytes": len(content)})
                except Exception as e:
                    results.append({"name": name, "status": "fail", "error": str(e)})

            zipped = base64.urlsafe_b64encode(gzip.compress(PAYLOAD)).decode().rstrip("=")
            plain_spec = json.dumps(ENVELOPE["spec"], separators=(",", ":"))
            check("GET inline envelope", 200, b'"engine":"echarts"', "application/json", c=PAYLOAD.decode(), encoding="json")
            check("GET inline bare spec", 200, b'"engine":"echarts"', "application/json", c=plain_spec, engine="echarts", format="json")
            check("GET gzip-base64url", 200, b'"engine":"echarts"', "application/json", c=zipped, encoding="gzip-base64url")
            check("GET gzip override", 200, b'"width":444', "application/json", c=zipped, encoding="gzip-base64url", format="json", width="444")
            check("GET reject two payloads", 400, c=PAYLOAD.decode(), sourceUrl=source_origin + "/chart")
            check("GET invalid gzip", 400, c="bad!", encoding="gzip-base64url")
            check("GET bad image format", 422, c=PAYLOAD.decode(), format="bmp")
            # Execute an actual repeated query parameter.
            url = base + "/v1/render?" + urlencode({"c": PAYLOAD.decode()}) + "&format=png&format=svg"
            try:
                urlopen(url, timeout=8)
                code = 200
            except HTTPError as ex:
                code = ex.code
            results.append({"name": "GET duplicate query params", "status": "pass" if code == 400 else "fail", "http": code})

            check("GET sourceUrl external payload", 200, b'"engine":"echarts"', "application/json", sourceUrl=source_origin + "/chart")
            check("GET sourceUrl output override", 200, b'"width":640', "application/json", sourceUrl=source_origin + "/chart", width="640")
            check("GET configured provider", 200, b'"engine":"echarts"', "application/json", provider="reports", key="2026")
            check("GET reject unconfigured provider", 403, provider="evil", key="2026")
            check("GET reject traversal provider key", 400, provider="reports", key="../metadata")
            check("GET deny private origin without allowlist match", 403, sourceUrl="http://localhost:" + str(source_port) + "/chart")
            check("GET deny metadata address", 403, sourceUrl="http://169.254.169.254/latest/meta-data/")
            check("GET deny file scheme", 400, sourceUrl="file:///etc/passwd")
            check("GET deny URL credentials", 400, sourceUrl="http://x:y@127.0.0.1:" + str(source_port) + "/chart")
            check("GET deny redirect", 502, sourceUrl=source_origin + "/redirect")
            check("GET deny wrong mime", 502, sourceUrl=source_origin + "/bad-mime")
            check("GET deny compressed upstream", 502, sourceUrl=source_origin + "/encoded")
            check("GET reject too large remote", 413, sourceUrl=source_origin + "/oversize")
            check("GET remote malformed JSON", 400, sourceUrl=source_origin + "/bad-json")
            check("GET timeout", 504, sourceUrl=source_origin + "/slow")
            check("GET raw rendered PNG", 200, b"\x89PNG", "image/png", c=PAYLOAD.decode(), format="png")
            check("GET standalone HTML", 200, b"data-ploteus-stateless", "text/html", c=PAYLOAD.decode(), format="html")
            check("GET standalone HTML compressed", 200, b"data-ploteus-stateless", "text/html", c=zipped, encoding="gzip-base64url", format="html")
        finally:
            process.terminate()
            try: process.wait(timeout=5)
            except subprocess.TimeoutExpired: process.kill()
            source.shutdown()

    failed = sum(x["status"] != "pass" for x in results)
    report = {"release": "0.6.0", "passed": len(results)-failed, "failed": failed, "results": results}
    print(json.dumps(report, indent=2))
    folder = Path.cwd() / "evidence" / "0.6.0"
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "stateless-http.json").write_text(json.dumps(report,indent=2)+"\n")
    return 0 if failed==0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
