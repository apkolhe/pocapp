import axios from "axios";

export default axios.create({
  // baseURL: "http://localhost:7071/api", //localhost
  baseURL: "https://pocapi.azurewebsites.net/api",
  headers: {
    "Content-type": "application/json"
  }
});
