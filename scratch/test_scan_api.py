import urllib.request
import json

# Grab a real QR token from ticket_db
import psycopg2

conn = psycopg2.connect('host=localhost port=5432 user=postgres password=123456 dbname=ticket_db')
cur = conn.cursor()
cur.execute("SELECT t.ticket_code, qr.qr_token, t.event_id FROM tickets t JOIN ticket_qr_tokens qr ON t.ticket_id = qr.ticket_id WHERE t.is_checked_in = false LIMIT 1")
row = cur.fetchone()
conn.close()

if not row:
    print("No unchecked-in ticket with QR token found.")
else:
    ticket_code, qr_token, event_id = row
    print(f"Testing scan with TicketCode: {ticket_code}, QrToken: {qr_token}, EventId: {event_id}")

    payload = json.dumps({
        "qrPayload": qr_token,
        "eventId": event_id,
        "gateId": "GATE_A"
    }).encode('utf-8')

    req = urllib.request.Request(
        'http://localhost:5288/api/checkin/scan-qr',
        data=payload,
        headers={'Content-Type': 'application/json'}
    )

    try:
        with urllib.request.urlopen(req) as resp:
            data = resp.read().decode('utf-8')
            print("Scan Response Status:", resp.status)
            print("Scan Response Body:", data)
    except urllib.error.HTTPError as e:
        print("HTTP Error:", e.code, e.read().decode('utf-8'))
    except Exception as e:
        print("Error:", e)
