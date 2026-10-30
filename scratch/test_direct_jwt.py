import urllib.request
import ssl
import json
import time
import hmac
import hashlib
import base64

def base64url_encode(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).decode('utf-8').rstrip('=')

# Generate JWT for user_id = 2, customer@ticketbox.com
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

print(f"Generated JWT: {jwt_token[:30]}...")

ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE

for url in ["https://localhost:7164/api/Tickets/my-tickets", "https://localhost:7268/api/Tickets/my-tickets"]:
    try:
        req = urllib.request.Request(url, headers={
            'Authorization': f'Bearer {jwt_token}',
            'Content-Type': 'application/json',
            'User-Agent': 'Mozilla/5.0'
        })
        with urllib.request.urlopen(req, context=ctx, timeout=5) as res:
            print(f"\nResponse from {url}: HTTP {res.status}")
            data = json.loads(res.read().decode('utf-8'))
            print("Success:", data.get("success"))
            tickets = data.get("data", [])
            print(f"Total tickets returned: {len(tickets)}")
            for t in tickets[:3]:
                print(" ", t.get("ticketId"), t.get("eventName"), t.get("ticketTypeName"), t.get("status"))
    except urllib.error.HTTPError as he:
        print(f"HTTPError on {url} {he.code}: {he.read().decode('utf-8')[:300]}")
    except Exception as e:
        print(f"Error on {url}: {e}")
