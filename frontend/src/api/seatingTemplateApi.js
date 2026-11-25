import axiosClient from "./axiosClient";

const seatingTemplateApi = {
  getVisible: () => axiosClient.get(`/seating-templates`),

  getById: (id) => axiosClient.get(`/seating-templates/${id}`),

  saveFromEvent: (dto) =>
    axiosClient.post(`/seating-templates/from-event`, dto),

  create: (dto) => axiosClient.post(`/seating-templates`, dto),

  update: (id, dto) => axiosClient.put(`/seating-templates/${id}`, dto),

  remove: (id) => axiosClient.delete(`/seating-templates/${id}`),

  apply: (templateId, eventId, dto) =>
    axiosClient.post(
      `/seating-templates/${templateId}/apply/${eventId}`,
      dto
    ),
};

export default seatingTemplateApi;
