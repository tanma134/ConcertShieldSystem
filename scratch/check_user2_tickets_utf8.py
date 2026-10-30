import psycopg2
import sys

sys.stdout.reconfigure(encoding='utf-8')

for db in ['05_ticket_db', 'ticket_db']:
    print(f"\n--- {db} ---")
    conn = psycopg2.connect(f"host=localhost port=5432 dbname={db} user=postgres password=123456")
    cur = conn.cursor()
    cur.execute("""
        SELECT t.ticket_id, t.order_id, t.event_id, t.owner_user_id, t.ticket_code, t.owner_name, t.status, o.email, o.customer_id, o.event_name
        FROM tickets t
        JOIN orders o ON t.order_id = o.order_id
        WHERE o.customer_id = 2 OR t.owner_user_id = 2 OR o.email = 'customer@ticketbox.com'
        ORDER BY t.ticket_id DESC LIMIT 15;
    """)
    rows = cur.fetchall()
    print(f"Total matching tickets in {db}: {len(rows)}")
    for r in rows:
        print(" ", r)
    conn.close()
