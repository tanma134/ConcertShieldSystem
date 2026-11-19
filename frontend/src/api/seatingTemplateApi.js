import axiosClient from "./axiosClient";

// Quan ly va ap dung mau so do ghe ngoi danh cho Organizer va Admin
const seatingTemplateApi = {
  // Lay danh sach cac mau so do ghe kha dung danh cho Organizer va Admin
  getVisible: () => axiosClient.get(`/seating-templates`),

  // Lay thong tin chi tiet mau so do ghe theo id danh cho Organizer va Admin
  getById: (id) => axiosClient.get(`/seating-templates/${id}`),

  // Luu so do ghe hien tai cua concert thanh mau mau tai su dung danh cho Organizer
  saveFromEvent: (dto) =>
    axiosClient.post(`/seating-templates/from-event`, dto),

  // Tao mau so do ghe moi truc tiep tu canvas danh cho Organizer va Admin
  create: (dto) => axiosClient.post(`/seating-templates`, dto),

  // Cap nhat mau so do ghe danh cho chu so huu hoac Admin
  update: (id, dto) => axiosClient.put(`/seating-templates/${id}`, dto),

  // Xoa mau so do ghe danh cho chu so huu hoac Admin
  remove: (id) => axiosClient.delete(`/seating-templates/${id}`),

  // Ap dung mau so do ghe co san vao concert danh cho Organizer
  apply: (templateId, eventId, dto) =>
    axiosClient.post(
      `/seating-templates/${templateId}/apply/${eventId}`,
      dto
    ),
};

export default seatingTemplateApi;
