import psycopg2

for db in ['05_ticket_db', 'ticket_db']:
    print(f"\n--- {db} ticket_qr_tokens schema ---")
    conn = psycopg2.connect(f"host=localhost port=5432 dbname={db} user=postgres password=123456")
    cur = conn.cursor()
    cur.execute("SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE table_name = 'ticket_qr_tokens' ORDER BY ordinal_position;")
    for r in cur.fetchall():
        print(f"  {r[0]}: {r[1]} (nullable: {r[2]})")
    cur.execute("SELECT * FROM ticket_qr_tokens LIMIT 5;")
    print("Sample rows:", cur.fetchall())
    conn.close()
