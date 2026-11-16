# ConcertShield System — Bộ Workflow Nghiệp Vụ

Tài liệu mô tả 12 workflow bổ sung cho 4 workflow đã có, bao phủ trọn vẹn 55 chức năng của hệ thống.

| Nhóm | Workflow | Số chức năng |
|---|---|---|
| Đã có | W1 Luồng mua vé · W2 Ticket Refund Flow · W3 Fraud Prevention Flow · W4 Event Change Flow | 19 bước hệ thống |
| Bổ sung | WF-05 → WF-16 | 55 chức năng |

Quy ước:
- `[Actor]` — bước do người dùng thực hiện.
- `[System]` — bước hệ thống tự xử lý.
- Mỗi workflow gồm: Actor · Trigger · Precondition · Luồng chính · Nhánh phụ · Postcondition · Chức năng phủ.

---

## WF-05 — Quản lý Concert ở trạng thái Nháp

**Actor:** Organizer
**Trigger:** Organizer muốn chuẩn bị một concert mới.
**Precondition:** Tài khoản đã được duyệt làm Organizer.

**Luồng chính**
1. [Organizer] **View Concert List** — mở danh sách concert thuộc quyền quản lý, xem nhanh trạng thái từng concert (Nháp / Chờ duyệt / Đã công bố / Bị từ chối / Đã huỷ / Tạm hoãn) và số vé đã bán.
2. [Organizer] **Create Concert Event** — tạo concert mới với thông tin cơ bản: tên, địa điểm, thành phố, thời gian bắt đầu và kết thúc, mô tả, ảnh poster và banner.
3. [System] Sinh mã định danh công khai cho concert, đặt trạng thái **Nháp**, ghi nhận người tạo và thời điểm.
4. [Organizer] **View Concert Details** — xem toàn bộ thông tin đã nhập, kèm danh sách hạng vé, sơ đồ ghế và chính sách hiện có (nếu đã cấu hình).
5. [Organizer] **Edit Concert Information** — chỉnh sửa thông tin cơ bản, thay poster/banner, cập nhật thời gian.
6. [System] Chỉ cho phép sửa khi concert ở trạng thái **Nháp** hoặc **Bị từ chối**; các trạng thái khác phải đi qua WF-09.
7. [Organizer] **Delete Draft Concert** — xoá concert nháp không còn dùng; concert được ẩn đi nhưng vẫn khôi phục được trong thời gian lưu trữ.
8. [Organizer] Chuyển sang WF-06 để dựng sơ đồ ghế.

**Nhánh phụ**
- 5a. Concert đã **Chờ duyệt** hoặc **Đã công bố** → hệ thống chặn sửa, hướng dẫn dùng WF-09 (gửi yêu cầu thay đổi).
- 7a. Concert đã phát sinh đơn hàng → không cho xoá; phải huỷ concert theo WF-09.

**Postcondition:** Concert tồn tại ở trạng thái **Nháp**, thông tin cơ bản hợp lệ, sẵn sàng cấu hình ghế và vé.
**Chức năng phủ:** View Concert List · View Concert Details · Create Concert Event · Edit Concert Information · Delete Draft Concert.

---

## WF-06 — Thiết kế sơ đồ chỗ ngồi

**Actor:** Organizer (Admin có thể tạo bộ mẫu dùng chung)
**Trigger:** Concert nháp cần sơ đồ ghế trước khi cấu hình hạng vé.
**Precondition:** Concert tồn tại ở trạng thái **Nháp** hoặc **Bị từ chối**.

**Luồng chính**
1. [Organizer] Xem sơ đồ hiện tại của concert (nếu đã có) để quyết định dựng mới hay dùng mẫu.
2. [Organizer] Chọn cách dựng sơ đồ:
   - dựng từ đầu, hoặc
   - **Apply Seating Template** — chọn một bộ mẫu có sẵn, hệ thống dựng lại toàn bộ khu vực ghế theo mẫu, sau đó organizer gán từng khu vực vào một hạng vé của concert.
3. [Organizer] **Build Seating Chart** — khai báo các khu vực:
   - khu vực **ghế ngồi**: số hàng, số ghế mỗi hàng, tiền tố nhãn hàng;
   - khu vực **đứng**: sức chứa tối đa.
4. [System] Sinh danh sách ghế cho từng khu vực ghế ngồi, tính sức chứa từng khu vực và tổng sức chứa concert.
5. [Organizer] **Edit Seating Zone** — thêm khu vực mới, đổi tên, đổi hình dạng, đổi sức chứa, gán lại hạng vé, hoặc xoá khu vực.
6. [System] Kiểm tra tính hợp lệ: không trùng tên khu vực, tổng sức chứa không vượt sức chứa địa điểm, khu vực đã bán vé thì không cho xoá.
7. [Organizer] **Preview Seating Chart** — xem trước sơ đồ hoàn chỉnh: vị trí khu vực, màu theo hạng vé, số ghế, sức chứa, để phát hiện lỗi bố trí trước khi công bố.
8. [Organizer] **Save Seating Chart as Template** — lưu sơ đồ hiện tại thành bộ mẫu để tái sử dụng cho concert sau, kèm tên và mô tả.
9. [System] Lưu bộ mẫu độc lập với concert (không gắn hạng vé); đánh dấu mẫu dùng chung nếu người lưu là Admin.
10. [Organizer] Chuyển sang WF-07 để cấu hình hạng vé và chính sách.

**Nhánh phụ**
- 2a. Mẫu được chọn không tương thích sức chứa địa điểm → hệ thống cảnh báo, cho phép sửa sau khi áp.
- 5a. Xoá khu vực đã phát sinh vé → hệ thống chặn và nêu rõ số vé đang ảnh hưởng.
- 8a. Organizer lưu mẫu dùng chung → hệ thống ghi nhận ở phạm vi riêng tư, chuyển Admin xét duyệt công khai.

