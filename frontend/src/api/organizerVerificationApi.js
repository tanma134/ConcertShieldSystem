import axiosClient from "./axiosClient";

const organizerVerificationApi = {
  // Submit Verification Request & Cloudinary Documents
  submitVerification: (formData) =>
    axiosClient.post("/organizer/verification/submit", formData, {
      headers: { "Content-Type": "multipart/form-data" },
    }),

  // Get My Verification Request Status
  getMyStatus: () =>
    axiosClient.get("/organizer/verification/my-status"),

  // Review Pending Verification Requests List (Admin)
  getPendingRequests: (statusFilter = "ALL") =>
    axiosClient.get(`/admin/verification/requests?statusFilter=${statusFilter}`),

  // Approve Verification Request (Admin)
  approveRequest: (requestId, data = {}) =>
    axiosClient.post(`/admin/verification/${requestId}/approve`, data),

  // Reject Verification Request (Admin)
  rejectRequest: (requestId, data) =>
    axiosClient.post(`/admin/verification/${requestId}/reject`, data),
};

export default organizerVerificationApi;
