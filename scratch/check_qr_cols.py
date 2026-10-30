import psycopg2

conn = psycopg2.connect('host=localhost port=5432 user=postgres password=123456 dbname=ticket_db')
cur = conn.cursor()
cur.execute("SELECT column_name FROM information_schema.columns WHERE table_name='ticket_qr_tokens'")
print("ticket_qr_tokens in ticket_db:", [r[0] for r in cur.fetchall()])
conn.close()