**Postcondition:** Concert có sơ đồ ghế hoàn chỉnh, mỗi khu vực gắn được một hạng vé; bộ mẫu (nếu lưu) dùng lại được cho concert khác.
**Chức năng phủ:** Build Seating Chart · Edit Seating Zone · Apply Seating Template · Preview Seating Chart · Save Seating Chart as Template.

---

## WF-07 — Cấu hình bán vé và chính sách vận hành

**Actor:** Organizer
**Trigger:** Sơ đồ ghế đã hoàn tất, organizer chuẩn bị điều kiện bán vé.
**Precondition:** Concert ở trạng thái **Nháp** hoặc **Bị từ chối**.

**Luồng chính**
1. [Organizer] **Configure Ticket Types** — tạo các hạng vé: tên hạng, mô tả, giá niêm yết, giá gốc (nếu có giảm), số lượng phát hành, màu hiển thị trên sơ đồ, thời gian mở bán và đóng bán.
2. [System] Kiểm tra tổng số lượng các hạng vé không vượt sức chứa sơ đồ; gắn hạng vé vào khu vực ghế tương ứng.
3. [Organizer] **Configure Purchase Limit per Event/Ticket Tier** — đặt giới hạn số vé khách được mua, theo hai tầng:
   - theo từng hạng vé: số vé tối thiểu và tối đa trong một đơn;
   - theo toàn concert: số vé tối thiểu và tối đa trên một tài khoản.
4. [System] Áp giới hạn khi khách chọn vé và khi tạo đơn; đơn vượt giới hạn bị từ chối kèm thông báo mức tối đa.
5. [Organizer] **Configure Pricing Rule** — thiết lập quy tắc giá cho từng hạng vé: giảm theo khung thời gian (bán sớm / bán muộn), giảm theo số lượng mua, đổi giá cố định, thứ tự ưu tiên khi nhiều quy tắc cùng áp dụng, trạng thái bật/tắt.
6. [Organizer] **Configure Refund Policy** — thiết lập chính sách hoàn tiền cho concert: tên chính sách, mô tả, mốc thời gian chốt trả vé trước giờ diễn, tỷ lệ hoàn tiền, có bắt buộc Organizer duyệt hay không.
7. [Organizer] **Configure Seat Hold Timeout Policy** — đặt thời hạn giữ ghế (mặc định 10 phút, cho phép 1–30 phút). Đây là khoảng thời gian khách được giữ ghế trước khi hệ thống tự nhả.
8. [Organizer] **Configure Re-entry Policy per Event** — chọn chính sách quét ra/vào cổng:
   - **Không cho vào lại** — quét lần hai bị chặn;
   - **Cho vào lại không giới hạn** — quét lại bất kỳ lúc nào trong thời gian diễn ra;
   - **Cho vào lại có giới hạn thời gian** — phải nhập thời lượng cho phép (5–120 phút).
9. [System] Lưu toàn bộ cấu hình vào concert; dùng thời hạn giữ ghế khi khách chọn ghế và dùng chính sách ra/vào khi soát vé tại cổng.
10. [Organizer] Chuyển sang WF-08 để nộp hồ sơ và xin duyệt.

**Nhánh phụ**
- 1a. Tổng số lượng các hạng vé vượt sức chứa → hệ thống chặn lưu, yêu cầu giảm số lượng hoặc mở thêm khu vực.
- 3a. Giới hạn tối thiểu lớn hơn tối đa → hệ thống từ chối và nêu rõ trường sai.
- 7a. Thời hạn giữ ghế ngoài khoảng 1–30 phút → hệ thống từ chối, không tự ý đổi giá trị.
- 8a. Chọn "có giới hạn thời gian" mà bỏ trống thời lượng → hệ thống từ chối và yêu cầu nhập.

**Postcondition:** Concert có đủ hạng vé, quy tắc giá, chính sách hoàn tiền, thời hạn giữ ghế và chính sách ra/vào cổng — đủ điều kiện nộp duyệt.
**Chức năng phủ:** Configure Ticket Types · Configure Purchase Limit per Event/Ticket Tier · Configure Pricing Rule · Configure Refund Policy · Configure Seat Hold Timeout Policy · Configure Re-entry Policy per Event.

---

## WF-08 — Nộp hồ sơ và duyệt công bố Concert

**Actor:** Organizer, Admin
**Trigger:** Concert đã cấu hình xong, organizer muốn đưa ra công chúng.
**Precondition:** Concert ở trạng thái **Nháp** hoặc **Bị từ chối**; đã có sơ đồ ghế và ít nhất một hạng vé.

**Luồng chính**
1. [Organizer] **Submit Event Compliance Documents** — nộp bộ hồ sơ tuân thủ theo yêu cầu của hệ thống: giấy phép tổ chức, xác nhận phòng cháy chữa cháy, bảo hiểm sự kiện, và các giấy tờ bổ sung theo loại hình sự kiện. Hồ sơ chỉ nhận tệp PDF, PNG, JPEG đúng định dạng, có giới hạn dung lượng mỗi tệp.
2. [System] Tạo một phiên bản hồ sơ mới, đối chiếu với danh sách giấy tờ bắt buộc và chỉ ra loại còn thiếu.
3. [Organizer] **Submit Concert for Approval** — gửi concert đi duyệt.
4. [System] Kiểm tra toàn diện trước khi nhận: thời gian diễn ra hợp lệ, sơ đồ ghế tồn tại, có hạng vé, giới hạn mua hợp lý, thời hạn giữ ghế và chính sách ra/vào hợp lệ, chính sách hoàn tiền đã khai báo, hồ sơ tuân thủ đầy đủ.
5. [System] Đạt toàn bộ điều kiện → chuyển concert sang **Chờ duyệt** và thông báo cho Admin.
6. [Admin] **View Pending Concert List** — mở danh sách concert đang chờ duyệt, ưu tiên theo thời điểm nộp, xem nhanh tên, organizer, lịch diễn, số vé dự kiến và tình trạng hồ sơ.
7. [Admin] **Review Event Compliance Record** — xem chi tiết bộ hồ sơ tuân thủ của concert: từng loại giấy tờ, phiên bản, tệp đính kèm, lịch sử các lần duyệt trước, ghi chú của Admin.
8. [Admin] Ra quyết định cho hồ sơ: **Đạt** · **Không đạt** · **Yêu cầu bổ sung**.
   - Đạt → hồ sơ được đánh dấu đã duyệt đúng phiên bản hiện tại.
   - Không đạt hoặc Yêu cầu bổ sung → concert quay về **Bị từ chối** kèm lý do, organizer phải nộp hồ sơ mới.
