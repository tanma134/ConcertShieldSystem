# Concert Anh Trai Vượt Ngàn Chông Gai 2026 – Day 3 & Day 4

File SQL: seed_concert_atvncg_day_3_4.sql
Chạy trong PostgreSQL database event_db bằng pgAdmin Query Tool hoặc:

    psql -h localhost -U postgres -d event_db -v ON_ERROR_STOP=1 -f database/seed_concert_atvncg_day_3_4.sql

Dữ liệu lấy từ sự kiện #56: thông tin, mô tả, địa điểm, banner, poster, 9 ảnh thư viện, 6 hạng vé, chính sách hoàn và tài liệu hiện có.
Sân khấu chữ T, 6 zone Standing lấy đúng bản demo #99, không sinh ghế đơn. Tổng 800 vé theo bản demo đã quay: X-VIP 50, S-VIP 100, VIP 100, Hỏa Ca 200, Gai Con 200, Tinh Hoa 150. Tất cả sold_quantity = 0.
Ngày bắt đầu 05/12/2026 19:00, kết thúc 06/12/2026 23:00, múi giờ Việt Nam. Nếu chạy sau lịch này, ngày mới cách ngày chạy ít nhất 7 ngày.
Sự kiện fixture Published, compliance Approved để tiếp tục flow đổi lịch và hoàn vé. Đây là trạng thái seed local, không phải lần duyệt thực tế mới. Virtual Queue tắt.
Ảnh và tài liệu sử dụng URL Cloudinary có sẵn, không nhúng file nhị phân. Không cần sự kiện #56 tồn tại khi import.
Không sao chép đơn mua, giao dịch thanh toán, vé đã thu hồi hoặc lịch sử đổi lịch.
Nếu slug sự kiện đã tồn tại, script không tạo trùng và không ghi đè bản ghi cũ.
File này mới được xuất, chưa import thêm vào DB hiện tại.

Giá demo: Tinh Hoa 800.000đ; Gai Con 1.200.000đ; Hỏa Ca 1.800.000đ; VIP 2.500.000đ; S-VIP 3.500.000đ; X-VIP 8.000.000đ.
Slug mới: concert-anh-trai-vuot-ngan-chong-gai-2026-day-3-day-4-standing. Import tạo sự kiện riêng, không ghi đè #56 đã có lịch sử đơn/vé.

