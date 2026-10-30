import urllib.request
import ssl
import json

ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE

# 1. First let's login as customer@ticketbox.com to get a real valid JWT
login_data = json.dumps({
    "email": "customer@ticketbox.com",
    "password": "Customer@123"
}).encode('utf-8')

token = None
# Try logging into Gateway or Auth service
for auth_url in ["https://localhost:7164/api/auth/login", "https://localhost:7234/api/auth/login"]:
    try:
        req = urllib.request.Request(auth_url, data=login_data, headers={'Content-Type': 'application/json', 'User-Agent': 'Mozilla/5.0'})
        with urllib.request.urlopen(req, context=ctx, timeout=5) as res:
            resp_body = json.loads(res.read().decode('utf-8'))
            print(f"Login success at {auth_url}:", resp_body)
            token = resp_body.get('data', {}).get('accessToken') or resp_body.get('accessToken')
            if token:
                break
    except Exception as e:
        print(f"Login failed at {auth_url}: {e}")

if not token:
    print("Could not get token!")
else:
    print(f"Got JWT Token: {token[:25]}...")
    
    # 2. Test /api/Tickets/my-tickets on Gateway (7164)
    for test_url in ["https://localhost:7164/api/Tickets/my-tickets", "https://localhost:7268/api/Tickets/my-tickets"]:
        try:
            req = urllib.request.Request(test_url, headers={
                'Authorization': f'Bearer {token}',
                'Content-Type': 'application/json',
                'User-Agent': 'Mozilla/5.0'
            })
            with urllib.request.urlopen(req, context=ctx, timeout=5) as res:
                print(f"Call {test_url}: Status {res.status}")
                body = res.read().decode('utf-8')
                print(f"Response: {body[:300]}")
        except urllib.error.HTTPError as he:
            print(f"Call {test_url} HTTPError: {he.code} - {he.read().decode('utf-8', errors='ignore')}")
        except Exception as e:
            print(f"Call {test_url} Error: {e}")