9. [Admin] **Approve/Reject Concert** — quyết định cuối cùng:
   - **Duyệt** → concert chuyển **Đã công bố**, hiển thị công khai, mở bán vé theo lịch đã đặt, thông báo cho organizer.
   - **Từ chối** → concert chuyển **Bị từ chối** kèm lý do bắt buộc; organizer sửa và nộp lại từ bước 1.
10. [System] Ghi nhận người duyệt, thời điểm duyệt và toàn bộ lịch sử quyết định để tra cứu về sau.

**Nhánh phụ**
- 4a. Kiểm tra không đạt → concert giữ nguyên **Nháp**, hệ thống trả về danh sách lỗi theo từng hạng mục để organizer sửa.
- 8a. Hồ sơ chưa đạt mà Admin vẫn bấm duyệt concert → hệ thống chặn, yêu cầu duyệt hồ sơ trước.
- 9a. Concert đã bị từ chối ba lần trở lên → hệ thống gắn cờ cảnh báo để Admin xem xét kỹ hơn.

**Postcondition:** Concert **Đã công bố** với hồ sơ tuân thủ đã được duyệt đúng phiên bản; hoặc **Bị từ chối** kèm lý do rõ ràng để organizer khắc phục.
**Chức năng phủ:** Submit Event Compliance Documents · Submit Concert for Approval · View Pending Concert List · Review Event Compliance Record · Approve/Reject Concert.

---

## WF-09 — Huỷ Concert và Thay đổi lịch diễn

**Actor:** Organizer, Admin, Staff, Cổng thanh toán/Ngân hàng
**Trigger:** Concert đã công bố nhưng buộc phải huỷ hoặc đổi lịch.
**Precondition:** Concert **Chờ duyệt** hoặc **Đã công bố** (với nhánh huỷ); **Đã công bố** hoặc **Tạm hoãn** (với nhánh đổi lịch); concert chưa bắt đầu.

**Nhánh A — Huỷ concert**

A1. [Organizer] **Cancel Event** — huỷ concert và xác nhận lại thao tác.
A2. [System] Chuyển concert sang **Đã huỷ**, khoá toàn bộ việc bán vé, ẩn khỏi danh sách công khai, gửi thông báo cho tất cả khách đã mua vé.
A3. [System] Xác định toàn bộ đơn hàng và vé bị ảnh hưởng, chuyển sang bước xử lý ở nhánh B bước B5.

**Nhánh B — Dời lịch / đổi lịch**

B1. [Organizer] **Postpone/Reschedule Event** — chọn hình thức:
   - **Tạm hoãn** — chưa có lịch mới;
   - **Đổi lịch** — nhập thời gian bắt đầu và kết thúc mới.
B2. [Organizer] **Submit Event Change Request** — gửi yêu cầu thay đổi kèm lý do (tối thiểu 10 ký tự), hệ thống tạo yêu cầu ở trạng thái **Chờ duyệt**.
B3. [System] Chặn gửi trùng: mỗi concert chỉ được có một yêu cầu thay đổi đang chờ xử lý tại một thời điểm.
B4. [Admin] **Approve Event Change Request** — xem chi tiết yêu cầu (lịch cũ, lịch mới, lý do, thời điểm gửi) và ra quyết định:
   - **Duyệt** → concert áp lịch mới; hình thức *Tạm hoãn* đưa concert sang **Tạm hoãn**, hình thức *Đổi lịch* đưa concert về **Đã công bố** với lịch mới.
   - **Từ chối** → bắt buộc nhập ghi chú từ 10 ký tự; concert giữ nguyên lịch cũ.
B5. [Admin] **View Affected Orders/Tickets by Change** — xem danh sách đơn hàng và vé bị ảnh hưởng bởi thay đổi hoặc bởi việc huỷ concert, lọc theo trạng thái đơn, mã đơn, khách hàng; tách riêng nhóm vé đã soát và chưa soát.
B6. [Admin] Quyết định phạm vi thu hồi: toàn bộ vé của concert, hoặc chỉ nhóm vé bị ảnh hưởng; ghi lý do thu hồi.
B7. [System] Cập nhật toàn bộ vé trong phạm vi sang trạng thái **Đã thu hồi**, vô hiệu hoá mã QR tương ứng, ghi lịch sử thu hồi kèm người thực hiện và thời điểm.
B8. [Staff/Admin] Chuyển danh sách vé bị thu hồi sang WF-12 để hoàn tiền theo chính sách của concert.
B9. [Cổng thanh toán/Ngân hàng] Thực thi hoàn tiền về phương thức thanh toán gốc của khách.
B10. [System] Ghi nhận kết quả hoàn tiền cho từng vé, cập nhật báo cáo đối soát, thông báo cho khách hàng.

**Nhánh phụ**
- B1a. Concert đã bắt đầu → hệ thống chặn cả huỷ lẫn đổi lịch.
- B1b. Concert đang **Tạm hoãn** mà organizer chọn *Tạm hoãn* lần nữa → hệ thống buộc chọn *Đổi lịch* kèm thời gian mới.
- B4a. Lịch mới không nằm trong tương lai hoặc giờ kết thúc trước giờ bắt đầu → hệ thống từ chối.
- B6a. Vé đã được soát tại cổng → vẫn thu hồi nhưng đánh dấu "đã sử dụng", không hoàn tiền.

