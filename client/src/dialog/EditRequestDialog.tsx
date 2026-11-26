import {useState} from "react";
import type {OngoingHolidayRequest} from "@/api/Api.ts";
import {requestApi} from "@/api/client.ts";
import {errorText} from "@/api/errors.ts";

interface EditRequestDialogProps {
    request: OngoingHolidayRequest,
    onClose: () => void,
    // the request was changed or withdrawn, the list must be reloaded
    onChanged: () => Promise<void>
}

// Edit of a pending request: new dates (the old request becomes Withdrawn, a new pending one replaces it),
// or Withdraw: the request is taken back without a new one
export function EditRequestDialog({request, onClose, onChanged}: EditRequestDialogProps) {

    const [startDate, setStartDate] = useState(request.startDate ?? "");
    const [endDate, setEndDate] = useState(request.endDate ?? "");
    const [error, setError] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);
    const [confirmWithdraw, setConfirmWithdraw] = useState(false);

    const unchanged = startDate === request.startDate && endDate === request.endDate;
    const datesValid = !!startDate && !!endDate && endDate >= startDate;

    async function save(e: React.FormEvent) {
        e.preventDefault();
        if (!request.id) return;
        setBusy(true);
        setError(null);
        try {
            // the server recalculates the hours and checks all holiday rules again
            await requestApi.editRequest(request.id, {startDate, endDate});
            await onChanged();
            onClose();
        } catch (err) {
            // e.g. "This holiday must be requested 1 month in advance: ..."
            setError(errorText(err, "Could not change the request."));
        } finally {
            setBusy(false);
        }
    }

    async function withdraw() {
        if (!request.id) return;
        setBusy(true);
        setError(null);
        try {
            await requestApi.withdrawRequest(request.id);
            await onChanged();
            onClose();
        } catch (err) {
            setError(errorText(err, "Could not withdraw the request."));
            setConfirmWithdraw(false);
        } finally {
            setBusy(false);
        }
    }

    return (
        <div onClick={onClose} className={"menu-bg flex center-v"}>
            <div onClick={(e) => e.stopPropagation()} className={"menu"}>
                <form onSubmit={save} className={"request-dialog shadow"}>
                    <h1>Edit request</h1>
                    <p className={"request-dialog__hint"}>
                        Currently {request.startDate} to {request.endDate}. The new dates are checked again
                        (balance, notice, overlapping holidays) and the hours are recalculated.
                    </p>
                    <label>
                        Start
                        <input required type="date" value={startDate} onChange={e => setStartDate(e.target.value)}/>
                    </label>
                    <label>
                        End
                        <input required type="date" min={startDate} value={endDate} onChange={e => setEndDate(e.target.value)}/>
                    </label>
                    {error && <p className={"error"}>{error}</p>}

                    {confirmWithdraw ? (
                        <div className={"request-dialog__confirm"}>
                            <p>Withdraw this request? It will not be sent to the accountant anymore.</p>
                            <div className={"flex g-10"}>
                                <button type="button" className={"btn-destructive-outline"} disabled={busy} onClick={withdraw}>
                                    Yes, withdraw
                                </button>
                                <button type="button" disabled={busy} onClick={() => setConfirmWithdraw(false)}>No</button>
                            </div>
                        </div>
                    ) : (
                        <div className={"request-dialog__actions"}>
                            <button type="submit" className={"request-button"} disabled={busy || unchanged || !datesValid}>
                                Save new dates
                            </button>
                            <button type="button" className={"btn-destructive-outline"} disabled={busy}
                                    onClick={() => setConfirmWithdraw(true)}>
                                Withdraw request
                            </button>
                            <button type="button" disabled={busy} onClick={onClose}>Close</button>
                        </div>
                    )}
                </form>
            </div>
        </div>
    );
}
