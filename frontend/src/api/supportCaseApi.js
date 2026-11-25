import axiosClient from "./axiosClient";

const cleanParams = (params = {}) => {
  return Object.fromEntries(
    Object.entries(params).filter(([, v]) => v !== "" && v != null)
  );
};

const supportCaseApi = {
  createCase: (data) => axiosClient.post("/support-cases", data),

  getCases: (params) => axiosClient.get("/support-cases", { params: cleanParams(params) }),

  getCaseDetail: (id) => axiosClient.get(`/support-cases/${id}`),

  getCaseByOrder: (orderId) => axiosClient.get(`/support-cases/by-order/${orderId}`),

  // Get this customer's order IDs that have support cases
  getMyReportedOrders: () => axiosClient.get("/support-cases/my-reported-orders"),

  // Phan cong truong hop ho tro cho nhan vien danh cho Admin
  assignStaff: (id, data) => axiosClient.post(`/support-cases/${id}/assign`, data),

  resolveCase: (id, data) => axiosClient.post(`/support-cases/${id}/resolve`, data),

  dismissCase: (id, data) => axiosClient.post(`/support-cases/${id}/dismiss`, data),

  // Them ghi chu trao doi vao truong hop ho tro danh cho Customer, Staff va Admin
  addNote: (id, data) => axiosClient.post(`/support-cases/${id}/notes`, data),

  getTicketDisputes: (params) =>
    axiosClient.get("/support-cases/ticket-disputes", {
      params: cleanParams(params),
    }),
};

export default supportCaseApi;