**Postcondition:** Không còn vé còn hiệu lực cho lịch cũ; mọi khoản hoàn tiền phát sinh đều được ghi nhận và đối soát.
**Chức năng phủ:** Cancel Event · Postpone/Reschedule Event · Submit Event Change Request · Approve Event Change Request · View Affected Orders/Tickets by Change.

---

## WF-10 — Khám phá Concert công khai

**Actor:** Khách hàng (không cần đăng nhập)
**Trigger:** Người dùng truy cập trang chủ hoặc tìm concert.
**Precondition:** Concert ở trạng thái **Đã công bố** hoặc **Tạm hoãn**.

**Luồng chính**
1. [Khách] **View Event List** — xem danh sách concert đang mở, có phân trang, hiển thị poster, tên, địa điểm, thời gian, giá vé thấp nhất, trạng thái còn vé.
2. [Khách] **View Featured Events** — xem khu vực concert nổi bật do hệ thống chọn lọc, hiển thị dạng banner lớn hơn ở đầu trang chủ.
3. [Khách] **Search Event** — tìm kiếm theo từ khoá tên concert hoặc địa điểm, kết hợp bộ lọc: thành phố, khoảng thời gian, khoảng giá, thể loại nhạc.
4. [System] Chỉ trả về concert công khai; áp bộ lọc và sắp xếp theo mức độ liên quan hoặc thời gian diễn ra.
5. [Khách] **View Event Details** — mở chi tiết một concert: mô tả, poster/banner, địa điểm, lịch diễn, sơ đồ ghế, danh sách hạng vé kèm giá sau khi áp quy tắc giá, chính sách hoàn tiền, chính sách quét ra/vào cổng.
6. [System] Hiển thị tình trạng bán theo thời gian thực: **Sắp mở bán** · **Đang bán** · **Sắp hết vé** · **Hết vé**.
7. [Khách] Bấm "Mua vé" → chuyển sang W1 (bước 12 nếu concert có hàng chờ ảo, bước 13 nếu không).

**Nhánh phụ**
- 5a. Concert ở trạng thái **Nháp**, **Chờ duyệt** hoặc **Đã huỷ** → hệ thống trả về không tìm thấy, kể cả khi khách có đường dẫn trực tiếp.
- 5b. Concert **Tạm hoãn** → vẫn xem được chi tiết, hiển thị thông báo lịch mới và vô hiệu hoá nút mua vé.
- 6a. Concert đang trong giai đoạn hàng chờ ảo → hiển thị đồng hồ đếm ngược mở bán thay vì nút mua vé.

**Postcondition:** Khách có đủ thông tin để quyết định mua vé và đi tiếp vào luồng mua.
**Chức năng phủ:** View Event List · View Featured Events · Search Event · View Event Details.

---

## WF-11 — Trả vé (phía khách hàng)

**Actor:** Khách hàng, Staff/Admin
**Trigger:** Khách không thể tham dự và muốn trả lại vé.
**Precondition:** Khách đã đăng nhập, sở hữu vé ở trạng thái **Hợp lệ**, vé chưa được soát tại cổng, còn trong hạn trả vé theo chính sách hoàn tiền của concert.

**Luồng chính**
1. [Khách] Mở "Vé của tôi"; hệ thống hiển thị mỗi vé kèm: khả năng trả vé, mức hoàn tiền dự kiến, tỷ lệ hoàn, và thông điệp giải thích nếu không trả được.
2. [System] Kiểm tra điều kiện trả vé: vé đã thanh toán, chưa soát, còn trong hạn chốt trả vé, không có yêu cầu trả vé nào khác đang chờ xử lý.
3. [Khách] **Submit Ticket Return Request** — gửi yêu cầu trả vé kèm lý do.
4. [System] Tạo yêu cầu ở trạng thái **Chờ duyệt**, chuyển vé sang **Đang chờ trả**; mã QR của vé mất hiệu lực ngay lập tức để tránh dùng vé trong lúc chờ.
5. [Khách] **Track Ticket Return Status** — theo dõi trạng thái yêu cầu: Chờ duyệt → Đã duyệt → Đã hoàn tiền, hoặc Bị từ chối / Hoàn tiền thất bại; xem số tiền hoàn, ghi chú của người duyệt và lỗi hoàn tiền nếu có.
6. [System] Gửi thông báo cho khách mỗi khi trạng thái yêu cầu thay đổi.
7. [Staff/Admin] Xem hàng đợi yêu cầu trả vé và duyệt hoặc từ chối kèm ghi chú.
8. [System] Khi được duyệt: hoàn vé về kho của concert trước, sau đó thực hiện hoàn tiền qua WF-12.
9. [Khách] **Cancel Ticket Return Request** — rút lại yêu cầu khi còn **Chờ duyệt**; vé quay về **Hợp lệ**, mã QR dùng lại được, khách có thể tiếp tục tham dự.

**Nhánh phụ**
- 2a. Vé đã soát cổng hoặc đã quá hạn chốt trả vé → hệ thống ẩn nút trả vé và nêu lý do cụ thể.
- 8a. Hoàn tiền thất bại → yêu cầu chuyển **Hoàn tiền thất bại**, khách thấy nút theo dõi và Staff xử lý lại theo WF-12.
- 8b. Không hoàn được vé về kho → yêu cầu giữ nguyên **Chờ duyệt**, chưa hoàn tiền, hệ thống ghi nhận để xử lý thủ công.
- 9a. Yêu cầu đã được duyệt hoặc đã hoàn tiền → không cho rút lại.

**Postcondition:** Vé hoặc được trả về kho kèm hoàn tiền, hoặc quay lại trạng thái dùng được; khách luôn biết trạng thái hiện tại của yêu cầu.
**Chức năng phủ:** Submit Ticket Return Request · Track Ticket Return Status · Cancel Ticket Return Request.

---

## WF-12 — Xử lý hoàn tiền và đối soát

