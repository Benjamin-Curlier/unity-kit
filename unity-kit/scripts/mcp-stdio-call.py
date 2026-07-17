#!/usr/bin/env python3
"""Minimal stdio MCP client for MCP for Unity — for sessions that started BEFORE the
UnityMCP server was registered (so no mcp__unityMCP__* tools exist in-session).

Runs a batch of calls against the mcp-for-unity stdio server and prints results
(truncated on stdout; full results written to <calls>.out.json).

Usage:
    python mcp-stdio-call.py calls.json

calls.json — a JSON array of calls:
    [ {"type": "tool", "name": "read_console",
       "arguments": {"action": "get", "types": ["error"], "count": 20}, "timeout": 60},
      {"type": "resource", "uri": "mcpforunity://editor/state"},
      {"type": "list_tools"},
      {"type": "list_resources"} ]

Server command defaults to `uvx --from mcpforunityserver mcp-for-unity`; override with the
UNITY_MCP_SERVER_CMD env var (a JSON array, e.g. to pin a version or add --offline).
The Unity editor must be open with its MCP bridge ready (see launch-unity.ps1/.sh).
"""
import json, os, subprocess, sys, threading, queue, time

DEFAULT_CMD = ["uvx", "--from", "mcpforunityserver", "mcp-for-unity"]
SERVER_CMD = json.loads(os.environ["UNITY_MCP_SERVER_CMD"]) if os.environ.get("UNITY_MCP_SERVER_CMD") else DEFAULT_CMD


def main():
    calls_path = sys.argv[1]
    with open(calls_path, encoding="utf-8") as f:
        calls = json.load(f)
    proc = subprocess.Popen(SERVER_CMD, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                            stderr=subprocess.PIPE, text=True, encoding="utf-8", bufsize=1)
    outq = queue.Queue()
    errbuf = []
    threading.Thread(target=lambda: [outq.put(l.strip()) for l in proc.stdout if l.strip()], daemon=True).start()
    threading.Thread(target=lambda: [errbuf.append(l) for l in proc.stderr], daemon=True).start()
    next_id = [0]

    def send(method, params=None, is_notification=False, timeout=60):
        msg = {"jsonrpc": "2.0", "method": method}
        if params is not None:
            msg["params"] = params
        if not is_notification:
            next_id[0] += 1
            msg["id"] = next_id[0]
        proc.stdin.write(json.dumps(msg) + "\n")
        proc.stdin.flush()
        if is_notification:
            return None
        deadline = time.time() + timeout
        while time.time() < deadline:
            try:
                line = outq.get(timeout=max(0.1, min(1.0, deadline - time.time())))
            except queue.Empty:
                if proc.poll() is not None:
                    raise RuntimeError(f"server exited code {proc.returncode}; stderr tail:\n" + "".join(errbuf[-40:]))
                continue
            try:
                resp = json.loads(line)
            except json.JSONDecodeError:
                continue
            if resp.get("id") == msg["id"] and ("result" in resp or "error" in resp):
                return resp
        raise TimeoutError(f"timeout ({timeout}s) waiting for {method}")

    send("initialize", {"protocolVersion": "2024-11-05", "capabilities": {},
                        "clientInfo": {"name": "unity-kit-shim", "version": "1.0"}}, timeout=180)
    send("notifications/initialized", {}, is_notification=True)

    results = []
    for c in calls:
        t = c.get("timeout", 120)
        try:
            if c["type"] == "tool":
                r = send("tools/call", {"name": c["name"], "arguments": c.get("arguments", {})}, timeout=t)
            elif c["type"] == "resource":
                r = send("resources/read", {"uri": c["uri"]}, timeout=t)
            elif c["type"] == "list_tools":
                r = send("tools/list", {}, timeout=t)
            elif c["type"] == "list_resources":
                r = send("resources/list", {}, timeout=t)
            else:
                r = {"error": f"unknown call type: {c.get('type')}"}
        except Exception as e:
            r = {"error": str(e)}
        results.append({"call": c, "result": r})
        print("=== " + json.dumps(c)[:160])
        print(json.dumps(r, indent=1)[:6000])
        sys.stdout.flush()

    with open(calls_path + ".out.json", "w", encoding="utf-8") as f:
        json.dump(results, f, indent=1)
    proc.stdin.close()
    try:
        proc.wait(timeout=10)
    except subprocess.TimeoutExpired:
        proc.kill()


if __name__ == "__main__":
    main()
