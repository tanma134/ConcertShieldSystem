import psycopg2

for db in ['05_ticket_db', 'ticket_db']:
    conn = psycopg2.connect(f'host=localhost port=5432 user=postgres password=123456 dbname={db}')
    cur = conn.cursor()
    cur.execute("SELECT ticket_id, qr_token, expires_at, is_used, is_revoked FROM ticket_qr_tokens ORDER BY ticket_qr_token_id DESC LIMIT 5")
    rows = cur.fetchall()
    print(f"--- {db} (top 5 qr tokens) ---")
    for r in rows:
        print(r)
    conn.close()
