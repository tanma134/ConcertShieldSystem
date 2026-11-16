import axiosClient from "./axiosClient";

const organizerRequestApi = {
  // User gửi yêu cầu trở thành Organizer
  create: (data) => axiosClient.post("/organizer-requests", data),

  // User xem lịch sử yêu cầu của chính mình
  getMyRequests: () => axiosClient.get("/organizer-requests/me"),

  // Xem chi tiết 1 request theo id
  getById: (requestId) => axiosClient.get(`/organizer-requests/${requestId}`),

  // Admin: lấy danh sách request, status = "Pending" | "Approved" | "Rejected" | undefined (tất cả)
  getAll: (status) =>
    axiosClient.get("/organizer-requests", { params: status ? { status } : {} }),

  // Admin: duyệt / từ chối request
  review: (requestId, data) =>
    axiosClient.put(`/organizer-requests/${requestId}/review`, data),
};

export default organizerRequestApi;