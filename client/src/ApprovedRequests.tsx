import { useEffect, useState } from "react";
import { requestApi } from "./api/client";
import type { CancelledHolidayView, OngoingHolidayRequest } from "./api/Api";
import { errorText } from "./api/errors";
import { CancelHolidayDialog } from "./dialog/CancelHolidayDialog";
import { formatHours, holidayKindLabel } from "./requestFormat";

// approved holidays of the employee that are not over yet. an approved holiday can only be cancelled by a
// cancellation request to the accountant, and only before it starts.
// below: planned holidays the accountant cancelled or moved on the employer's decision, with the reason
export function ApprovedRequests() {
    const [requests, setRequests] = useState<OngoingHolidayRequest[]>([]);
    const [error, setError] = useState<string | null>(null);
    const [loading, setLoading] = useState(true);
    const [cancelling, setCancelling] = useState<OngoingHolidayRequest | null>(null);
    const [cancelledByEmployer, setCancelledByEmployer] = useState<CancelledHolidayView[]>([]);

    async function load() {
        try {
            const [approved, cancelled] = await Promise.all([
                requestApi.getApprovedRequests(),
                requestApi.getChangedByEmployer()
            ]);
            setRequests(approved ?? []);
            setCancelledByEmployer(cancelled ?? []);
            setError(null);
        } catch (reason) {
            setError(errorText(reason, "Could not load your approved holidays."));
        } finally {
            setLoading(false);
        }
    }

    useEffect(() => {
        load();
    }, []);

    return (
        <section className="ongoing-requests">
            {cancelling && <CancelHolidayDialog request={cancelling} onClose={() => setCancelling(null)} onSent={load} />}
            <header className="ongoing-requests__header">
                <div>
                    <p className="ongoing-requests__eyebrow">HOLIDAY MANAGEMENT</p>
                    <h2>Approved Holidays</h2>
                    <p className="ongoing-requests__subtitle">Your planned holidays. To cancel one, send a cancellation request to the accountant before it starts.</p>
                </div>
                {!loading && !error && requests.length > 0 && (
                    <span className="ongoing-requests__count">{requests.length} {requests.length === 1 ? "holiday" : "holidays"}</span>
                )}
            </header>

            {error ? (
                <p className="ongoing-requests__message ongoing-requests__message--error" role="alert">{error}</p>
            ) : loading ? (
                <p className="ongoing-requests__message" role="status">Loading your holidays…</p>
            ) : requests.length === 0 ? (
                <p className="ongoing-requests__message">You have no approved holidays coming up.</p>
            ) : (
                <div className="ongoing-requests__table-wrap">
                    <table className="ongoing-requests__table">
                        <thead>
                            <tr>
                                <th scope="col">Holiday type</th>
                                <th scope="col">Period</th>
                                <th scope="col">Hours</th>
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
                                    <td>
                                        {request.hasPendingCancellation ? (
                                            <span className="ongoing-requests__status">
                                                <span className="ongoing-requests__status-dot" aria-hidden="true" />
                                                Cancellation pending
                                            </span>
                                        ) : (
                                            <span className="ongoing-requests__status ongoing-requests__status--approved">
                                                <span className="ongoing-requests__status-dot" aria-hidden="true" />
                                                {request.started ? "In progress" : "Approved"}
                                            </span>
                                        )}
                                    </td>
                                    <td>
                                        <div className="ongoing-requests__actions">
                                            <button className="ongoing-requests__action-button ongoing-requests__action-button--cancel"
                                                    type="button"
                                                    disabled={request.started || request.hasPendingCancellation}
                                                    title={request.started ? "A holiday that has started can not be cancelled"
                                                        : request.hasPendingCancellation ? "The accountant has not answered yet" : undefined}
                                                    onClick={() => setCancelling(request)}>Cancel</button>
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            {cancelledByEmployer.length > 0 && (
                <div className="cancelled-by-employer">
                    <h3>Changed by the accountant</h3>
                    <p className="ongoing-requests__subtitle">These approved holidays were cancelled or moved by the company. The hours of a cancelled holiday were returned to your balance.</p>
                    <ul>
                        {cancelledByEmployer.map(c => (
                            <li key={c.id}>
                                {c.movedToStartDate ? (
                                    <>
                                        Moved: <strong>{c.startDate} to {c.endDate}</strong> → <strong>{c.movedToStartDate} to {c.movedToEndDate}</strong>
                                        {" "}({holidayKindLabel({ holidayKind: c.holidayKind })}, {formatHours(c.movedToHours)})
                                    </>
                                ) : (
                                    <>
                                        Cancelled: <strong>{c.startDate} to {c.endDate}</strong>
                                        {" "}({holidayKindLabel({ holidayKind: c.holidayKind })}, {formatHours(c.hours)})
                                    </>
                                )}
                                <span className="request-reason"> Reason: {c.reason}</span>
                            </li>
                        ))}
                    </ul>
                </div>
            )}
        </section>
    );
}
