import client from './axiosClient';
const base = id => `/events/${id}/governance`;
export const governanceApi = {
  compliance: id => client.get(`${base(id)}/compliance`),
  upload: (id,type,file) => { const data=new FormData(); data.append('documentType',type); data.append('file',file); return client.post(`${base(id)}/compliance/documents`,data,{timeout:170000}); },
  reviewCompliance: (id,input) => client.post(`${base(id)}/compliance/reviews`,input),
  download: (id,doc) => client.get(`${base(id)}/compliance/documents/${doc}/download`,{responseType:'blob'}),
  changes: id => client.get(`${base(id)}/changes`),
  preview: (id,input) => client.post(`${base(id)}/changes/preview`,input),
  submitChange: (id,input) => client.post(`${base(id)}/changes`,input),
  reviewChange: (id,request,input) => client.post(`${base(id)}/changes/${request}/review`,input),
  allChanges: status => client.get('/admin/change-requests',{params:status?{status}:{}}),
  affected: (id,request,filters) => client.get(`${base(id)}/changes/${request}/affected`,{params:filters}),
};
