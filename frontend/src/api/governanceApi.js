import client from './axiosClient';
const base = id => `/events/${id}/governance`;

// Quan ly cac API goi den backend EventAPI Governance Module
export const governanceApi = {
  // Lay danh sach tai lieu phap ly va lich su xet duyet
  compliance: id => client.get(`${base(id)}/compliance`),

  // Tai len tai lieu phap ly moi cho su kien
  upload: (id, type, file) => {
    const data = new FormData();
    data.append('documentType', type);
    data.append('file', file);
    return client.post(`${base(id)}/compliance/documents`, data, { timeout: 170000 });
  },

  // Admin phe duyet hoac yeu cau bo sung ho so phap ly
  reviewCompliance: (id, input) => client.post(`${base(id)}/compliance/reviews`, input),

  // Tai ve tep tin phap ly da upload
  download: (id, doc) => client.get(`${base(id)}/compliance/documents/${doc}/download`, { responseType: 'blob' }),

  // Lay danh sach cac yeu cau thay doi lich cua su kien
  changes: id => client.get(`${base(id)}/changes`),

  // Uoc tinh tac dong cua thay doi len ve va doanh thu
  preview: (id, input) => client.post(`${base(id)}/changes/preview`, input),

  // Organizer gui de xuat hoan hoac doi ngay su kien
  submitChange: (id, input) => client.post(`${base(id)}/changes`, input),

  // Admin phe duyet hoac tu choi de xuat thay doi lich
  reviewChange: (id, request, input) => client.post(`${base(id)}/changes/${request}/review`, input),

  // Admin truc tiep thuc thi hoan hoac doi ngay trong truong hop khan cap
  adminDirectChange: (id, input) => client.post(`${base(id)}/changes/admin-direct`, input),

  // Lay danh sach toan bo yeu cau thay doi tren toan he thong cho Admin
  allChanges: status => client.get('/admin/change-requests', { params: status ? { status } : {} }),

  // Lay danh sach chi tiet ve va don hang bi anh huong
  affected: (id, request, filters) => client.get(`${base(id)}/changes/${request}/affected`, { params: filters }),
};