**Actor:** Admin, Staff, Cổng thanh toán/Ngân hàng
**Trigger:** Phát sinh nghĩa vụ hoàn tiền từ W2, WF-09 (huỷ/đổi lịch) hoặc WF-13 (khiếu nại).
**Precondition:** Đơn hàng đã thanh toán thành công; số tiền hoàn không vượt quá số tiền còn hoàn được của đơn.

**Luồng chính**
1. [Admin] Mở tổng quan hoàn tiền của đơn hàng: tổng đã thanh toán, tổng đã hoàn, số còn hoàn được, danh sách các lần hoàn trước đó.
2. [Admin] **Process Full/Partial Refund** — chọn hình thức:
   - **Hoàn toàn phần** — trả lại toàn bộ số tiền còn hoàn được;
   - **Hoàn một phần** — nhập số tiền cụ thể.
   Kèm lý do và ghi chú nội bộ.
3. [System] Kiểm tra số tiền hoàn lớn hơn 0 và không vượt số còn hoàn được; kiểm tra không trùng với một yêu cầu hoàn đang xử lý; tạo bản ghi hoàn tiền ở trạng thái **Đang xử lý**.
4. [Cổng thanh toán/Ngân hàng] Thực thi hoàn tiền về phương thức thanh toán gốc.
5. [System] Ghi nhận kết quả:
   - **Thành công** → trạng thái **Đã hoàn tiền**, lưu mã tham chiếu giao dịch, thông báo khách.
   - **Thất bại** → trạng thái **Hoàn tiền thất bại**, lưu mã lỗi và mô tả lỗi, tăng số lần thử.
6. [Admin] **Retry Failed Refund** — thử lại khoản hoàn tiền thất bại, chọn cách thử **ngay lập tức** hoặc **theo lịch hẹn**; xem lại chi tiết từng lần thử.
7. [System] Đối soát tự động: so số tiền đã hoàn với số tiền cổng thanh toán ghi nhận, gắn nhãn **Khớp** hoặc **Lệch** (các khoản thất bại cũng xếp vào nhóm lệch).
8. [Admin] **View Refund Reconciliation Report** — xem báo cáo đối soát: tổng số khoản hoàn, số khớp, số lệch, số thất bại, lọc theo khoảng thời gian, concert, trạng thái, người xử lý.
9. [Admin] Xử lý ngoại lệ: mở từng khoản lệch, xem chênh lệch, ghi chú cách xử lý và đánh dấu đã giải quyết.
10. [System] Kết xuất báo cáo hoàn tiền phục vụ đối soát tài chính cuối kỳ.

**Nhánh phụ**
- 3a. Số tiền vượt mức còn hoàn được → hệ thống từ chối và hiển thị mức tối đa cho phép.
- 5a. Cổng thanh toán phản hồi chậm hoặc timeout → khoản hoàn giữ **Đang xử lý**, hệ thống đối soát lại sau thay vì kết luận thất bại ngay.
- 7a. Khoản lệch không tự khớp được → bắt buộc Admin ghi chú xử lý trước khi đóng ngoại lệ.

**Postcondition:** Mọi khoản hoàn tiền đều có trạng thái cuối cùng và khớp với sao kê cổng thanh toán; báo cáo đối soát phản ánh đúng thực tế.
**Chức năng phủ:** Process Full/Partial Refund · Retry Failed Refund · View Refund Reconciliation Report.

---

## WF-13 — Giải quyết tranh chấp vé

**Actor:** Admin, Staff, Khách hàng
**Trigger:** Khách phản ánh vé không hợp lệ, không nhận được vé, vé bị trùng, hoặc bị từ chối tại cổng dù đã mua.
**Precondition:** Đã có khiếu nại liên quan đến một vé hoặc một đơn hàng cụ thể.

**Luồng chính**
1. [Admin/Staff] **View Ticket Dispute List** — mở danh sách tranh chấp vé, hiển thị mã vé, mã đơn, khách hàng, concert, loại tranh chấp, mức ưu tiên, trạng thái và người đang phụ trách; lọc theo trạng thái, loại tranh chấp, khoảng thời gian.
2. [Admin/Staff] **View Dispute Details** — xem chi tiết một tranh chấp:
   - thông tin vé: hạng vé, vị trí ghế, trạng thái hiện tại;
   - lịch sử vé: thời điểm phát hành, các lần đổi mã QR, lịch sử soát vé tại cổng;
   - thông tin đơn hàng và thanh toán liên quan;
   - nội dung trao đổi với khách và bằng chứng đính kèm.
3. [System] Đối chiếu tự động để phát hiện bất thường: vé đã soát nhiều lần, vé bị thu hồi nhưng khách vẫn giữ, mã QR đã bị đổi, vé thuộc đơn đã hoàn tiền.
4. [Admin/Staff] **Resolve Ticket Dispute** — chọn hướng xử lý:
   - **Xác nhận vé hợp lệ** — kết luận không có sai sót, thông báo lại cho khách cách sử dụng vé;
   - **Phát hành lại vé** — cấp vé mới và mã QR mới, vô hiệu hoá vé cũ;
   - **Thu hồi vé** — thu hồi vé bị lỗi hoặc bị lạm dụng, ghi rõ lý do;
   - **Hoàn tiền** — chuyển sang WF-12 nếu khách không thể sử dụng vé.
5. [Admin/Staff] Nhập kết luận và ghi chú xử lý; hệ thống lưu lại toàn bộ lịch sử quyết định.
6. [System] Thực thi kết quả: cập nhật trạng thái vé, sinh mã QR mới nếu phát hành lại, ghi lịch sử thay đổi, gửi thông báo cho khách.
7. [System] Đánh dấu tranh chấp **Đã giải quyết**; nếu phát hiện dấu hiệu lạm dụng, tự động chuyển sang W3 (Fraud Prevention Flow).

