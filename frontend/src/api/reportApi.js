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

// Quan ly bao cao doanh thu va du lieu check-in danh cho Organizer va Admin
const reportApi = {
  // Lay du lieu dashboard va loc doanh thu concert danh cho Organizer va Admin
  getRevenue: (eventId, params) =>
    axiosClient.get(`/organizer/events/${eventId}/revenue`, { params: cleanParams(params) }),

  // Xuat file CSV bao cao doanh thu concert danh cho Organizer va Admin
  exportRevenue: (eventId, params) =>
    axiosClient.get(`/organizer/events/${eventId}/revenue/export`, {
      params: cleanParams(params),
      responseType: "blob",
    }),

  // Lay so lieu thong ke check-in tren man hinh danh cho Organizer va Admin
  getCheckin: (eventId) => axiosClient.get(`/organizer/events/${eventId}/checkin`),

  // Xuat file CSV bao cao check-in su kien danh cho Organizer va Admin
  exportCheckin: (eventId) =>
    axiosClient.get(`/organizer/events/${eventId}/checkin/export`, { responseType: "blob" }),
};

export default reportApi;
