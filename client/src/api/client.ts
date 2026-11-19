import {DayOffClient, LoginClient, RuleSetClient, TimeClient, UserClient} from "./Api";

const baseUrl = "http://localhost:5232";

// credentials: "include" sends the session cookie with every request
const http = {
  fetch: (url: RequestInfo, init?: RequestInit) => fetch(url, { ...init, credentials: "include" }),
};

export const loginApi = new LoginClient(baseUrl, http);
export const userApi = new UserClient(baseUrl, http);
export const timeApi = new TimeClient(baseUrl, http);
export const ruleSetApi = new RuleSetClient(baseUrl, http);
export const dayOffApi = new DayOffClient(baseUrl, http);

export interface CreateRequestDTO {
  startDate: string;
  endDate: string;
  hours: number;
  holidayKind: "law" | "agreed";
}

export const requestApi = {
  async create(request: CreateRequestDTO): Promise<void> {
    const response = await http.fetch(`${baseUrl}/request/create`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Accept": "application/json",
      },
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      throw new Error(`Request submission failed (${response.status}): ${await response.text()}`);
    }
  },
};
