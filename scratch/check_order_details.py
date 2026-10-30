import psycopg2

conn = psycopg2.connect("host=localhost port=5432 dbname=ticket_db user=postgres password=123456")
cur = conn.cursor()
cur.execute("SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE table_name = 'order_details' ORDER BY ordinal_position;")
for r in cur.fetchall():
    print(r)
cur.execute("SELECT * FROM order_details WHERE order_id = 8 LIMIT 5;")
print("Sample order_details for order 8:", cur.fetchall())
conn.close()
