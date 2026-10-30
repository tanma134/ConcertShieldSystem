import psycopg2

conn = psycopg2.connect("host=localhost port=5432 dbname=ticket_db user=postgres password=123456")
cur = conn.cursor()
cur.execute("SELECT order_id, customer_id, event_id, event_name, email, status FROM orders WHERE event_id = 20;")
print("Event 20 orders in ticket_db:", cur.fetchall())
cur.execute("SELECT ticket_id, order_id, event_id, ticket_code, owner_name, status, is_checked_in FROM tickets WHERE event_id = 20;")
print("Event 20 tickets in ticket_db:", cur.fetchall())
conn.close()
