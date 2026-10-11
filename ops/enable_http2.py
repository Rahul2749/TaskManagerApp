#!/usr/bin/env python3
"""Enable HTTP/2 for the existing TaskManager site on Nginx 1.24.

Run as root on the VPS after reviewing the performance report.
"""
import argparse
from datetime import datetime, timezone
import os
from pathlib import Path
import re
import shutil
import subprocess


def propose_config(contents: str) -> str:
    if not re.search(r"^\s*server_name\s+taskmanager\.shape2real\.in\s*;", contents, re.M):
        raise ValueError("This is not the expected TaskManager site configuration.")
    listeners = 0

    def replace(match):
        nonlocal listeners
        directive = match.group(1)
        if not re.search(r"(?:^|\s)(?:\[::\]:)?443(?:\s|$)", directive):
            return match.group(0)
        if not re.search(r"\bssl\b", directive):
            raise ValueError("Port 443 listener is missing ssl.")
        listeners += 1
        if re.search(r"\bhttp2\b", directive):
            return match.group(0)
        return directive.rstrip() + " http2;"

    updated = re.sub(r"^(\s*listen\s+[^;\n]+);", replace, contents, flags=re.M)
    if not listeners:
        raise ValueError("No HTTPS listeners found.")
    return updated


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", default="/etc/nginx/sites-available/taskmanager")
    parser.add_argument("--apply", action="store_true", help="Write, validate and reload; otherwise show proposed listener lines")
    args = parser.parse_args()
    target = Path(args.config).resolve(strict=True)
    original = target.read_text()
    updated = propose_config(original)
    if not args.apply:
        print("Proposed HTTPS listeners:")
        for line in updated.splitlines():
            if re.match(r"\s*listen\s+.*443", line):
                print(line)
        return
    if os.geteuid() != 0:
        raise SystemExit("Run with sudo/root to apply.")
    version = subprocess.run(["nginx", "-V"], capture_output=True, text=True, check=True).stderr
    match = re.search(r"nginx/(\d+)\.(\d+)\.(\d+)", version)
    if not match or tuple(map(int, match.groups())) >= (1, 25, 1):
        raise SystemExit("This helper targets Nginx before 1.25.1. Newer versions use http2 on; see the report.")
    if "--with-http_v2_module" not in version:
        raise SystemExit("Nginx was built without HTTP/2 support.")
    if updated == original:
        print("HTTP/2 is already enabled; no change.")
        return
    backup = target.with_name(target.name + ".http2-backup-" + datetime.now(timezone.utc).strftime("%Y%m%d%H%M%S%f"))
    shutil.copy2(target, backup)
    try:
        target.write_text(updated)
        subprocess.run(["nginx", "-t"], check=True)
        subprocess.run(["systemctl", "reload", "nginx"], check=True)
    except Exception:
        target.write_text(original)
        subprocess.run(["nginx", "-t"], check=False)
        subprocess.run(["systemctl", "reload", "nginx"], check=False)
        raise
    print(f"HTTP/2 enabled. Backup: {backup}")


if __name__ == "__main__":
    main()
