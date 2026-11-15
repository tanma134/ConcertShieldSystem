import axiosClient from "./axiosClient";

const organizerRequestApi = {
  // User gửi yêu cầu trở thành Organizer (kèm thông tin pháp lý và tệp/ảnh đính kèm)
  create: (formData) => axiosClient.post("/organizer-requests", formData),

  // User xem lịch sử yêu cầu của chính mình (kèm danh sách tệp đính kèm)
  getMyRequests: () => axiosClient.get("/organizer-requests/me"),

  // Xem chi tiết 1 request theo id (kèm tài liệu)
  getById: (requestId) => axiosClient.get(`/organizer-requests/${requestId}`),

  // Admin: lấy danh sách request, status = "Pending" | "Approved" | "Rejected" | undefined (tất cả)
  getAll: (status) =>
    axiosClient.get("/organizer-requests", { params: status ? { status } : {} }),

  // Admin: duyệt / từ chối request
  review: (requestId, data) =>
    axiosClient.put(`/organizer-requests/${requestId}/review`, data),
};

export default organizerRequestApi;
