import { useEffect, useState } from "react";
import { requestApi } from "./api/client";
import type { OngoingHolidayRequest } from "./api/Api";
import { errorText } from "./api/errors";
import { EditRequestDialog } from "./dialog/EditRequestDialog";
import { formatCreatedAt, formatHours, holidayKindLabel } from "./requestFormat";

// pending requests of the employee. the only action is Edit: new dates, or withdraw the request inside the dialog
export function OnGoingRequest() {
    const [requests, setRequests] = useState<OngoingHolidayRequest[]>([]);
    const [error, setError] = useState<string | null>(null);
    const [loading, setLoading] = useState(true);
    const [editing, setEditing] = useState<OngoingHolidayRequest | null>(null);

    async function load() {
        try {
            setRequests((await requestApi.getOngoingRequests()) ?? []);
            setError(null);
        } catch (reason) {
            setError(errorText(reason, "Could not load your requests."));
        } finally {
            setLoading(false);
        }
    }

    useEffect(() => {
        load();
    }, []);

    return (
        <section className="ongoing-requests">
            {editing && <EditRequestDialog request={editing} onClose={() => setEditing(null)} onChanged={load} />}
            <header className="ongoing-requests__header">
                <div>
                    <p className="ongoing-requests__eyebrow">HOLIDAY MANAGEMENT</p>
                    <h2>Ongoing Requests</h2>
                    <p className="ongoing-requests__subtitle">Requests waiting for the accountant. You can still change their dates or withdraw them.</p>
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
                            {requests.map((request) => (
                                <tr key={request.id}>
                                    <td>
                                        <span className="ongoing-requests__kind">{holidayKindLabel(request)}</span>
                                        {request.isMainHoliday && <span className="ongoing-requests__tag">Main holiday</span>}
                                    </td>
                                    <td>{request.startDate} <span className="ongoing-requests__date-separator">to</span> {request.endDate}</td>
                                    <td>{formatHours(request.hours)}</td>
                                    <td>{formatCreatedAt(request.createdAt)}</td>
                                    <td>
                                        <span className="ongoing-requests__status">
                                            <span className="ongoing-requests__status-dot" aria-hidden="true" />
                                            Pending
                                        </span>
                                    </td>
                                    <td>
                                        <div className="ongoing-requests__actions">
                                            <button className="ongoing-requests__action-button ongoing-requests__action-button--edit"
                                                    type="button" onClick={() => setEditing(request)}>Edit</button>
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
