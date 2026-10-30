import psycopg2

conn = psycopg2.connect("host=localhost port=5432 dbname=ticket_db user=postgres password=123456")
cur = conn.cursor()
cur.execute("SELECT ticket_id, order_id, event_id, ticket_code, owner_user_id, owner_name, status, is_checked_in FROM tickets WHERE order_id = 8;")
print("Order 8 tickets in ticket_db:")
for r in cur.fetchall():
    print(" ", r)
conn.close()
