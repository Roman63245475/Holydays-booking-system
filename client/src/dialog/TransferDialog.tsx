import { useState } from "react";
import type { HolidayAccountView } from "@/api/Api.ts";
import { transferApi } from "@/api/client.ts";
import { errorText } from "@/api/errors.ts";
import { accountTitle } from "@/HolidayBalance.tsx";
import { day, hours, hoursAndDays } from "@/balanceStatus.ts";

interface TransferDialogProps {
    account: HolidayAccountView;
    onClose: () => void;
    // the request was sent, the page must be reloaded
    onSent: () => Promise<void> | void;
}

// the employee asks to transfer unused holiday to the next holiday period. by law this needs a written agreement,
// so the accountant approves or declines it
export function TransferDialog({ account, onClose, onSent }: TransferDialogProps) {
    const max = account.transfer_max_hours ?? 0;
    const [amount, setAmount] = useState<string>(max.toFixed(2));
    const [comment, setComment] = useState("");
    const [error, setError] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);

    const value = Number(amount);
    const valid = amount !== "" && value > 0 && value <= max && Math.round(value * 100) === value * 100;

    async function send(e: React.FormEvent) {
        e.preventDefault();
        if (!account.id || !valid) return;
        setBusy(true);
        setError(null);
        try {
            await transferApi.createTransfer({ account_id: account.id, hours: value, comment: comment.trim() || undefined });
            await onSent();
            onClose();
        } catch (err) {
            setError(errorText(err, "Could not send the transfer request."));
        } finally {
            setBusy(false);
        }
    }

    return (
        <div onClick={onClose} className={"menu-bg flex center-v"}>
            <div onClick={(e) => e.stopPropagation()} className={"menu"}>
                <form onSubmit={send} className={"request-dialog shadow"}>
                    <h1>Transfer holiday</h1>
                    <p className={"request-dialog__hint"}>
                        From <strong>{accountTitle(account)}</strong> (expires {day(account.usage_period_end)}) to the next
                        holiday period. You can transfer at most {hoursAndDays(max, account.day_hours)}. The accountant must
                        approve it by {day(account.transfer_deadline)}, otherwise the request is declined.
                    </p>
                    <label>
                        Hours to transfer
                        <input type="number" min={0.01} max={max} step={0.01} value={amount}
                               onChange={(e) => setAmount(e.target.value)} />
                    </label>
                    {!valid && amount !== "" && (
                        <p className={"error"}>Enter between 0.01 and {hours(max)} (at most two decimals).</p>
                    )}
                    <label>
                        Comment (optional)
                        <textarea maxLength={500} rows={3} value={comment} onChange={(e) => setComment(e.target.value)} />
                    </label>
                    {error && <p className={"error"}>{error}</p>}
                    <div className={"request-dialog__actions"}>
                        <button type="submit" className={"request-button"} disabled={busy || !valid}>Send transfer request</button>
                        <button type="button" disabled={busy} onClick={onClose}>Close</button>
                    </div>
                </form>
            </div>
        </div>
    );
}
