import urllib.request
import ssl

ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE

for url in ["http://localhost:5288/swagger/index.html", "https://localhost:7330/swagger/index.html", "https://localhost:7164/api/checkin/terminal/assignment"]:
    try:
        req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
        res = urllib.request.urlopen(req, context=ctx, timeout=3)
        print(f"{url}: UP, status {res.status}")
    except Exception as e:
        print(f"{url}: DOWN, error: {e}")
