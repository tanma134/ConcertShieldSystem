import axiosClient from "./axiosClient";

const eventApi = {
  getList: (filter = {}) => axiosClient.get("/events", { params: filter }),

  getFeatured: (count = 5) =>
    axiosClient.get("/events/featured", { params: { count } }),

  getByCity: (city, filter = {}) =>
    axiosClient.get(`/events/city/${encodeURIComponent(city)}`, {
      params: filter,
    }),

  getByCategory: (categoryId, filter = {}) =>
    axiosClient.get(`/events/category/${categoryId}`, { params: filter }),

  getBySlug: (slug) => axiosClient.get(`/events/slug/${slug}`),

  getById: (id) => axiosClient.get(`/events/${id}`),

  checkSlug: (slug, excludeEventId = null) =>
    axiosClient.get("/events/slug-availability", {
      params: { slug, ...(excludeEventId ? { excludeEventId } : {}) },
    }),

  getMine: () => axiosClient.get("/events/mine"),

  getMineById: (id) => axiosClient.get(`/events/mine/${id}`),

  getMyDashboard: () => axiosClient.get("/events/mine/dashboard"),

  create: (dto) => axiosClient.post("/events", dto),

  update: (id, dto) => axiosClient.put(`/events/${id}`, dto),

  remove: (id) => axiosClient.delete(`/events/${id}`),

  validateForSubmission: (id) => axiosClient.get(`/events/${id}/validate`),

  submit: (id) => axiosClient.post(`/events/${id}/submit`),

  cancel: (id, payload) => {
    if (payload instanceof FormData) {
      return axiosClient.post(`/events/${id}/cancel`, payload, {
        headers: { "Content-Type": "multipart/form-data" },
      });
    }
    return axiosClient.post(`/events/${id}/cancel`, typeof payload === 'string' ? { reason: payload } : (payload || {}));
  },

  uploadPoster: (id, file) => {
    const form = new FormData();
    form.append("file", file);
    return axiosClient.post(`/events/${id}/poster`, form, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },
  deletePoster: (id) => axiosClient.delete(`/events/${id}/poster`),

  uploadBanner: (id, file) => {
    const form = new FormData();
    form.append("file", file);
    return axiosClient.post(`/events/${id}/banner`, form, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },
  deleteBanner: (id) => axiosClient.delete(`/events/${id}/banner`),

  getPending: (page = 1, pageSize = 12) =>
    axiosClient.get("/events/pending", { params: { page, pageSize } }),

  getAllAdmin: (filter = {}) =>
    axiosClient.get("/events/admin", { params: filter }),

  adminGetById: (id) => axiosClient.get(`/events/admin/${id}`),

  approve: (id) => axiosClient.post(`/events/${id}/approve`),

  reject: (id, reason) => axiosClient.post(`/events/${id}/reject`, { reason }),

  // ---- Event Gates (Cổng sự kiện và phân loại vé) ----
  getGates: (eventId) => axiosClient.get(`/events/${eventId}/gates`),
  createGate: (eventId, data) => axiosClient.post(`/events/${eventId}/gates`, data),
  updateGate: (eventId, gateId, data) => axiosClient.put(`/events/${eventId}/gates/${gateId}`, data),
  deleteGate: (eventId, gateId) => axiosClient.delete(`/events/${eventId}/gates/${gateId}`),
};

export default eventApi;
