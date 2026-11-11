-- Run against ticket_db. Snapshot before repairing order poster URLs.
BEGIN;
UPDATE orders SET poster_url=NULL WHERE order_id=1;
UPDATE orders SET poster_url=NULL WHERE order_id=2;
UPDATE orders SET poster_url=NULL WHERE order_id=3;
UPDATE orders SET poster_url='https://res.cloudinary.com/pzccgu3t/image/upload/v1792716794/concertshield/events/4/poster.jpg' WHERE order_id=30;
COMMIT;
