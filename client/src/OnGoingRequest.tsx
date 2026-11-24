import { useEffect, useState } from "react";
import { requestApi, type HolidayRequest } from "./api/client";

export function OnGoingRequest() {
    const [requests, setRequests] = useState<HolidayRequest[]>([]);
    const [error, setError] = useState<string | null>(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        let cancelled = false;

        requestApi.getOngoing()
            .then((result) => {
                if (!cancelled) {
                    setRequests(result);
                    setLoading(false);
                }
            })
            .catch((reason: unknown) => {
                if (!cancelled) {
                    setError(reason instanceof Error ? reason.message : "Could not load your requests.");
                    setLoading(false);
                }
            });

        return () => { cancelled = true; };
    }, []);

    return (
        <section className="ongoing-requests">
            <header className="ongoing-requests__header">
                <div>
                    <p className="ongoing-requests__eyebrow">HOLIDAY MANAGEMENT</p>
                    <h2>Ongoing Requests</h2>
                    <p className="ongoing-requests__subtitle">Track the status and details of your holiday requests.</p>
                </div>
                {!loading && !error && requests.length > 0 && (
                    <span className="ongoing-requests__count">{requests.length} {requests.length === 1 ? "request" : "requests"}</span>
                )}
            </header>

            {error ? (
                <p className="ongoing-requests__message ongoing-requests__message--error" role="alert">{error}</p>
            ) : loading ? (
                <p className="ongoing-requests__message" role="status">Loading your requests…</p>
            ) : requests.length === 0 ? (
                <p className="ongoing-requests__message">You have no pending holiday requests.</p>
            ) : (
                <div className="ongoing-requests__table-wrap">
                    <table className="ongoing-requests__table">
                        <thead>
                            <tr>
                                <th scope="col">Holiday type</th>
                                <th scope="col">Period</th>
                                <th scope="col">Hours</th>
                                <th scope="col">Created at</th>
                                <th scope="col">Status</th>
                                <th scope="col">Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            {requests.map((request, index) => (
                                <tr key={request.id ?? `${request.createdAt ?? "request"}-${index}`}>
                                    <td><span className="ongoing-requests__kind">{request.holidayKind === "law" ? "Law" : "Agreed"}</span></td>
                                    <td>{request.startDate} <span className="ongoing-requests__date-separator">to</span> {request.endDate}</td>
                                    <td>{request.hours ?? "—"}</td>
                                    <td>{request.createdAt ? formatCreatedAt(request.createdAt) : "—"}</td>
                                    <td>
                                        <span className="ongoing-requests__status">
                                            <span className="ongoing-requests__status-dot" aria-hidden="true" />
                                            {request.requestStatus === 2 ? "Pending" : request.requestStatus ?? "Unknown"}
                                        </span>
                                    </td>
                                    <td>
                                        <div className="ongoing-requests__actions">
                                            <button className="ongoing-requests__action-button ongoing-requests__action-button--edit" type="button">Edit</button>
                                            <button className="ongoing-requests__action-button ongoing-requests__action-button--cancel" type="button">Cancel</button>
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}
        </section>
    );
}

function formatCreatedAt(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}
