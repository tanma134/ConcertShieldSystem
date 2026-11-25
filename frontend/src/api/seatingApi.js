import axiosClient from "./axiosClient";

// Quan ly so do ghe ngoi va khu vuc cho concert danh cho Guest va Organizer
const seatingApi = {
  // Lay so do ghe day du cua concert danh cho Guest va Organizer
  getByEvent: (eventId) => axiosClient.get(`/seating/event/${eventId}`),

  // Xem truoc tong quan suc chua va khu vuc ghe danh cho Guest va Organizer
  getPreview: (eventId) => axiosClient.get(`/seating/event/${eventId}/preview`),

  // Lay danh sach cac ghe con trong trong mot khu vuc danh cho khach hang chon ve
  getZoneSeats: (seatZoneId, availableOnly = false) =>
    axiosClient.get(`/seating/zones/${seatZoneId}/seats`, {
      params: { availableOnly },
    }),

  // Khoi tao hoac ve lai toan bo so do ghe ngoi cua concert danh cho Organizer
  build: (eventId, dto) => axiosClient.post(`/seating/event/${eventId}`, dto),

  // Cap nhat metadata bo cuc san khau va kich thuoc canvas danh cho Organizer
  updateLayout: (eventId, layoutJson) =>
    axiosClient.put(`/seating/event/${eventId}/layout`, {
      layoutJson:
        typeof layoutJson === "string" ? layoutJson : JSON.stringify(layoutJson),
    }),

  // Them mot khu vuc ghe moi vao so do da co san danh cho Organizer
  addZone: (eventId, dto) =>
    axiosClient.post(`/seating/event/${eventId}/zones`, dto),

  // Cap nhat thong tin hinh hoc va hang ve lien ket cua khu vuc ghe danh cho Organizer
  updateZone: (seatZoneId, dto) =>
    axiosClient.put(`/seating/zones/${seatZoneId}`, dto),

  // Xoa mot khu vuc ghe khoi so do danh cho Organizer
  removeZone: (seatZoneId) =>
    axiosClient.delete(`/seating/zones/${seatZoneId}`),

  // Xoa toan bo so do ghe va dua concert ve dang ve tu do danh cho Organizer
  remove: (eventId) => axiosClient.delete(`/seating/event/${eventId}`),
};

export default seatingApi;