**Nhánh phụ**
- 3a. Phát hiện cùng một vé được dùng bởi nhiều tài khoản → hệ thống gắn cờ ưu tiên cao và đề xuất thu hồi.
- 4a. Tranh chấp đã quá thời hạn xử lý → hệ thống cảnh báo cho Admin cấp trên.
- 4b. Tranh chấp liên quan đến khoản hoàn tiền đã thực hiện → không cho phát hành lại vé, chỉ được thu hồi hoặc hoàn thêm.

**Postcondition:** Mỗi tranh chấp có kết luận rõ ràng, vé liên quan ở trạng thái đúng, khách được thông báo kết quả.
**Chức năng phủ:** View Ticket Dispute List · View Dispute Details · Resolve Ticket Dispute.

---

## WF-14 — Quản lý yêu cầu hỗ trợ

**Actor:** Khách hàng, Staff hỗ trợ, Admin
**Trigger:** Khách cần hỗ trợ về đơn hàng, vé, thanh toán hoặc trải nghiệm sự kiện.
**Precondition:** Khách đã đăng nhập; yêu cầu có thể gắn với một đơn hàng hoặc vé cụ thể nếu có.

**Luồng chính**
1. [Khách] **Create Support Case** — gửi yêu cầu hỗ trợ: tiêu đề, mô tả chi tiết, loại vấn đề, mức ưu tiên, đính kèm mã đơn hàng / mã vé / mã giao dịch nếu có.
2. [System] Sinh mã yêu cầu duy nhất, đặt trạng thái **Mới mở**, ghi nhận người gửi và thời điểm, gửi thông báo xác nhận cho khách.
3. [Staff/Admin] **View Support Case List** — xem danh sách yêu cầu: mã, tiêu đề, khách hàng, loại vấn đề, mức ưu tiên, trạng thái, người phụ trách; lọc theo trạng thái, mức ưu tiên, người phụ trách và tìm kiếm tự do.
4. [System] Phân quyền hiển thị: Staff chỉ thấy yêu cầu được gán cho mình hoặc chưa có người phụ trách; Admin và quản lý thấy toàn bộ.
5. [Staff/Admin] **View Support Case Details** — xem chi tiết: nội dung khách gửi, thông tin đơn hàng/vé liên quan, dòng thời gian trao đổi, ghi chú nội bộ, lịch sử thay đổi trạng thái.
6. [Admin] **Assign Support Case to Staff** — gán yêu cầu cho một nhân viên phù hợp; hệ thống chuyển trạng thái sang **Đã phân công**, gửi thông báo và email cho nhân viên được gán.
7. [Staff] Phân loại hướng xử lý:
   - **Khiếu nại vé/đơn** → chuyển sang WF-13 để xác minh và giải quyết tranh chấp;
   - **Cần hoàn tiền** → chuyển sang WF-12;
   - **Nghi ngờ gian lận** → chuyển sang W3;
   - **Chỉ cần giải đáp** → trả lời trực tiếp và bổ sung ghi chú nội bộ.
8. [Staff] Cập nhật tiến độ qua ghi chú nội bộ và phản hồi cho khách; hệ thống gửi thông báo mỗi lần có cập nhật.
9. [Staff/Admin] **Resolve/Close Support Case** — nhập kết quả xử lý và chuyển trạng thái:
   - **Đã giải quyết** — đã xử lý xong, chờ khách xác nhận;
   - **Đã đóng** — kết thúc vĩnh viễn, không tiếp nhận trao đổi thêm.
10. [System] Ghi nhận người xử lý, thời điểm giải quyết, gửi thông báo kết quả cho khách và lưu vào lịch sử phục vụ báo cáo chất lượng hỗ trợ.

**Nhánh phụ**
- 1a. Khách tham chiếu đơn hàng/vé không thuộc quyền sở hữu → hệ thống từ chối tạo yêu cầu.
- 6a. Admin chọn nhân viên không thuộc nhóm xử lý phù hợp → hệ thống cảnh báo, yêu cầu chọn lại.
- 9a. Khách không phản hồi sau thời hạn nhắc nhở → hệ thống tự động nhắc lần cuối, sau đó Admin đóng yêu cầu.
- 9b. Khách mở lại yêu cầu đã đóng → hệ thống tạo yêu cầu mới liên kết với yêu cầu cũ.

**Postcondition:** Mọi yêu cầu hỗ trợ đều có người phụ trách, có kết luận và khách nhận được thông báo kết quả.
**Chức năng phủ:** Create Support Case · View Support Case List · View Support Case Details · Assign Support Case to Staff · Resolve/Close Support Case.

---

## WF-15 — Xử lý sự cố tại chỗ

**Actor:** Staff, Nhân viên an ninh, Admin
**Trigger:** Xảy ra sự cố tại cổng soát vé hoặc trong khu vực tổ chức: vé giả, chen lấn, xô xát, thiết bị soát vé lỗi, sự cố y tế, mất trật tự.
**Precondition:** Staff đang trong ca trực của concert; xác định được concert và vị trí xảy ra sự cố.

