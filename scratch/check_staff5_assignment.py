import psycopg2

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
    print(" ", r)
conn.close()
