# Seed sự kiện demo Virtual Queue

File: seed_virtual_queue_concert.sql
Database: event_db (PostgreSQL).

Chạy bằng pgAdmin Query Tool trong event_db, hoặc:

    psql -h localhost -U postgres -d event_db -v ON_ERROR_STOP=1 -f database/seed_virtual_queue_concert.sql

Đã chạy tại workspace này, event_id = 101.
Slug: anh-trai-vuot-ngan-chong-gai-2026-mo-ban-dot-2
Ngày diễn: 12/12/2026, 19:00–23:30 (Asia/Ho_Chi_Minh). Nếu chạy file sau ngày diễn, script chọn ngày ít nhất 7 ngày sau ngày chạy.

- Published; requires_virtual_queue = true; sales_frozen = false.
- Organizer #3, tài khoản organizer@ticketbox.com có sẵn.
- 6 hạng vé, 800 vé, 6 zone Standing, không tạo ghế đơn.
- Banner, poster, 4 ảnh thư viện, chính sách hoàn vé, Main Gate.
- 2 tài liệu mẫu tái sử dụng; trạng thái compliance Approved phục vụ fixture local, không phải lần duyệt pháp lý mới.
- Ảnh/tài liệu là HTTPS URL Cloudinary đã có; file SQL không nhúng ảnh nhị phân. Không cần bản ghi nguồn #99 để chạy.
- Chạy lại không tạo trùng hoặc reset sự kiện đã tồn tại.
- Không tạo đơn, thanh toán, phiên queue hoặc thay đổi eKYC/tài khoản.
- Không mở pre-queue sẵn: Admin cấu hình lúc quay, admission rate = 1.

Lưu ý kịch bản: thứ tự random không đảm bảo luôn là Customer 3 → Customer 1 → Customer 2. Mỗi lượt phải đọc thứ tự thực tế, không sửa thứ tự để giả lập ngẫu nhiên.
