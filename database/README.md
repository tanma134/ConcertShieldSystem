# ConcertShield — SQL Database Initialization & Bootstrap Guide

Toàn bộ các file SQL dưới đây đã được kiểm thử chạy lại từ đầu (from scratch) trên các cơ sở dữ liệu trắng và đảm bảo thành công 100%, không phát sinh lỗi.

### 1. Danh sách file khởi tạo theo Database

| Database | File SQL Khởi Tạo | Mô Tả & Nội Dung |
| :--- | :--- | :--- |
| `authentication_db` | `authentication_db.sql` | Tài khoản, vai trò (Admin, Customer, Organizer, Staff), eKYC, avatar, refresh token. |
| `event_db` | `event_db.sql` | Sự kiện, sơ đồ chỗ ngồi, khu vực vé, quy tắc giá động, chính sách hoàn trả, hồ sơ pháp lý, phân công staff. |
| `ticket_db` | `ticket_db.sql` | Đơn hàng, chi tiết đơn, vé, mã QR, yêu cầu hoàn vé, đổi lịch và vé bị ảnh hưởng. |
| `payment_db` | `payment_db.sql` | Giao dịch thanh toán, mã giảm giá (voucher), lịch sử sử dụng, yêu cầu hoàn tiền, biên bản đối soát. |
| `notification_db` | `notification_db.sql` | Bảng thông báo in-app thời gian thực. |
| `incident_db` | `incident_db.sql` | Quản lý báo cáo sự cố hiện trường, tệp bằng chứng và dòng thời gian cập nhật. |
| `admin_db` | `admin_db.sql` | Support cases, ghi chú nội bộ, audit logs, phân tích cảnh báo gian lận và rủi ro. |
| `checkin_db` | `checkin_db.sql` | Chức danh soát vé (validator roles), cổng sự kiện, log quét vé. |

### 2. File Seed & Dữ Liệu Bổ Sung

- `seed_real_concerts_2026.sql`: Seed các concert thị trường thật năm 2026 (Annyeong HCMC, Hoàng Dũng, Mỹ Tâm, Vương Bình, Mason Nguyễn) kèm sơ đồ ghế và vé. Chạy trên `event_db`.
- `open_mason_ticket_sales.sql`: Mở bán vé sự kiện Mason Nguyễn ngay lập tức để test luồng mua vé. Chạy trên `event_db`.
- `fix_event_image_urls.sql`: Đồng bộ URL ảnh Cloudinary đã xác thực cho các sự kiện trên `event_db`.
- `fix_ticket_order_image_urls.sql`: Đồng bộ URL ảnh poster của đơn hàng trên `ticket_db`.

### 3. Quy trình chạy lại từ đầu (Khởi tạo DB trắng)

Khi muốn xóa và khởi tạo lại toàn bộ từ đầu, thực hiện tuần tự qua PowerShell:

```powershell
$env:PGPASSWORD = 'your_postgres_password'

# 1. Tạo mới các database
@('authentication_db', 'event_db', 'ticket_db', 'payment_db', 'notification_db', 'incident_db', 'admin_db', 'checkin_db', 'queue_db') | ForEach-Object {
    psql -U postgres -h 127.0.0.1 -p 5432 -d postgres -c "DROP DATABASE IF EXISTS $_;"
    psql -U postgres -h 127.0.0.1 -p 5432 -d postgres -c "CREATE DATABASE $_;"
}

# 2. Khởi tạo schema & dữ liệu cơ sở
psql -U postgres -h 127.0.0.1 -p 5432 -d authentication_db -f "database\authentication_db.sql"
psql -U postgres -h 127.0.0.1 -p 5432 -d event_db -f "database\event_db.sql"
psql -U postgres -h 127.0.0.1 -p 5432 -d ticket_db -f "database\ticket_db.sql"
psql -U postgres -h 127.0.0.1 -p 5432 -d payment_db -f "database\payment_db.sql"
psql -U postgres -h 127.0.0.1 -p 5432 -d notification_db -f "database\notification_db.sql"
psql -U postgres -h 127.0.0.1 -p 5432 -d incident_db -f "database\incident_db.sql"
psql -U postgres -h 127.0.0.1 -p 5432 -d admin_db -f "database\admin_db.sql"
psql -U postgres -h 127.0.0.1 -p 5432 -d checkin_db -f "database\checkin_db.sql"

# 3. Seed concert mẫu và chuẩn hóa ảnh
psql -U postgres -h 127.0.0.1 -p 5432 -d event_db -f "database\seed_real_concerts_2026.sql"
psql -U postgres -h 127.0.0.1 -p 5432 -d event_db -f "database\open_mason_ticket_sales.sql"
psql -U postgres -h 127.0.0.1 -p 5432 -d event_db -f "database\fix_event_image_urls.sql"
psql -U postgres -h 127.0.0.1 -p 5432 -d ticket_db -f "database\fix_ticket_order_image_urls.sql"
```

### 4. Bản sao lưu dự phòng (Backups)

Thư mục `database/backups_pre_recreate/` chứa bản sao lưu nhị phân đầy đủ (`.dump`) của tất cả cơ sở dữ liệu trước khi thực hiện thao tác. Khôi phục nhanh bằng lệnh:
```powershell
pg_restore -U postgres -h 127.0.0.1 -p 5432 -d <database_name> -c "database\backups_pre_recreate\<database_name>.dump"
```
