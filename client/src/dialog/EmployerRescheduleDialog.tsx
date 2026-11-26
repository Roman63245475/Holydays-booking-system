import {useState} from "react";
import {type ApprovedHolidayView, HolidayType} from "@/api/Api.ts";
import {requestApi} from "@/api/client.ts";
import {errorText} from "@/api/errors.ts";

// keep in sync with RequestService.MIN_EMPLOYER_REASON_LENGTH
const MIN_REASON_LENGTH = 10;

interface EmployerRescheduleDialogProps {
    holiday: ApprovedHolidayView,
    onClose: () => void,
    // the holiday was moved, show the message and reload the list
    onDone: (message: string) => Promise<void>
}

// the accountant moves an approved holiday to new dates on the employer's decision (05.11 § 10): only in a
// force-majeure-like situation, with the new dates agreed with the employee, the reason is recorded.
// the server checks the new dates like any request (balance, overlap, period), but without the notice
export function EmployerRescheduleDialog({holiday, onClose, onDone}: EmployerRescheduleDialogProps) {

    const [startDate, setStartDate] = useState(holiday.startDate ?? "");
    const [endDate, setEndDate] = useState(holiday.endDate ?? "");
    const [reason, setReason] = useState("");
    const [confirmed, setConfirmed] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);

    const changed = startDate !== holiday.startDate || endDate !== holiday.endDate;
    const datesValid = !!startDate && !!endDate && endDate >= startDate;
    const canSend = changed && datesValid && reason.trim().length >= MIN_REASON_LENGTH && confirmed;
    const account = holiday.holidayType === HolidayType.FerieUge ? "Week 6" : "Weeks 1-5";

    async function send(e: React.FormEvent) {
        e.preventDefault();
        if (!holiday.id) return;
        setBusy(true);
        setError(null);
        try {
            const moved = await requestApi.employerReschedule(holiday.id, {startDate, endDate, reason: reason.trim(), confirmed});
            await onDone(`The holiday of ${holiday.name} was moved to ${moved.startDate} – ${moved.endDate} ` +
                `(${moved.hours?.toFixed(2)} h). The reason was recorded.`);
            onClose();
        } catch (err) {
            // e.g. "Not enough holiday: ..." or "These dates overlap ...": nothing was changed
            setError(errorText(err, "Could not move the holiday."));
        } finally {
            setBusy(false);
        }
    }

    return (
        <div onClick={onClose} className={"menu-bg flex center-v"}>
            <div onClick={(e) => e.stopPropagation()} className={"menu"}>
                <form onSubmit={send} className={"request-dialog shadow"}>
                    <h1>Reschedule holiday</h1>
                    <p className={"request-dialog__hint"}>
                        {holiday.name} · {account} · currently {holiday.startDate} to {holiday.endDate} ({holiday.hours?.toFixed(2)} h).
                        The new dates are checked against the employee's balance and other holidays, the hours are recalculated.
                    </p>
                    <label>
                        New start
                        <input required type="date" value={startDate} onChange={e => setStartDate(e.target.value)}/>
                    </label>
                    <label>
                        New end
                        <input required type="date" min={startDate} value={endDate} onChange={e => setEndDate(e.target.value)}/>
                    </label>
                    <label>
                        Reason (required)
                        <textarea maxLength={500} rows={3} value={reason} placeholder={"Why does the company move this holiday?"}
                                  onChange={e => setReason(e.target.value)}/>
                    </label>
                    <label className={"request-dialog__checkbox"}>
                        <input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)}/>
                        <span>
                            This is a force-majeure-like situation, the new dates were agreed with the employee and
                            their financial loss will be covered by payroll.
                        </span>
                    </label>
                    {error && <p className={"error"}>{error}</p>}
                    <div className={"request-dialog__actions"}>
                        <button type="submit" className={"btn-warning"} disabled={busy || !canSend}>Move holiday</button>
                        <button type="button" disabled={busy} onClick={onClose}>Close</button>
                    </div>
                </form>
            </div>
        </div>
    );
}
