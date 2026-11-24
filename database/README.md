# ConcertShield — SQL consolidated by database

These are consolidated *source scripts*, not a verified migration against your live PostgreSQL databases. Back up every database before applying.

| File | Contents | Caution |
| --- | --- | --- |
| event_db.sql | event_db.sql + ALL_IN_ONE/1_event_db_ALL.sql | Original event seed contains data-changing statements; review before applying to an existing DB |
| ticket_db.sql | 05_ticket_db_FULL_RERUN_SAFE.sql + ALL_IN_ONE/2_ticket_db_ALL.sql | Leading ROLLBACK removed; includes demo/test data updates; review before running |
| payment_db.sql | ALL_IN_ONE/4_payment_db_ALL.sql | **Only payment_refunds extension**. No base payment schema found in ZIP. Do not treat as complete payment DB bootstrap |
| notification_db.sql | ALL_IN_ONE/3_notification_db_ALL.sql | Notification schema extension |
| authentication_db.sql | init_authen_db.sql + avatar migration | **DROP TABLE statements: destructive** |
| checkin_db.sql | 07_checkin_db.sql | Existing checkin script |
| create_queue_tables.sql | Queue configurations, sessions, and abuse alerts tables | Run after connecting to `queue_db` |
| create_review_tables.sql | Reviews and review replies tables | Run after connecting to `review_db` |
| seed_queue_db.sql | Sample queue configuration, terminal sessions, and abuse alert | Requires event 18; sample sessions are not synchronized to Redis |
| seed_review_db.sql | Sample reviews and admin replies | Requires events 1–3 and users 1–3 from the existing seed data |

No cross-database foreign keys are introduced. The mixed database/011_return_review_refund.sql is already distributed among event/ticket/payment via the ALL_IN_ONE scripts, not appended twice.

Original migrations remain in the original source ZIP. These scripts should be run **against their respective databases**, not all in one PostgreSQL database. They have NOT been run against a live database and have NOT been proven fully rerunnable. Review transaction and seed sections first.

Missing source: payment_db base creation SQL; only refund table migration is available. Dedicated Queue/Review table initialization scripts are listed below.

## Khởi tạo `queue_db` và `review_db`

Tạo database (chỉ chạy nếu database chưa tồn tại), sau đó tạo các bảng:

```powershell
psql -h localhost -p 5432 -U postgres -d postgres -c "CREATE DATABASE queue_db;"
psql -h localhost -p 5432 -U postgres -d postgres -c "CREATE DATABASE review_db;"
psql -h localhost -p 5432 -U postgres -d queue_db -f "E:\Study Case\Do An\New5\ConcertShieldSystem\database\create_queue_tables.sql"
psql -h localhost -p 5432 -U postgres -d review_db -f "E:\Study Case\Do An\New5\ConcertShieldSystem\database\create_review_tables.sql"
```

Các file tạo bảng dùng `IF NOT EXISTS` và có thể chạy lại trên database đã tạo.

## Thêm dữ liệu mẫu Queue/Review

Sau khi đã tạo database và bảng, có thể chạy:

```powershell
psql -h localhost -p 5432 -U postgres -d queue_db -f "E:\Study Case\Do An\New5\ConcertShieldSystem\database\seed_queue_db.sql"
psql -h localhost -p 5432 -U postgres -d review_db -f "E:\Study Case\Do An\New5\ConcertShieldSystem\database\seed_review_db.sql"
```

Các file seed không xóa dữ liệu và có thể chạy lại. `seed_queue_db.sql` chỉ tạo dữ liệu mẫu trong PostgreSQL; trạng thái queue thực tế còn phụ thuộc Redis. Các ID mẫu dựa trên seed sự kiện và tài khoản hiện có.

## Thêm 2.000 tài khoản mẫu

Chạy seed sau trên `authentication_db` đã được khởi tạo:

```powershell
psql -h localhost -p 5432 -U postgres -d authentication_db -f "E:\Study Case\Do An\New5\ConcertShieldSystem\database\seed_sample_users.sql"
```

Seed tạo/cập nhật `sample-user-0001@example.test` đến `sample-user-2000@example.test`, gán role `Customer` và có thể chạy lại an toàn. Tất cả dùng mật khẩu `Customer@123`; chỉ dùng các tài khoản này cho môi trường mẫu/kiểm thử.
