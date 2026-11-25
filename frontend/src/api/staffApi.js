import axiosClient from "./axiosClient";

// Quan ly danh sach nhan vien va phan cong soat ve danh cho Organizer va Staff
const staffApi = {
  // Lay danh sach nhan vien duoc phan cong cho su kien danh cho Organizer
  list: (eventId) => axiosClient.get(`/events/${eventId}/staff`),

  // Tim kiem nhan vien ung vien tu danh ba he thong danh cho Organizer
  candidates: (eventId, q) =>
    axiosClient.get(`/events/${eventId}/staff/candidates`, { params: q ? { q } : {} }),

  // Phan cong nhan vien vao lam viec tai su kien kem cong soat ve danh cho Organizer
  assign: (eventId, staffUserId, gateName, canReviewReturns = false) =>
    axiosClient.post(`/events/${eventId}/staff`, { staffUserId, gateName, canReviewReturns }),

  // Cap nhat quyen duyet hoan ve cho nhan vien tai su kien danh cho Organizer
  setReturnReview: (eventId, staffUserId, canReviewReturns) =>
    axiosClient.put(`/events/${eventId}/staff/${staffUserId}/return-review`, { canReviewReturns }),

  // Huy phan cong nhan vien khoi su kien danh cho Organizer
  unassign: (eventId, staffUserId) => axiosClient.delete(`/events/${eventId}/staff/${staffUserId}`),

  // Lay danh sach su kien va cong soat ve duoc phan cong cho nhan vien dang dang nhap danh cho Staff
  getMyAssignments: (staffUserId) => axiosClient.get(`/events/staff/${staffUserId}`),
};

export default staffApi;
