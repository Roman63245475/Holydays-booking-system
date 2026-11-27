import { useEffect, useState } from "react";
import { transferApi } from "./api/client";
import { HolidayType, RequestStatus, type TransferRequestView } from "./api/Api";
import { errorText } from "./api/errors";
import { day, hours } from "./balanceStatus";
import { formatCreatedAt } from "./requestFormat";

const STATUS: Record<number, { label: string; modifier: string }> = {
    [RequestStatus.Pending]: { label: "Waiting", modifier: "pending" },
    [RequestStatus.Accepted]: { label: "Approved", modifier: "approved" },
    [RequestStatus.Rejected]: { label: "Declined", modifier: "rejected" },
    [RequestStatus.Withdrawn]: { label: "Withdrawn", modifier: "withdrawn" },
};

function period(t: TransferRequestView): string {
    const start = day(t.earning_period_start).slice(0, 4);
    const end = day(t.earning_period_end).slice(0, 4);
    const years = start === end ? start : `${start}/${end.slice(2)}`;
    return `${t.holiday_type === HolidayType.FerieUge ? "Week 6" : "Weeks 1-5"} · earned ${years}`;
}

// the employee's transfer requests (unused holiday moved to the next holiday period), newest first.
// renders nothing when there are none, transfers are rare
export function TransferRequests() {
    const [rows, setRows] = useState<TransferRequestView[]>([]);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        transferApi.getMyTransfers()
            .then((result) => setRows(result ?? []))
            .catch((reason) => setError(errorText(reason, "Could not load your transfer requests.")));
    }, []);

    if (error) return <p className="error" role="alert">{error}</p>;
    if (rows.length === 0) return null;

    return (
        <section className="ongoing-requests">
            <header className="ongoing-requests__header">
                <div>
                    <p className="ongoing-requests__eyebrow">HOLIDAY MANAGEMENT</p>
                    <h2>Transfer requests</h2>
                    <p className="ongoing-requests__subtitle">Unused holiday you asked to move to the next holiday period. An approved transfer is the written agreement.</p>
                </div>
            </header>
            <div className="ongoing-requests__table-wrap">
                <table className="ongoing-requests__table">
                    <thead>
                        <tr>
                            <th scope="col">From balance</th>
                            <th scope="col">Hours</th>
                            <th scope="col">Status</th>
                            <th scope="col">Requested</th>
                            <th scope="col">Deadline</th>
                            <th scope="col">Note</th>
                        </tr>
                    </thead>
                    <tbody>
                        {rows.map((t) => {
                            const status = STATUS[t.status ?? -1] ?? { label: "—", modifier: "withdrawn" };
                            return (
                                <tr key={t.id}>
                                    <td className="holiday-history__nowrap">{period(t)}</td>
                                    <td className="holiday-history__nowrap">{hours(t.hours)}</td>
                                    <td>
                                        <span className={`ongoing-requests__status ongoing-requests__status--${status.modifier}`}>
                                            <span className="ongoing-requests__status-dot" aria-hidden="true" />
                                            {status.label}
                                        </span>
                                    </td>
                                    <td>{formatCreatedAt(t.created_at)}</td>
                                    <td className="holiday-history__nowrap">{day(t.deadline)}</td>
                                    <td className="holiday-history__note">{t.note ?? t.comment ?? "—"}</td>
                                </tr>
                            );
                        })}
                    </tbody>
                </table>
            </div>
        </section>
    );
}
