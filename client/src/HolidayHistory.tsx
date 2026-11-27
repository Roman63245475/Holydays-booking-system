import { useEffect, useState } from "react";
import { requestApi } from "./api/client";
import { HolidayType, RequestStatus, type HolidayHistoryView } from "./api/Api";
import { errorText } from "./api/errors";
import { formatCreatedAt, formatHours } from "./requestFormat";

const STATUS: Record<number, { label: string; modifier: string }> = {
    [RequestStatus.Accepted]: { label: "Approved", modifier: "approved" },
    [RequestStatus.Rejected]: { label: "Declined", modifier: "rejected" },
    [RequestStatus.Cancelled]: { label: "Cancelled", modifier: "cancelled" },
    [RequestStatus.Withdrawn]: { label: "Withdrawn", modifier: "withdrawn" },
};

// every request of the employee that is not pending anymore, newest first, with what happened to it.
// without userId: the logged in user's own history; with userId: any user's (system owner only)
export function HolidayHistory({ userId, title = "Holiday History" }: { userId?: string; title?: string }) {
    const [rows, setRows] = useState<HolidayHistoryView[]>([]);
    const [error, setError] = useState<string | null>(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        (userId ? requestApi.getUserHistory(userId) : requestApi.getHistory())
            .then((result) => {
                setRows(result ?? []);
                setError(null);
            })
            .catch((reason) => setError(errorText(reason, "Could not load the holiday history.")))
            .finally(() => setLoading(false));
    }, [userId]);

    return (
        <section className="ongoing-requests">
            <header className="ongoing-requests__header">
                <div>
                    <p className="ongoing-requests__eyebrow">HOLIDAY MANAGEMENT</p>
                    <h2>{title}</h2>
                    <p className="ongoing-requests__subtitle">All requests that are decided: approved, declined, withdrawn or cancelled.</p>
                </div>
                {!loading && !error && rows.length > 0 && (
                    <span className="ongoing-requests__count">{rows.length} {rows.length === 1 ? "request" : "requests"}</span>
                )}
            </header>

            {error ? (
                <p className="ongoing-requests__message ongoing-requests__message--error" role="alert">{error}</p>
            ) : loading ? (
                <p className="ongoing-requests__message" role="status">Loading the history…</p>
            ) : rows.length === 0 ? (
                <p className="ongoing-requests__message">No decided requests yet.</p>
            ) : (
                <div className="ongoing-requests__table-wrap">
                    <table className="ongoing-requests__table">
                        <thead>
                            <tr>
                                <th scope="col">Holiday type</th>
                                <th scope="col">Period</th>
                                <th scope="col">Hours</th>
                                <th scope="col">Status</th>
                                <th scope="col">Requested</th>
                                <th scope="col">Decided</th>
                                <th scope="col">What happened</th>
                            </tr>
                        </thead>
                        <tbody>
                            {rows.map((row) => {
                                const status = STATUS[row.status ?? -1] ?? { label: "—", modifier: "withdrawn" };
                                return (
                                    <tr key={row.id}>
                                        <td className="holiday-history__nowrap">
                                            <span className="ongoing-requests__kind">
                                                {row.holiday_type === HolidayType.FerieUge ? "Week 6" : "Weeks 1-5"}
                                            </span>
                                            {row.is_main_holiday && <span className="ongoing-requests__tag">Main holiday</span>}
                                        </td>
                                        <td className="holiday-history__nowrap">{row.start_date} <span className="ongoing-requests__date-separator">to</span> {row.end_date}</td>
                                        <td className="holiday-history__nowrap">{formatHours(row.hours)}</td>
                                        <td>
                                            <span className={`ongoing-requests__status ongoing-requests__status--${status.modifier}`}>
                                                <span className="ongoing-requests__status-dot" aria-hidden="true" />
                                                {status.label}
                                            </span>
                                        </td>
                                        <td>{formatCreatedAt(row.created_at)}</td>
                                        <td>{row.decided_at ? formatCreatedAt(row.decided_at) : "—"}</td>
                                        <td className="holiday-history__note">{row.note ?? "—"}</td>
                                    </tr>
                                );
                            })}
                        </tbody>
                    </table>
                </div>
            )}
        </section>
    );
}
