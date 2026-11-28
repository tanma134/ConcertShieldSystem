\getenv restore_existing_databases RESTORE_EXISTING_DATABASES
\if :restore_existing_databases
\echo "Skipping repository schema bootstrap; existing database dumps will be restored."
\else
\connect authentication_db
\i /docker-bootstrap/authentication_db.sql

\connect event_db
\i /docker-bootstrap/event_db.sql

\connect ticket_db
\i /docker-bootstrap/ticket_db.sql

\connect payment_db
\i /docker-bootstrap/payment_db.sql

\connect notification_db
\i /docker-bootstrap/notification_db.sql

\connect incident_db
\i /docker-bootstrap/incident_db.sql

\connect admin_db
\i /docker-bootstrap/admin_db.sql

\connect checkin_db
\i /docker-bootstrap/checkin_db.sql

\connect queue_db
\i /docker-bootstrap/create_queue_tables.sql

\connect review_db
\i /docker-bootstrap/create_review_tables.sql
\endif
