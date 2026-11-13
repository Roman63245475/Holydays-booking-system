import { LoginClient, UserClient } from "./Api";

const baseUrl = "http://localhost:5232";

// credentials: "include" sends the session cookie with every request
const http = {
  fetch: (url: RequestInfo, init?: RequestInit) => fetch(url, { ...init, credentials: "include" }),
};

export const loginApi = new LoginClient(baseUrl, http);
export const userApi = new UserClient(baseUrl, http);
