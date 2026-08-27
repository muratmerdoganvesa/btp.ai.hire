"""Set AICORE_SERVICE_KEY on CF without shell interpolation, then verify JSON shape."""
from __future__ import annotations

import json
import pathlib
import subprocess
import sys

KEY_PATH = pathlib.Path(__file__).resolve().parents[1] / "aicore-service-key.json"
APPS = ("hirelens-api", "hirelens-worker")


def cf(*args: str, capture: bool = False) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["cf", *args],
        check=False,
        text=True,
        encoding="utf-8",
        errors="replace",
        capture_output=capture,
    )


def load_key() -> str:
    raw = KEY_PATH.read_text(encoding="utf-8").strip()
    obj = json.loads(raw)
    if not obj.get("clientid") or not obj.get("clientsecret"):
        raise SystemExit("local key file missing clientid/clientsecret")
    if not (obj.get("serviceurls") or {}).get("AI_API_URL"):
        raise SystemExit("local key file missing serviceurls.AI_API_URL")
    return json.dumps(obj, separators=(",", ":"))


def set_env(app: str, name: str, value: str) -> None:
    print(f"set-env {app} {name}", flush=True)
    result = cf("set-env", app, name, value)
    if result.returncode != 0:
        raise SystemExit(f"cf set-env {app} {name} failed: {result.returncode}")


def key_meta(app: str) -> dict[str, object]:
    guid_proc = cf("app", app, "--guid", capture=True)
    guid = (guid_proc.stdout or "").strip()
    if guid_proc.returncode != 0 or not guid:
        return {"ok": False, "reason": "no-guid"}
    curl = cf("curl", f"/v3/apps/{guid}/environment_variables", capture=True)
    try:
        payload = json.loads(curl.stdout or "")
    except json.JSONDecodeError:
        return {"ok": False, "reason": "env-json-invalid"}
    raw = (payload.get("var") or {}).get("AICORE_SERVICE_KEY") or ""
    meta: dict[str, object] = {
        "len": len(raw),
        "first_char": raw[:1],
        "ok": False,
    }
    try:
        obj = json.loads(raw)
    except json.JSONDecodeError:
        meta["reason"] = "not-json"
        return meta
    if not isinstance(obj, dict):
        meta["reason"] = "not-object"
        return meta
    meta["keys"] = sorted(obj.keys())
    meta["has_clientid"] = bool(obj.get("clientid"))
    urls = obj.get("serviceurls") if isinstance(obj.get("serviceurls"), dict) else {}
    meta["has_ai_api_url"] = bool(urls.get("AI_API_URL"))
    meta["ok"] = bool(meta["has_clientid"] and obj.get("clientsecret") and meta["has_ai_api_url"])
    return meta


def main() -> None:
    compact = load_key()
    for app in APPS:
        set_env(app, "AICORE_SERVICE_KEY", compact)
        set_env(app, "AICORE_DEPLOYMENT_ID", "d08b1ad950db57c6")
        set_env(app, "AICORE_CRITERIA_DEPLOYMENT_ID", "dbec6f896a57c947")
        set_env(app, "AICORE_RESOURCE_GROUP", "default")
    print("restart hirelens-api", flush=True)
    restart = cf("restart", "hirelens-api")
    if restart.returncode != 0:
        raise SystemExit(restart.returncode)
    meta = key_meta("hirelens-api")
    print("verify", json.dumps(meta), flush=True)
    if not meta.get("ok"):
        raise SystemExit("AICORE_SERVICE_KEY on CF is still invalid")


if __name__ == "__main__":
    sys.exit(main())
