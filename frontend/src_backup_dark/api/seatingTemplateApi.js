import axiosClient from "./axiosClient";

const seatingTemplateApi = {
  // Mine + every public (admin-provided) template.
  getVisible: () => axiosClient.get(`/seating-templates`),

  getById: (id) => axiosClient.get(`/seating-templates/${id}`),

  // UC_26.3 - snapshot the already-built layout of one of my concerts.
  saveFromEvent: (dto) =>
    axiosClient.post(`/seating-templates/from-event`, dto),

  // Draw-from-scratch template, not tied to any concert.
  create: (dto) => axiosClient.post(`/seating-templates`, dto),

  update: (id, dto) => axiosClient.put(`/seating-templates/${id}`, dto),

  remove: (id) => axiosClient.delete(`/seating-templates/${id}`),

  // UC_26.2 - map each template zone to a ticket type, then build the chart.
  apply: (templateId, eventId, dto) =>
    axiosClient.post(
      `/seating-templates/${templateId}/apply/${eventId}`,
      dto
    ),
};

export default seatingTemplateApi;
