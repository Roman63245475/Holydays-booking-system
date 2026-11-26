import {useState} from "react";
import type {ApprovedHolidayView} from "@/api/Api.ts";
import {requestApi} from "@/api/client.ts";
import {errorText} from "@/api/errors.ts";

// keep in sync with RequestService.MIN_EMPLOYER_REASON_LENGTH
const MIN_REASON_LENGTH = 10;

interface EmployerCancelDialogProps {
    holiday: ApprovedHolidayView,
    onClose: () => void,
    // the holiday was cancelled, show the message and reload the list
    onDone: (message: string) => Promise<void>
}

// the accountant cancels an approved holiday. two cases:
//  - the employee has already asked to cancel it: confirming approves the employee's request (no reason needed)
//  - otherwise it is the employer's decision (05.11 § 10): only in a force-majeure-like situation, the reason is
//    recorded and the employee's financial loss is covered by payroll
export function EmployerCancelDialog({holiday, onClose, onDone}: EmployerCancelDialogProps) {

    const [reason, setReason] = useState("");
    const [confirmed, setConfirmed] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);

    const employeeAsked = !!holiday.hasPendingCancellation;
    const canSend = employeeAsked || (reason.trim().length >= MIN_REASON_LENGTH && confirmed);

    async function send(e: React.FormEvent) {
        e.preventDefault();
        if (!holiday.id) return;
        setBusy(true);
        setError(null);
        try {
            const result = await requestApi.employerCancel(holiday.id, {reason: reason.trim() || undefined, confirmed});
            await onDone(result.employeeRequestApproved
                ? `The employee's cancellation request was approved, ${holiday.name}'s hours were returned.`
                : `The holiday of ${holiday.name} was cancelled and the hours were returned. The reason was recorded.`);
            onClose();
        } catch (err) {
            setError(errorText(err, "Could not cancel the holiday."));
        } finally {
            setBusy(false);
        }
    }

    return (
        <div onClick={onClose} className={"menu-bg flex center-v"}>
            <div onClick={(e) => e.stopPropagation()} className={"menu"}>
                <form onSubmit={send} className={"request-dialog shadow"}>
                    <h1>Cancel holiday</h1>
                    <p className={"request-dialog__hint"}>
                        {holiday.name}: {holiday.startDate} to {holiday.endDate} ({holiday.hours?.toFixed(2)} h).
                        The hours are returned to the employee's balance.
                    </p>

                    {employeeAsked ? (
                        <p className={"request-dialog__hint"}>
                            {holiday.name} has already asked to cancel this holiday
                            {holiday.pendingCancellationReason ? ` (“${holiday.pendingCancellationReason}”)` : ""}.
                            Cancelling it here approves that request.
                        </p>
                    ) : (
                        <>
                            <label>
                                Reason (required)
                                <textarea maxLength={500} rows={3} value={reason} placeholder={"Why does the company cancel this holiday?"}
                                          onChange={e => setReason(e.target.value)}/>
                            </label>
                            <label className={"request-dialog__checkbox"}>
                                <input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)}/>
                                <span>
                                    This is a force-majeure-like situation, the employee has been consulted and their
                                    financial loss (e.g. a cancelled trip) will be covered by payroll.
                                </span>
                            </label>
                        </>
                    )}

                    {error && <p className={"error"}>{error}</p>}
                    <div className={"request-dialog__actions"}>
                        <button type="submit" className={"btn-destructive-outline"} disabled={busy || !canSend}>
                            {employeeAsked ? "Approve employee's cancellation" : "Cancel holiday"}
                        </button>
                        <button type="button" disabled={busy} onClick={onClose}>Close</button>
                    </div>
                </form>
            </div>
        </div>
    );
}
