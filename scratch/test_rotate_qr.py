import urllib.request
import ssl
import json
import time
import hmac
import hashlib
import base64

def base64url_encode(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).decode('utf-8').rstrip('=')

header = {"alg": "HS256", "typ": "JWT"}
payload = {
    "sub": "2",
    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier": "2",
    "email": "customer@ticketbox.com",
    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress": "customer@ticketbox.com",
    "fullName": "Test Customer",
    "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "Customer",
    "iss": "AuthenticationAPI",
    "aud": "AuthenticationAPIUsers",
    "exp": int(time.time()) + 3600
}

secret = "SuperSecretKey_MustBe32CharsOrMore!@#".encode('utf-8')
encoded_header = base64url_encode(json.dumps(header).encode('utf-8'))
encoded_payload = base64url_encode(json.dumps(payload).encode('utf-8'))
to_sign = f"{encoded_header}.{encoded_payload}".encode('utf-8')
signature = base64url_encode(hmac.new(secret, to_sign, hashlib.sha256).digest())
jwt_token = f"{encoded_header}.{encoded_payload}.{signature}"

ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE

url = "https://localhost:7268/api/Orders/tickets/207/qr/rotate"
req = urllib.request.Request(url, method='POST', headers={
    'Authorization': f'Bearer {jwt_token}',
    'Content-Type': 'application/json',
    'User-Agent': 'Mozilla/5.0'
})

try:
    print(f"Calling {url}...")
    with urllib.request.urlopen(req, context=ctx, timeout=10) as res:
        print("Status:", res.status)
        print("Body:", res.read().decode('utf-8'))
except urllib.error.HTTPError as he:
    print(f"HTTPError {he.code}: {he.read().decode('utf-8')}")
except Exception as e:
    print("Error:", e)
