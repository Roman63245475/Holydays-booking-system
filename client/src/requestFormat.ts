import type {OngoingHolidayRequest} from "./api/Api";

// shared by the Ongoing Requests and Approved Holidays tabs

export function holidayKindLabel(request: OngoingHolidayRequest): string {
    return request.holidayKind === "law" ? "Weeks 1-5" : "Week 6";
}

// typed as Date by the generated client, but JSON gives a string at runtime
export function formatCreatedAt(value?: Date | string): string {
    if (!value) return "—";
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleString();
}

export function formatHours(hours?: number): string {
    return hours === undefined || hours === null ? "—" : `${hours.toFixed(2)} h`;
}
