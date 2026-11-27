import { useEffect, useState } from "react";
import { transferApi } from "@/api/client.ts";
import { errorText } from "@/api/errors.ts";
import { HolidayType, type EmployeeBalanceRow } from "@/api/Api.ts";
import { day, flagMessage, hours } from "@/balanceStatus.ts";

// every holiday balance of every employee, with what has to be done with it: expires soon with unplanned hours,
// expired without being taken (payroll), final settlement of leaving employees
export function BalanceTab() {
    const [week6Active, setWeek6Active] = useState(false);
    const [onlyFlagged, setOnlyFlagged] = useState(false);
    const [rows, setRows] = useState<EmployeeBalanceRow[]>([]);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        transferApi.getBalanceOverview()
            .then((result) => setRows(result ?? []))
            .catch((reason) => setError(errorText(reason, "Could not load the balances.")));
    }, []);

    const type = week6Active ? HolidayType.FerieUge : HolidayType.FerieEfterFerieloven;
    const ofType = rows.filter((r) => r.account?.holiday_type === type);
    const flaggedCount = ofType.filter((r) => r.account?.flag && r.account.flag !== "none").length;
    const shown = onlyFlagged ? ofType.filter((r) => r.account?.flag && r.account.flag !== "none") : ofType;

    return (
        <div className={"flex center-v p-16"}>
            <h1>{week6Active ? "Week 6 Balances" : "Week 1-5 Balances"}</h1>
            {error && <p className={"error"} role="alert">{error}</p>}
            <div className={"table-border"}>
                <div className={"table-tools balance-overview__tools"}>
                    <button onClick={() => setWeek6Active(true)}>Week 6</button>
                    <button onClick={() => setWeek6Active(false)}>Week 1-5</button>
                    <label>
                        <input type="checkbox" checked={onlyFlagged} onChange={(e) => setOnlyFlagged(e.target.checked)} />
                        Only balances that need action ({flaggedCount})
                    </label>
                </div>
                <table>
                    <thead>
                    <tr>
                        <th>Name</th>
                        <th>Earned in</th>
                        <th>Usable until</th>
                        <th>Earned</th>
                        <th>Used</th>
                        <th>Transferred</th>
                        <th>Available</th>
                        <th>Pending</th>
                        <th>Status</th>
                    </tr>
                    </thead>
                    <tbody>
                    {shown.length === 0 && (
                        <tr><td colSpan={9}>{onlyFlagged ? "No balances need action." : "No balances."}</td></tr>
                    )}
                    {shown.map((r) => {
                        const a = r.account!;
                        const flag = flagMessage(a, "employee");
                        return (
                            <tr key={a.id}>
                                <td>
                                    {r.name}
                                    {r.end_date && <div className={"balance-overview__muted"}>last day {day(r.end_date)}</div>}
                                </td>
                                <td>{day(a.earning_period_start)} – {day(a.earning_period_end)}</td>
                                <td>
                                    {day(a.usage_period_end)}
                                    <div className={"balance-overview__muted"}>
                                        {a.usable ? `${a.days_until_expiry} days left` : "expired"}
                                    </div>
                                </td>
                                <td>{hours(a.earned_holiday_hours)}</td>
                                <td>{hours(a.used_holiday_hours)}</td>
                                <td>{a.transferred_holiday_hours ? hours(a.transferred_holiday_hours) : "—"}</td>
                                <td><strong>{hours(a.available_holiday_hours)}</strong></td>
                                <td>{a.pending_hours ? hours(a.pending_hours) : "—"}</td>
                                <td className={`balance-overview__flag ${flag ? `balance-overview__flag--${flag.tone}` : ""}`}>
                                    {flag ? (<><strong>{flag.title}</strong>{flag.text}</>) : <span className={"balance-overview__muted"}>OK</span>}
                                </td>
                            </tr>
                        );
                    })}
                    </tbody>
                </table>
            </div>
        </div>
    );
}
