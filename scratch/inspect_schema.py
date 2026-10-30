import psycopg2

for db in ['05_ticket_db', 'ticket_db']:
    print(f"\n--- {db} schema ---")
    conn = psycopg2.connect(f"host=localhost port=5432 dbname={db} user=postgres password=123456")
    cur = conn.cursor()
    cur.execute("SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE table_name = 'tickets' ORDER BY ordinal_position;")
    print("Tickets columns:")
    for r in cur.fetchall():
        print(f"  {r[0]}: {r[1]} (nullable: {r[2]})")
    cur.execute("SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE table_name = 'orders' ORDER BY ordinal_position;")
    print("Orders columns:")
    for r in cur.fetchall():
        print(f"  {r[0]}: {r[1]} (nullable: {r[2]})")
    conn.close()
