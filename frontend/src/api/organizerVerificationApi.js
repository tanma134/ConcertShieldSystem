import axiosClient from "./axiosClient";

const organizerVerificationApi = {
  // UC_11.3: Submit Verification Request & Cloudinary Documents
  submitVerification: (formData) =>
    axiosClient.post("/organizer/verification/submit", formData, {
      headers: { "Content-Type": "multipart/form-data" },
    }),

  // Get My Verification Request Status
  getMyStatus: () =>
    axiosClient.get("/organizer/verification/my-status"),

  // UC_11.4: Review Pending Verification Requests List (Admin)
  getPendingRequests: (statusFilter = "ALL") =>
    axiosClient.get(`/admin/verification/requests?statusFilter=${statusFilter}`),

  // UC_11.5: Approve Verification Request (Admin)
  approveRequest: (requestId, data = {}) =>
    axiosClient.post(`/admin/verification/${requestId}/approve`, data),

  // UC_11.5: Reject Verification Request (Admin)
  rejectRequest: (requestId, data) =>
    axiosClient.post(`/admin/verification/${requestId}/reject`, data),
};

export default organizerVerificationApi;
