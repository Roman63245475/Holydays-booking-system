import { useEffect, useState } from "react";
import { requestApi } from "./api/client";
import type { OngoingHolidayRequest } from "./api/Api";
import { errorText } from "./api/errors";
import { CancelHolidayDialog } from "./dialog/CancelHolidayDialog";
import { formatHours, holidayKindLabel } from "./requestFormat";

// approved holidays of the employee that are not over yet. an approved holiday can only be cancelled by a
// cancellation request to the accountant, and only before it starts
export function ApprovedRequests() {
    const [requests, setRequests] = useState<OngoingHolidayRequest[]>([]);
    const [error, setError] = useState<string | null>(null);
    const [loading, setLoading] = useState(true);
    const [cancelling, setCancelling] = useState<OngoingHolidayRequest | null>(null);

    async function load() {
        try {
            setRequests((await requestApi.getApprovedRequests()) ?? []);
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
        </section>
    );
}
