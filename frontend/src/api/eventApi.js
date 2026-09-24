import axiosClient from "./axiosClient";

const eventApi = {
  // ---- Public reads (Published only) ----
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
  checkSlug: (slug, excludeEventId = null) =>
    axiosClient.get("/events/slug-availability", {
      params: { slug, ...(excludeEventId ? { excludeEventId } : {}) },
    }),

  // ---- Customer: my concerts (any status) ----
  getMine: () => axiosClient.get("/events/mine"),
  getMineById: (id) => axiosClient.get(`/events/mine/${id}`),
  getMyDashboard: () => axiosClient.get("/events/mine/dashboard"),

  // ---- Draft lifecycle ----
  create: (dto) => axiosClient.post("/events", dto),
  update: (id, dto) => axiosClient.put(`/events/${id}`, dto),
  remove: (id) => axiosClient.delete(`/events/${id}`),

  validateForSubmission: (id) => axiosClient.get(`/events/${id}/validate`),
  submit: (id) => axiosClient.post(`/events/${id}/submit`),
  cancel: (id) => axiosClient.post(`/events/${id}/cancel`),

  // ---- Poster / banner (multipart) ----
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

  // ---- Admin moderation ----
  getPending: (page = 1, pageSize = 12) =>
    axiosClient.get("/events/pending", { params: { page, pageSize } }),
  getAllAdmin: (filter = {}) =>
    axiosClient.get("/events/admin", { params: filter }),
  adminGetById: (id) => axiosClient.get(`/events/admin/${id}`),
  approve: (id) => axiosClient.post(`/events/${id}/approve`),
  reject: (id, reason) => axiosClient.post(`/events/${id}/reject`, { reason }),
};

export default eventApi;
