import type {HolidayAccountView} from "./api/Api";
import {flagMessage, hours} from "./balanceStatus";

// Keep these values in sync with server/be/HolidayType.cs. The generated API
// client currently exposes TypeOfHolidayDay (for days off), not HolidayType.
const HolidayType = {
    FerieEfterFerieloven: 1,
    FerieUge: 2,
} as const;

interface HolidayBalanceProps {
    account: HolidayAccountView;
    // without onSelect the card is read-only (the admin's user page)
    onSelect?: (account: HolidayAccountView) => void;
    // the employee's own page: ask to transfer hours to the next holiday period / take back a pending transfer
    onTransfer?: (account: HolidayAccountView) => void;
    onWithdrawTransfer?: (account: HolidayAccountView) => void;
    // whose balance it is, for the texts of the status box
    who?: "you" | "employee";
}

// dates come from the API as "yyyy-MM-dd" strings (typed as Date by the generated client)
function formatDate(value?: Date | string): string {
    const date = value ? new Date(value) : null;
    return date && !Number.isNaN(date.getTime()) ? date.toLocaleDateString() : "—";
}

// "Weeks 1-5 · earned 2026/27" or "Week 6 · earned 2026"
export function accountTitle(account: HolidayAccountView): string {
    const start = account.earning_period_start ? new Date(account.earning_period_start) : null;
    const end = account.earning_period_end ? new Date(account.earning_period_end) : null;
    if (!start || !end) {
        return account.holiday_type === HolidayType.FerieUge ? "Week 6" : "Weeks 1-5";
    }
    const years = start.getFullYear() === end.getFullYear()
        ? `${start.getFullYear()}`
        : `${start.getFullYear()}/${String(end.getFullYear()).slice(-2)}`;
    return account.holiday_type === HolidayType.FerieUge
        ? `Week 6 · earned ${years}`
        : `Weeks 1-5 · earned ${years}`;
}

export function HolidayBalance({account, onSelect, onTransfer, onWithdrawTransfer, who = "you"}: HolidayBalanceProps) {
    const flag = flagMessage(account, who);
    return (
        <section className="holiday-balance-card">
            <h2 className="holiday-balance-title">{accountTitle(account)}</h2>
            <p className="holiday-balance-caption">
                {account.active ? "Still being earned" : "Earning finished"}
            </p>
            <div className="holiday-balance-remaining">
                <span>Available</span>
                <strong>{account.available_holiday_hours ?? "—"}</strong>
                <span>hours</span>
            </div>
            <div className="holiday-balance-details">
                <p><span>Can be used</span><strong>{formatDate(account.usage_period_start)} – {formatDate(account.usage_period_end)}</strong></p>
                <p><span>Earned</span><strong>{account.earned_holiday_hours ?? "—"} hours</strong></p>
                {/* balance conversion after working schedule changes (same number of days, new day length) */}
                {!!account.converted_holiday_hours && (
                    <p><span>Schedule change</span><strong>{account.converted_holiday_hours > 0 ? "+" : ""}{account.converted_holiday_hours} hours</strong></p>
                )}
                {/* approved transfers: - sent to the next holiday period, + received from the previous one */}
                {!!account.transferred_holiday_hours && (
                    <p>
                        <span>{account.transferred_holiday_hours > 0 ? "Transferred in" : "Transferred out"}</span>
                        <strong>{account.transferred_holiday_hours > 0 ? "+" : ""}{account.transferred_holiday_hours} hours</strong>
                    </p>
                )}
                <p><span>Used</span><strong>{account.used_holiday_hours ?? "—"} hours</strong></p>
                {!!account.pending_hours && (
                    <p><span>Requested (pending)</span><strong>{account.pending_hours} hours</strong></p>
                )}
            </div>
            {flag && (
                <div className={`balance-flag balance-flag--${flag.tone}`} role="status">
                    <strong>{flag.title}</strong>
                    <p>{flag.text}</p>
                </div>
            )}
            {!!account.pending_transfer_id && (
                <div className="balance-flag balance-flag--info">
                    <strong>Transfer requested</strong>
                    <p>{hours(account.pending_transfer_hours)} to the next holiday period, waiting for the accountant.</p>
                    {onWithdrawTransfer && (
                        <button type="button" className="btn-destructive-outline" onClick={() => onWithdrawTransfer(account)}>
                            Withdraw transfer request
                        </button>
                    )}
                </div>
            )}
            {onTransfer && account.can_transfer && (
                <button className="holiday-balance-action holiday-balance-action--secondary" type="button" onClick={() => onTransfer(account)}>
                    Transfer to the next period…
                </button>
            )}
            {onSelect && (
                <button
                    className="holiday-balance-action"
                    type="button"
                    onClick={() => onSelect(account)}
                >
                    Request from this balance
                </button>
            )}
        </section>
    );
}
