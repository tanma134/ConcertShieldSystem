import psycopg2
import sys

sys.stdout.reconfigure(encoding='utf-8')

conn = psycopg2.connect("host=localhost port=5432 dbname=event_db user=postgres password=123456")
cur = conn.cursor()
cur.execute("""
    SELECT s.event_staff_id, s.event_id, s.staff_user_id, s.gate_name, s.is_active, s.assigned_at, e.title
    FROM event_staff s
    JOIN events e ON s.event_id = e.event_id
    WHERE s.staff_user_id = 5;
""")
print("Staff 5 assignments in event_db:")
for r in cur.fetchall():
    print(f"  ID: {r[0]} | Event {r[1]}: '{r[6]}' | Gate: '{r[3]}' | is_active: {r[4]}")

cur.execute("SELECT event_id, title, status, is_deleted FROM events WHERE event_id IN (20, 21);")
print("\nEvents 20 and 21 in event_db:")
for r in cur.fetchall():
    print(f"  Event {r[0]}: '{r[1]}' | status: {r[2]} | deleted: {r[3]}")

conn.close()
