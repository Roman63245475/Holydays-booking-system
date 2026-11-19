import {ApiException} from "./Api";

// readable text of an error returned by the backend.
// controllers with [ApiController] send a message as a JSON string ("..." with quotes)
// and a JSON that can not be read as a validation problem object { title, errors: { field: [messages] } }
export function errorText(err: unknown, fallback: string): string {
    if (!(err instanceof ApiException)) {
        return fallback;
    }
    const response = err.response ?? "";
    try {
        const parsed = JSON.parse(response);
        if (typeof parsed === "string") {
            return parsed;
        }
        if (parsed && typeof parsed === "object" && parsed.errors) {
            const messages = Object.values(parsed.errors as Record<string, string[]>).flat();
            return messages.length > 0 ? messages.join(" ") : (parsed.title ?? fallback);
        }
    } catch {
        // not JSON: a plain text message
    }
    return response || fallback;
}
