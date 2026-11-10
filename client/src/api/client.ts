import { ApiClient } from "./Api";

export const api = new ApiClient("http://localhost:5232", {
  fetch: (url, init) => fetch(url, { ...init, credentials: "include" }),
});
