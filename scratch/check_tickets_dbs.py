import psycopg2
import sys

sys.stdout.reconfigure(encoding='utf-8')

for db in ['05_ticket_db', 'ticket_db']:
    try:
        conn = psycopg2.connect(f"host=localhost port=5432 dbname={db} user=postgres password=123456")
        cur = conn.cursor()
        print(f"\n=== Database: {db} ===")
        cur.execute("SELECT table_name FROM information_schema.tables WHERE table_schema='public';")
        tables = [r[0] for r in cur.fetchall()]
        print("Tables:", tables)

        if 'orders' in tables:
            cur.execute("SELECT order_id, customer_id, email, full_name, event_name, status FROM orders;")
            print("Orders:", cur.fetchall())

        if 'tickets' in tables:
            cur.execute("SELECT ticket_id, order_id, event_id, ticket_code, owner_name, status, is_checked_in FROM tickets;")
            print("Tickets:", cur.fetchall())
            
        conn.close()
    except Exception as e:
        print(f"Error {db}: {e}")
