import axiosClient from "./axiosClient";

// Removes empty filters so they are not sent as "?from=&to=".
const cleanParams = (params = {}) =>
  Object.fromEntries(Object.entries(params).filter(([, value]) => value !== "" && value != null));

// Reads the file name from Content-Disposition, falling back to the given name.
const fileNameFrom = (response, fallback) => {
  const header = response.headers?.["content-disposition"] || "";
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(header);
  return match ? decodeURIComponent(match[1]) : fallback;
};

// Saves a blob response as a file in the browser.
export function saveBlob(response, fallbackName) {
  const url = window.URL.createObjectURL(new Blob([response.data], { type: "text/csv;charset=utf-8" }));
  const link = document.createElement("a");
  link.href = url;
  link.download = fileNameFrom(response, fallbackName);
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.URL.revokeObjectURL(url);
}

// UC_13 and UC_14.2: organizer reports of one event.
const reportApi = {
  // UC_13.1 + UC_13.2: revenue summary; params = { from, to, ticketTypeId }.
  getRevenue: (eventId, params) =>
    axiosClient.get(`/organizer/events/${eventId}/revenue`, { params: cleanParams(params) }),

  // UC_13.3: the same report as a CSV file.
  exportRevenue: (eventId, params) =>
    axiosClient.get(`/organizer/events/${eventId}/revenue/export`, {
      params: cleanParams(params),
      responseType: "blob",
    }),

  // UC_14.2: check-in figures shown next to the export button.
  getCheckin: (eventId) => axiosClient.get(`/organizer/events/${eventId}/checkin`),

  // UC_14.2: check-in report as a CSV file.
  exportCheckin: (eventId) =>
    axiosClient.get(`/organizer/events/${eventId}/checkin/export`, { responseType: "blob" }),
};

export default reportApi;
