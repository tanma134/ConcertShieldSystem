import axiosClient from "./axiosClient";

const eventImageApi = {
  getByEvent: (eventId) => axiosClient.get(`/eventimages/event/${eventId}`),

  upload: (eventId, file) => {
    const form = new FormData();
    form.append("file", file);
    return axiosClient.post(`/eventimages/event/${eventId}/upload`, form, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },

  uploadMultiple: (eventId, files) => {
    const form = new FormData();
    files.forEach((f) => form.append("files", f));
    return axiosClient.post(
      `/eventimages/event/${eventId}/upload-multiple`,
      form,
      { headers: { "Content-Type": "multipart/form-data" } }
    );
  },

  setMain: (imageId) => axiosClient.post(`/eventimages/${imageId}/set-main`),
  reorder: (eventId, orders) =>
    axiosClient.put("/eventimages/reorder", orders, {
      params: { eventId },
    }),
  remove: (imageId) => axiosClient.delete(`/eventimages/${imageId}`),
};

export default eventImageApi;
