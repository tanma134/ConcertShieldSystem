import psycopg2

for db in ['05_ticket_db', 'ticket_db']:
    try:
        conn = psycopg2.connect(f'host=localhost port=5432 user=postgres password=123456 dbname={db}')
        cur = conn.cursor()
        cur.execute("SELECT table_name FROM information_schema.tables WHERE table_schema='public'")
        tables = [r[0] for r in cur.fetchall()]
        print(f'{db} has {len(tables)} tables: {sorted(tables)}')
        conn.close()
    except Exception as e:
        print(f'{db} error: {e}')
