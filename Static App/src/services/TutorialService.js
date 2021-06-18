import http from "../http-common";

const getAll = () => {  
  return http.get("/tutorials");
};

const get = uniqueId => {  
  return http.get(`/tutorials/${uniqueId}`);
};

const create = data => {
  return http.post("/tutorials", data);
};

const update = (uniqueId, data) => {  
  return http.put(`/tutorials/${uniqueId}`, data);
};

const remove = (uniqueId) => {  
  return http.delete(`/tutorials/${uniqueId}`);
};

const removeAll = () => {
  return http.delete(`/tutorials`);
};

const findByTitle = title => {
  return http.get(`/tutorials/search/title?title=${title}`);
};

export default {
  getAll,
  get,
  create,
  update,
  remove,
  removeAll,
  findByTitle
};
