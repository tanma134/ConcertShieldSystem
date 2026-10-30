import urllib.request
import ssl

ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE

services = [
    ("GatewayAPI", "https://localhost:7164"),
    ("TicketAPI", "https://localhost:7268"),
    ("CheckinAPI", "https://localhost:7001"),
    ("AuthAPI", "https://localhost:7234"),
    ("EventAPI", "https://localhost:7289"),
]

for name, url in services:
    try:
        req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
        res = urllib.request.urlopen(req, context=ctx, timeout=2)
        print(f"{name} ({url}): UP, status {res.status}")
    except Exception as e:
        print(f"{name} ({url}): DOWN or error: {e}")
