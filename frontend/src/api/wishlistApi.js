import axiosClient from "./axiosClient";

const unwrap = (response) => response.data?.data ?? response.data;

const wishlistApi = {
  getMine: async () => unwrap(await axiosClient.get("/wishlist")),
  getStatus: async (eventId) => unwrap(await axiosClient.get(`/wishlist/events/${eventId}/exists`)),
  add: (eventId) => axiosClient.post(`/wishlist/events/${eventId}`),
  remove: (eventId) => axiosClient.delete(`/wishlist/events/${eventId}`),
  getAdminSummary: async (params = {}) => unwrap(await axiosClient.get("/wishlist/admin/summary", { params })),
};

export default wishlistApi;
