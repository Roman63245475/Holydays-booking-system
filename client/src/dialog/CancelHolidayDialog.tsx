import {useState} from "react";
import type {OngoingHolidayRequest} from "@/api/Api.ts";
import {requestApi} from "@/api/client.ts";
import {errorText} from "@/api/errors.ts";

interface CancelHolidayDialogProps {
    request: OngoingHolidayRequest,
    onClose: () => void,
    // the cancellation request was sent, the list must be reloaded
    onSent: () => Promise<void>
}

// the employee asks the accountant to cancel an approved holiday. the holiday stays approved until the
// accountant approves the cancellation, then its hours are given back
export function CancelHolidayDialog({request, onClose, onSent}: CancelHolidayDialogProps) {

    const [reason, setReason] = useState("");
    const [error, setError] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);

    async function send(e: React.FormEvent) {
        e.preventDefault();
        if (!request.id) return;
        setBusy(true);
        setError(null);
        try {
            await requestApi.cancelApprovedRequest(request.id, {reason: reason.trim() || undefined});
            await onSent();
            onClose();
        } catch (err) {
            setError(errorText(err, "Could not send the cancellation request."));
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
                        Your approved holiday from {request.startDate} to {request.endDate} stays approved until
                        the accountant accepts the cancellation. Then its hours are returned to your balance.
                    </p>
                    <label>
                        Reason (optional)
                        <textarea maxLength={500} rows={3} value={reason} onChange={e => setReason(e.target.value)}/>
                    </label>
                    {error && <p className={"error"}>{error}</p>}
                    <div className={"request-dialog__actions"}>
                        <button type="submit" className={"btn-destructive-outline"} disabled={busy}>
                            Send cancellation request
                        </button>
                        <button type="button" disabled={busy} onClick={onClose}>Close</button>
                    </div>
                </form>
            </div>
        </div>
    );
}