**Luồng chính**
1. [Staff] **Report On-site Incident** — tạo báo cáo sự cố: loại sự cố, mức độ nghiêm trọng, mô tả diễn biến, vị trí/cổng xảy ra, thời điểm, thông tin vé hoặc đơn hàng liên quan nếu có.
2. [System] Tạo hồ sơ sự cố ở trạng thái **Đã báo cáo**, sinh mã sự cố, ghi nhận người báo cáo.
3. [Staff] **Attach Evidence** — đính kèm bằng chứng: ảnh, video, tài liệu, biên bản; hệ thống kiểm tra định dạng và dung lượng, cho phép gỡ bỏ bằng chứng đính kèm nhầm.
4. [Staff] **Update Incident Report** — bổ sung thông tin khi có tình tiết mới: cập nhật mô tả, thêm người liên quan, cập nhật mức độ nghiêm trọng, chuyển trạng thái xử lý (Đang xử lý / Đã tạm ổn).
5. [Staff] **Escalate Incident to Admin** — chuyển sự cố lên Admin khi vượt thẩm quyền, kèm lý do và mức ưu tiên; trạng thái chuyển **Đã chuyển cấp** và Admin trực ca nhận thông báo ngay.
6. [Admin] Tiếp nhận, điều phối nguồn lực xử lý, có thể đổi mức ưu tiên, gán thêm người phụ trách hoặc yêu cầu bổ sung bằng chứng.
7. [Staff/Admin] Ghi nhận kết quả xử lý: biện pháp đã áp dụng, hậu quả, thiệt hại, có thu hồi vé hay không, có cần chuyển cơ quan chức năng hay không; trạng thái chuyển **Đã giải quyết**.
8. [Staff/Admin] **View Resolution** — xem kết luận xử lý và toàn bộ lịch sử sự cố: ai báo cáo, ai xử lý, bằng chứng, các lần chuyển cấp, thời gian từng bước.
9. [System] Sau thời hạn khiếu nại, tự động đóng sự cố (**Đã đóng**); dữ liệu được tổng hợp vào báo cáo vận hành của concert.

**Nhánh phụ**
- 1a. Sự cố mức nghiêm trọng (y tế, an ninh, cháy nổ) → hệ thống tự động chuyển cấp ngay khi tạo, không chờ Staff thao tác.
- 3a. Tệp vượt dung lượng hoặc sai định dạng → hệ thống từ chối và yêu cầu nộp lại.
- 4a. Sự cố đã chuyển cấp → Staff chỉ được bổ sung thông tin, không được tự đổi trạng thái.
- 7a. Sự cố liên quan vé giả hoặc lạm dụng → kết thúc bằng thu hồi vé hàng loạt hoặc chuyển W3 để khoá tài khoản.

**Postcondition:** Mỗi sự cố có hồ sơ đầy đủ gồm bằng chứng, kết luận, lịch sử xử lý và phục vụ báo cáo sau sự kiện.
**Chức năng phủ:** Report On-site Incident · Attach Evidence · Update Incident Report · Escalate Incident to Admin · View Resolution.

---

## WF-16 — Báo cáo doanh thu và phân công nhân sự

**Actor:** Organizer
**Trigger:** Concert đang hoặc đã bán vé; organizer cần theo dõi hiệu quả và chuẩn bị đội ngũ vận hành.
**Precondition:** Concert thuộc quyền quản lý của organizer; với báo cáo doanh thu, đã có đơn hàng phát sinh.

**Luồng chính**
1. [Organizer] **View Revenue Dashboard** — mở bảng điều khiển doanh thu của concert: tổng doanh thu, số vé đã bán trên tổng số vé, tỷ lệ lấp chỗ, doanh thu theo từng hạng vé, số đơn đã huỷ, số tiền đã hoàn.
2. [Organizer] **Filter Revenue Dashboard** — lọc số liệu theo: khoảng thời gian, hạng vé, kênh bán; bảng điều khiển tính lại toàn bộ chỉ số theo bộ lọc.
3. [System] Khi tính doanh thu, tự động trừ các đơn đã huỷ và các khoản đã hoàn tiền; chỉ tính đơn đã thanh toán thành công.
4. [Organizer] **Export Revenue Report** — kết xuất báo cáo doanh thu ra tệp theo đúng bộ lọc đang xem, phục vụ đối soát nội bộ.
5. [Organizer] **View Staff List** — xem danh sách nhân sự đã được phân công cho concert: họ tên, vai trò, cổng/khu vực phụ trách, ca trực, quyền hạn (bao gồm quyền duyệt yêu cầu trả vé).
6. [Organizer] Tìm nhân sự khả dụng theo tên hoặc mã nhân viên để bổ sung vào concert.
7. [Organizer] **Assign/Unassigned Staff** — phân công nhân sự vào concert kèm cổng phụ trách và quyền hạn; hoặc bỏ phân công khi không còn cần.
8. [System] Gửi thông báo phân công cho nhân sự; concert xuất hiện trong danh sách ca trực của họ và mở quyền soát vé theo WF mua vé bước 18–19.
9. [Organizer] **Export Check-in Report** — kết xuất báo cáo soát vé của concert: số vé đã soát, số chưa soát, tỷ lệ tham dự, phân bổ theo cổng và theo khung giờ.
10. [System] Dữ liệu báo cáo được lưu lại phục vụ đối chiếu sau sự kiện.

**Nhánh phụ**
- 1a. Concert chưa phát sinh đơn hàng → bảng điều khiển hiển thị chỉ số bằng 0 kèm thông báo chưa có giao dịch.
- 7a. Nhân sự đã được phân công cho concert khác trong cùng khung giờ → hệ thống cảnh báo trùng ca, yêu cầu xác nhận lại.
- 7b. Bỏ phân công nhân sự đang có sự cố chưa đóng → hệ thống cảnh báo và yêu cầu bàn giao trước.
- 9a. Concert chưa diễn ra → báo cáo soát vé trả về rỗng kèm thông báo chưa có dữ liệu.

**Postcondition:** Organizer nắm được doanh thu theo bộ lọc tùy chọn, có báo cáo doanh thu và báo cáo soát vé để đối soát, và có đội ngũ vận hành được phân công rõ ràng.
**Chức năng phủ:** View Revenue Dashboard · Filter Revenue Dashboard · Export Revenue Report · Export Check-in Report · View Staff List · Assign/Unassigned Staff.

---

# Luồng nghiệp vụ tổng thể

