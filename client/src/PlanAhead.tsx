import { useEffect, useState } from "react";
import { userApi } from "./api/client";
import { type HolidayAccountView, type ProjectedAccountView, type ProjectedBalanceView } from "./api/Api";
import { errorText } from "./api/errors";
import { accountTitle } from "./HolidayBalance";

function hours(value?: number): string {
    return `${(value ?? 0).toFixed(2)} h`;
}

function days(value: number | undefined, dayHours: number | undefined): string {
    if (!dayHours) return "";
    return `≈ ${((value ?? 0) / dayHours).toFixed(1)} days`;
}

function title(account: ProjectedAccountView): string {
    // the same fields as HolidayAccountView are used for the title
    return accountTitle(account as HolidayAccountView);
}

// "how much holiday will I have on ...": today's balances - pending requests + what is still earned until that date.
// a projection only, calculated by the server like the monthly accrual
export function PlanAhead({ initialDate }: { initialDate?: string }) {
    const [date, setDate] = useState(initialDate ?? "");
    const [result, setResult] = useState<ProjectedBalanceView | null>(null);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        if (!date) return;
        let cancelled = false;
        userApi.projected_balance(date)
            .then((r) => {
                if (cancelled) return;
                setResult(r);
                setError(null);
            })
            .catch((reason) => {
                if (cancelled) return;
                setResult(null);
                setError(errorText(reason, "Could not calculate the balance for this date."));
            });
        return () => {
            cancelled = true;
        };
    }, [date]);

    const accounts = result?.accounts ?? [];
    const expiring = accounts.filter((a) => a.state === "expired" && (a.projected_hours ?? 0) > 0);
    const later = accounts.filter((a) => a.state === "not_yet" && (a.projected_hours ?? 0) > 0);

    return (
        <section className="plan-ahead">
            <header className="plan-ahead__header">
                <div>
                    <h2>Plan ahead</h2>
                    <p className="ongoing-requests__subtitle">How much holiday you will have on a date, with what you earn until then and your pending requests reserved.</p>
                </div>
                <label className="plan-ahead__date">
                    <span>Date</span>
                    <input type="date" value={date} min={result ? String(result.today) : undefined}
                           onChange={(e) => setDate(e.target.value)} />
                </label>
            </header>

            {error && <p className="ongoing-requests__message ongoing-requests__message--error" role="alert">{error}</p>}

            {result && (
                <>
                    <div className="plan-ahead__totals">
                        <div>
                            <span>Weeks 1-5</span>
                            <strong className={(result.law_hours ?? 0) < 0 ? "is-negative" : ""}>{hours(result.law_hours)}</strong>
                            <small>{days(result.law_hours, result.day_hours)}</small>
                        </div>
                        <div>
                            <span>Week 6</span>
                            <strong>{hours(result.agreed_hours)}</strong>
                            <small>{days(result.agreed_hours, result.day_hours)}</small>
                        </div>
                    </div>

                    {(result.debt_hours ?? 0) > 0 && (
                        <p className="plan-ahead__note">
                            You could also borrow up to <strong>{hours(result.debt_hours)}</strong> of weeks 1-5 holiday
                            (holiday debt), paid back by what you earn until 31 August.
                        </p>
                    )}
                    {expiring.map((a, i) => (
                        <p key={`e${i}`} className="plan-ahead__note plan-ahead__note--warning">
                            {hours(a.projected_hours)} of {title(a)} can only be used until {String(a.usage_period_end)} -
                            before this date. Plan them before, or ask the accountant to transfer them.
                        </p>
                    ))}
                    {later.map((a, i) => (
                        <p key={`l${i}`} className="plan-ahead__note">
                            {hours(a.projected_hours)} of {title(a)} can be used from {String(a.usage_period_start)}.
                        </p>
                    ))}

                    <details className="plan-ahead__details">
                        <summary>How it is calculated</summary>
                        <table className="ongoing-requests__table">
                            <thead>
                                <tr>
                                    <th>Balance</th>
                                    <th>Available today</th>
                                    <th>Pending requests</th>
                                    <th>Still earned</th>
                                    <th>On {String(result.date)}</th>
                                </tr>
                            </thead>
                            <tbody>
                                {accounts.map((a, i) => (
                                    <tr key={a.id ?? `future-${i}`}>
                                        <td>
                                            {title(a)}
                                            {!a.id && <span className="ongoing-requests__tag">starts later</span>}
                                            {a.state !== "usable" && (
                                                <span className="ongoing-requests__tag">{a.state === "expired" ? "expired by then" : "not usable yet"}</span>
                                            )}
                                        </td>
                                        <td>{hours(a.available_now)}</td>
                                        <td>{a.pending_hours ? `− ${hours(a.pending_hours)}` : "—"}</td>
                                        <td>{a.future_accrual ? `+ ${hours(a.future_accrual)}` : "—"}</td>
                                        <td><strong>{hours(a.projected_hours)}</strong></td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </details>
                </>
            )}
        </section>
    );
}
