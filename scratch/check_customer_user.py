import psycopg2
import sys

sys.stdout.reconfigure(encoding='utf-8')

try:
    conn = psycopg2.connect("host=localhost port=5432 dbname=01_auth_db user=postgres password=123456")
    cur = conn.cursor()
    cur.execute("SELECT user_id, email, full_name, phone_number, role FROM users WHERE email ILIKE '%customer%' OR role ILIKE '%Customer%';")
    rows = cur.fetchall()
    print("Found users in 01_auth_db:")
    for r in rows:
        print(r)
    conn.close()
except Exception as e:
    print(f"Error 01_auth_db: {e}")

try:
    conn = psycopg2.connect("host=localhost port=5432 dbname=05_ticket_db user=postgres password=123456")
    cur = conn.cursor()
    cur.execute("SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'orders';")
    print("\nOrders columns in 05_ticket_db:")
    for r in cur.fetchall():
        print(r[0], r[1])
    conn.close()
except Exception as e:
    print(f"Error 05_ticket_db: {e}")
