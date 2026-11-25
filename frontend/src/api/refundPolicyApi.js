import axiosClient from "./axiosClient";

// Quan ly chinh sach hoan ve theo moc thoi gian cua concert danh cho Guest va Organizer
const refundPolicyApi = {
  // Lay danh sach cac chinh sach hoan ve cua concert danh cho Guest va Organizer
  getByEvent: (eventId) => axiosClient.get(`/refundpolicies/event/${eventId}`),

  // Tao moi chinh sach hoan ve danh cho Organizer
  create: (eventId, dto) =>
    axiosClient.post(`/refundpolicies/event/${eventId}`, dto),

  // Cap nhat moc thoi gian va ty le hoan ve danh cho Organizer
  update: (refundPolicyId, dto) =>
    axiosClient.put(`/refundpolicies/${refundPolicyId}`, {
      refundPolicyId,
      ...dto,
    }),

  // Xoa chinh sach hoan ve danh cho Organizer
  remove: (refundPolicyId) =>
    axiosClient.delete(`/refundpolicies/${refundPolicyId}`),
};

export default refundPolicyApi;