```text
[Khách] Yêu cầu làm Organizer
   └─► [Admin] Duyệt Organizer
         └─► WF-05 Tạo concert (Nháp)
               └─► WF-06 Dựng sơ đồ ghế
                     └─► WF-07 Cấu hình vé & chính sách
                           └─► WF-08 Nộp hồ sơ → Admin duyệt → Đã công bố
                                 ├─► WF-10 Khách khám phá concert
                                 │     └─► W1 Mua vé → soát vé tại cổng
                                 │           ├─► WF-11 Khách trả vé
                                 │           │     └─► WF-12 Hoàn tiền & đối soát
                                 │           └─► WF-13 Tranh chấp vé
                                 │                 └─► WF-14 Yêu cầu hỗ trợ
                                 ├─► WF-09 Huỷ / đổi lịch → thu hồi vé → WF-12
                                 ├─► WF-15 Sự cố tại chỗ (trong lúc diễn ra)
                                 ├─► WF-16 Doanh thu & phân công nhân sự
                                 └─► W3 Fraud Prevention Flow (song song, bất kỳ lúc nào)
```

---

# Ma trận phủ chức năng

| # | Chức năng | Workflow | Bước |
|---|---|---|---|
| 1 | View Event List | WF-10 | 1 |
| 2 | View Event Details | WF-10 | 5 |
| 3 | Search Event | WF-10 | 3 |
| 4 | View Featured Events | WF-10 | 2 |
| 5 | Configure Seat Hold Timeout Policy | WF-07 | 7 |
| 6 | Configure Re-entry Policy per Event | WF-07 | 8 |
| 7 | Submit Ticket Return Request | WF-11 | 3 |
| 8 | Track Ticket Return Status | WF-11 | 5 |
| 9 | Cancel Ticket Return Request | WF-11 | 9 |
| 10 | View Concert List | WF-05 | 1 |
| 11 | View Concert Details | WF-05 | 4 |
| 12 | Edit Concert Information | WF-05 | 5 |
| 13 | Submit Concert for Approval | WF-08 | 3 |
| 14 | Create Concert Event | WF-05 | 2 |
| 15 | Delete Draft Concert | WF-05 | 7 |
| 16 | Cancel Event | WF-09 | A1 |
| 17 | Postpone/Reschedule Event | WF-09 | B1 |
| 18 | Submit Event Change Request | WF-09 | B2 |
| 19 | Approve Event Change Request | WF-09 | B4 |
| 20 | View Affected Orders/Tickets by Change | WF-09 | B5 |
| 21 | Submit Event Compliance Documents | WF-08 | 1 |
| 22 | Review Event Compliance Record | WF-08 | 7 |
| 23 | Build Seating Chart | WF-06 | 3 |
| 24 | Edit Seating Zone | WF-06 | 5 |
| 25 | Apply Seating Template | WF-06 | 2 |
| 26 | Save Seating Chart as Template | WF-06 | 8 |
| 27 | Preview Seating Chart | WF-06 | 7 |
| 28 | Configure Ticket Types | WF-07 | 1 |
| 29 | Configure Pricing Rule | WF-07 | 5 |
| 30 | Configure Refund Policy | WF-07 | 6 |
| 31 | Configure Purchase Limit per Event/Ticket Tier | WF-07 | 3 |
| 32 | View Revenue Dashboard | WF-16 | 1 |
| 33 | Filter Revenue Dashboard | WF-16 | 2 |
| 34 | Export Revenue Report | WF-16 | 4 |
| 35 | Export Check-in Report | WF-16 | 9 |
| 36 | View Staff List | WF-16 | 5 |
| 37 | Assign/Unassigned Staff | WF-16 | 7 |
| 38 | Report On-site Incident | WF-15 | 1 |
| 39 | Attach Evidence | WF-15 | 3 |
| 40 | Update Incident Report | WF-15 | 4 |
| 41 | Escalate Incident to Admin | WF-15 | 5 |
| 42 | View Resolution | WF-15 | 8 |
| 43 | View Pending Concert List | WF-08 | 6 |
| 44 | Approve/Reject Concert | WF-08 | 9 |
| 45 | View Ticket Dispute List | WF-13 | 1 |
| 46 | View Dispute Details | WF-13 | 2 |
| 47 | Resolve Ticket Dispute | WF-13 | 4 |
| 48 | Create Support Case | WF-14 | 1 |
| 49 | View Support Case List | WF-14 | 3 |
| 50 | View Support Case Details | WF-14 | 5 |
| 51 | Assign Support Case to Staff | WF-14 | 6 |
| 52 | Resolve/Close Support Case | WF-14 | 9 |
| 53 | Process Full/Partial Refund | WF-12 | 2 |
| 54 | Retry Failed Refund | WF-12 | 6 |
| 55 | View Refund Reconciliation Report | WF-12 | 8 |

**Kết quả: 55/55 chức năng đã có workflow và vị trí bước cụ thể.**

---

# Quan hệ giữa các workflow mới và 4 workflow đã có

| Workflow đã có | Được bổ sung bởi |
|---|---|
| W1 Luồng mua vé | WF-10 (đầu vào), WF-15 (soát vé tại cổng) |
| W2 Ticket Refund Flow | WF-11 (phía khách), WF-12 (thực thi hoàn tiền) |
| W3 Fraud Prevention Flow | WF-13 (chuyển sang khi phát hiện lạm dụng), WF-15 (sự cố vé giả) |
| W4 Event Change Flow | WF-09 (mở rộng đầy đủ cả huỷ và đổi lịch) |

Ba điểm cần thống nhất khi trình bày:
1. **Ticket Dispute** và **Support Case** là hai màn hình khác nhau nhưng cùng một nghiệp vụ gốc: tranh chấp vé là một loại yêu cầu hỗ trợ đã được phân loại. WF-13 xử lý phần chuyên môn về vé, WF-14 xử lý phần tiếp nhận và theo dõi chung.
2. **Huỷ concert** và **Đổi lịch** là hai đường khác nhau: huỷ là thao tác trực tiếp của organizer; đổi lịch bắt buộc đi qua yêu cầu thay đổi có Admin duyệt (WF-09 nhánh B).
3. **Approval** xuất hiện ở ba nơi: duyệt hồ sơ tuân thủ, duyệt concert công bố (WF-08), và duyệt yêu cầu thay đổi lịch (WF-09). Cần phân biệt rõ ba quyết định này khi vẽ activity diagram.
