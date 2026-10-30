import psycopg2

for db in ['05_ticket_db', 'ticket_db']:
    print(f"\n--- {db} ---")
    conn = psycopg2.connect(f"host=localhost port=5432 dbname={db} user=postgres password=123456")
    cur = conn.cursor()
    cur.execute("""
        SELECT t.ticket_id, t.order_id, t.event_id, t.owner_user_id, t.ticket_code, t.owner_name, t.ticket_type_name, t.status, o.email, o.customer_id
        FROM tickets t
        JOIN orders o ON t.order_id = o.order_id
        WHERE o.customer_id = 2 OR t.owner_user_id = 2 OR o.email = 'customer@ticketbox.com';
    """)
    rows = cur.fetchall()
    print(f"Total matching tickets in {db}: {len(rows)}")
    for r in rows:
        print(" ", r)
    conn.close()
