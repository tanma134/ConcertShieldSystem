import psycopg2

sql = "ALTER TABLE public.order_details ADD COLUMN IF NOT EXISTS seat_ids jsonb NULL;"

for db in ['ticket_db', '05_ticket_db']:
    try:
        conn = psycopg2.connect(f"host=localhost port=5432 dbname={db} user=postgres password=123456")
        cur = conn.cursor()
        cur.execute(sql)
        conn.commit()
        print(f"Added seat_ids to {db}.order_details")
        conn.close()
    except Exception as e:
        print(f"Error {db}: {e}")
