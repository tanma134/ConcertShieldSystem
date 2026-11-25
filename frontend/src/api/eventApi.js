import axiosClient from "./axiosClient";

// Quan ly cac API su kien concert tu tao lap, phe duyet den cong bo danh cho Guest, Organizer va Admin
const eventApi = {
  // Lay danh sach su kien co loc va phan trang danh cho Guest va Customer
  getList: (filter = {}) => axiosClient.get("/events", { params: filter }),

  // Lay danh sach su kien noi bat trang chu danh cho Guest va Customer
  getFeatured: (count = 5) =>
    axiosClient.get("/events/featured", { params: { count } }),

  // Lay danh sach su kien theo thanh pho danh cho Guest va Customer
  getByCity: (city, filter = {}) =>
    axiosClient.get(`/events/city/${encodeURIComponent(city)}`, {
      params: filter,
    }),

  // Lay danh sach su kien theo danh muc danh cho Guest va Customer
  getByCategory: (categoryId, filter = {}) =>
    axiosClient.get(`/events/category/${categoryId}`, { params: filter }),

  // Lay thong tin chi tiet su kien theo slug danh cho Guest va Customer
  getBySlug: (slug) => axiosClient.get(`/events/slug/${slug}`),

  // Lay thong tin chi tiet su kien theo id danh cho Guest va Customer
  getById: (id) => axiosClient.get(`/events/${id}`),

  // Kiem tra tinh duy nhat cua slug khi nhap lieu danh cho Organizer
  checkSlug: (slug, excludeEventId = null) =>
    axiosClient.get("/events/slug-availability", {
      params: { slug, ...(excludeEventId ? { excludeEventId } : {}) },
    }),

  // Lay danh sach su kien do toi tao danh cho Organizer
  getMine: () => axiosClient.get("/events/mine"),

  // Lay chi tiet day du su kien cua toi o moi trang thai danh cho Organizer
  getMineById: (id) => axiosClient.get(`/events/mine/${id}`),

  // Lay so lieu tong quan tren dashboard danh cho Organizer
  getMyDashboard: () => axiosClient.get("/events/mine/dashboard"),

  // Tao moi su kien o trang thai Draft danh cho Organizer
  create: (dto) => axiosClient.post("/events", dto),

  // Cap nhat thong tin su kien o trang thai Draft hoac Rejected danh cho Organizer
  update: (id, dto) => axiosClient.put(`/events/${id}`, dto),

  // Xoa mem su kien o trang thai Draft danh cho Organizer
  remove: (id) => axiosClient.delete(`/events/${id}`),

  // Kiem tra thu tinh day du cua su kien truoc khi submit danh cho Organizer
  validateForSubmission: (id) => axiosClient.get(`/events/${id}/validate`),

  // Gui yeu cau xet duyet su kien len Admin danh cho Organizer
  submit: (id) => axiosClient.post(`/events/${id}/submit`),

  // Huy su kien da cong bo danh cho Organizer va Admin
  cancel: (id, payload) => {
    if (payload instanceof FormData) {
      return axiosClient.post(`/events/${id}/cancel`, payload, {
        headers: { "Content-Type": "multipart/form-data" },
      });
    }
    return axiosClient.post(`/events/${id}/cancel`, typeof payload === 'string' ? { reason: payload } : (payload || {}));
  },

  // Tai len anh poster su kien danh cho Organizer
  uploadPoster: (id, file) => {
    const form = new FormData();
    form.append("file", file);
    return axiosClient.post(`/events/${id}/poster`, form, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },
  deletePoster: (id) => axiosClient.delete(`/events/${id}/poster`),

  // Tai len anh banner su kien danh cho Organizer
  uploadBanner: (id, file) => {
    const form = new FormData();
    form.append("file", file);
    return axiosClient.post(`/events/${id}/banner`, form, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },
  deleteBanner: (id) => axiosClient.delete(`/events/${id}/banner`),

  // Lay danh sach su kien dang cho xet duyet danh cho Admin
  getPending: (page = 1, pageSize = 12) =>
    axiosClient.get("/events/pending", { params: { page, pageSize } }),

  // Lay tat ca su kien tren he thong danh cho Admin
  getAllAdmin: (filter = {}) =>
    axiosClient.get("/events/admin", { params: filter }),

  // Lay chi tiet su kien danh cho Admin
  adminGetById: (id) => axiosClient.get(`/events/admin/${id}`),

  // Phe duyet su kien de chuyen sang Published danh cho Admin
  approve: (id) => axiosClient.post(`/events/${id}/approve`),

  // Tu choi su kien kem ly do giai thich danh cho Admin
  reject: (id, reason) => axiosClient.post(`/events/${id}/reject`, { reason }),

  // ---- Event Gates (Cổng sự kiện và phân loại vé) ----
  getGates: (eventId) => axiosClient.get(`/events/${eventId}/gates`),
  createGate: (eventId, data) => axiosClient.post(`/events/${eventId}/gates`, data),
  updateGate: (eventId, gateId, data) => axiosClient.put(`/events/${eventId}/gates/${gateId}`, data),
  deleteGate: (eventId, gateId) => axiosClient.delete(`/events/${eventId}/gates/${gateId}`),
};

export default eventApi;
