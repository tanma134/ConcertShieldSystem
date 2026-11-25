import axiosClient from "./axiosClient";

const cleanParams = (params = {}) => {
  return Object.fromEntries(
    Object.entries(params).filter(([, v]) => v !== "" && v != null)
  );
};

// Quan ly cac API ho so ho tro khach hang danh cho Customer, Staff va Admin
const supportCaseApi = {
  // Tao ho so yeu cau ho tro moi danh cho Customer va Nguoi dung
  createCase: (data) => axiosClient.post("/support-cases", data),

  // Lay danh sach cac truong hop ho tro co loc va phan trang danh cho Staff va Admin
  getCases: (params) => axiosClient.get("/support-cases", { params: cleanParams(params) }),

  // Xem thong tin chi tiet truong hop ho tro theo id danh cho Customer, Staff va Admin
  getCaseDetail: (id) => axiosClient.get(`/support-cases/${id}`),

  // Lay ho so ho tro lien ket voi don hang cu the danh cho Customer va Staff
  getCaseByOrder: (orderId) => axiosClient.get(`/support-cases/by-order/${orderId}`),

  // Get this customer's order IDs that have support cases
  getMyReportedOrders: () => axiosClient.get("/support-cases/my-reported-orders"),

  // Phan cong truong hop ho tro cho nhan vien danh cho Admin
  assignStaff: (id, data) => axiosClient.post(`/support-cases/${id}/assign`, data),

  // Giai quyet va dong truong hop ho tro danh cho Staff va Admin
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
