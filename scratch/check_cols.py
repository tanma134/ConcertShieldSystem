import psycopg2

conn = psycopg2.connect('host=localhost port=5432 user=postgres password=123456 dbname=ticket_db')
cur = conn.cursor()
cur.execute("SELECT column_name FROM information_schema.columns WHERE table_name='orders'")
print("orders columns in ticket_db:", [r[0] for r in cur.fetchall()])
cur.execute("SELECT column_name FROM information_schema.columns WHERE table_name='tickets'")
print("tickets columns in ticket_db:", [r[0] for r in cur.fetchall()])
conn.close()
