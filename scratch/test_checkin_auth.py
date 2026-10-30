import urllib.request
import ssl
import json
import time
import hmac
import hashlib
import base64

def base64url_encode(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).decode('utf-8').rstrip('=')

# Real token signed by AuthenticationAPI secret:
secret = "SuperSecretKey_MustBe32CharsOrMore!@#".encode('utf-8')
header = {"alg": "HS256", "typ": "JWT"}
payload = {
    "sub": "5",
    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier": "5",
    "email": "staff@ticketbox.com",
    "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "Staff",
    "iss": "AuthenticationAPI",
    "aud": "AuthenticationAPIUsers",
    "exp": int(time.time()) + 3600
}
encoded_header = base64url_encode(json.dumps(header).encode('utf-8'))
encoded_payload = base64url_encode(json.dumps(payload).encode('utf-8'))
to_sign = f"{encoded_header}.{encoded_payload}".encode('utf-8')
signature = base64url_encode(hmac.new(secret, to_sign, hashlib.sha256).digest())
token = f"{encoded_header}.{encoded_payload}.{signature}"

ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE

# Test CheckinAPI directly on http://localhost:5288/api/checkin/scan
url = "http://localhost:5288/api/checkin/scan"
scan_req = json.dumps({
    "qrPayload": "QR-ACOUSTIC-VIP-011",
    "gateId": "GATE_A",
    "eventId": 20
}).encode('utf-8')

req = urllib.request.Request(url, data=scan_req, headers={
    'Authorization': f'Bearer {token}',
    'Content-Type': 'application/json',
    'User-Agent': 'Mozilla/5.0'
})

try:
    with urllib.request.urlopen(req, timeout=5) as res:
        print("Status:", res.status)
        print("Body:", res.read().decode('utf-8'))
except urllib.error.HTTPError as he:
    print(f"HTTPError {he.code}: {he.read().decode('utf-8')}")
except Exception as e:
    print("Error:", e)
