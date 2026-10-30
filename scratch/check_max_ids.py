import psycopg2
import sys

sys.stdout.reconfigure(encoding='utf-8')

conn05 = psycopg2.connect("host=localhost port=5432 dbname=05_ticket_db user=postgres password=123456")
cur05 = conn05.cursor()
cur05.execute("SELECT MAX(ticket_id) FROM tickets;")
max_tkt_05 = cur05.fetchone()[0]
cur05.execute("SELECT MAX(ticket_qr_token_id) FROM ticket_qr_tokens;")
max_qr_05 = cur05.fetchone()[0]
print(f"05_ticket_db: max ticket_id = {max_tkt_05}, max qr_id = {max_qr_05}")
conn05.close()

conn_t = psycopg2.connect("host=localhost port=5432 dbname=ticket_db user=postgres password=123456")
cur_t = conn_t.cursor()
cur_t.execute("SELECT MAX(ticket_id) FROM tickets;")
max_tkt_t = cur_t.fetchone()[0]
print(f"ticket_db: max ticket_id = {max_tkt_t}")
conn_t.close()
