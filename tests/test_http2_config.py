import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("enable_http2", Path(__file__).resolve().parents[1] / "ops/enable_http2.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class Http2ConfigTests(unittest.TestCase):
    def test_preserves_certbot_and_proxy_configuration(self):
        original = """server {
    server_name taskmanager.shape2real.in;
    listen [::]:443 ssl ipv6only=on; # managed by Certbot
    listen 443 ssl; # managed by Certbot
    location / { proxy_http_version 1.1; }
}
server { listen 80; }
"""
        expected = original.replace("ipv6only=on;", "ipv6only=on http2;").replace("listen 443 ssl;", "listen 443 ssl http2;")
        self.assertEqual(module.propose_config(original), expected)
        self.assertEqual(module.propose_config(expected), expected)

    def test_rejects_wrong_site_or_missing_tls(self):
        with self.assertRaises(ValueError):
            module.propose_config("server_name another.example;\nlisten 443 ssl;")
        with self.assertRaises(ValueError):
            module.propose_config("server_name taskmanager.shape2real.in;\nlisten 443;")
        with self.assertRaises(ValueError):
            module.propose_config("server_name taskmanager.shape2real.in;\nlisten 80;")


if __name__ == "__main__":
    unittest.main()
