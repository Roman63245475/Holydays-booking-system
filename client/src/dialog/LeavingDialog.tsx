import { useState } from "react";
import type { ClientUser } from "@/api/Api.ts";
import { userApi } from "@/api/client.ts";
import { errorText } from "@/api/errors.ts";

interface LeavingDialogProps {
    user: ClientUser;
    onClose: () => void;
    // saved, the users list must be reloaded
    onSaved: () => Promise<void> | void;
}

// the system owner registers that an employee leaves the company (fratræden): their last working day.
// holiday is earned for the exact days until then, approved holidays after it are cancelled, pending requests and
// transfers declined, and what is left is settled with the final salary (by payroll)
export function LeavingDialog({ user, onClose, onSaved }: LeavingDialogProps) {
    const current = user.end_date ? String(user.end_date).slice(0, 10) : "";
    const [date, setDate] = useState(current);
    const [error, setError] = useState<string | null>(null);
    const [result, setResult] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);

    async function save(end_date: string | null) {
        if (!user.id) return;
        setBusy(true);
        setError(null);
        try {
            const r = await userApi.set_end_date(user.id, { end_date: end_date ?? undefined });
            await onSaved();
            if (end_date === null) {
                onClose();
                return;
            }
            setResult(`Saved. ${r.cancelled_holidays ?? 0} approved holiday(s) after the last day cancelled, `
                + `${r.declined_requests ?? 0} pending request(s) and ${r.declined_transfers ?? 0} transfer request(s) declined.`);
        } catch (err) {
            setError(errorText(err, "Could not save the last working day."));
        } finally {
            setBusy(false);
        }
    }

    return (
        <div onClick={onClose} className={"menu-bg flex center-v"}>
            <div onClick={(e) => e.stopPropagation()} className={"menu"}>
                <form onSubmit={(e) => { e.preventDefault(); if (date) save(date); }} className={"request-dialog shadow"}>
                    <h1>{user.name} leaves the company</h1>
                    <p className={"request-dialog__hint"}>
                        Holiday is earned for the exact days until the last working day. Approved holidays after it are
                        cancelled, pending holiday and transfer requests are declined. What is left on the last day is
                        settled with the final salary by payroll. After the last day the user can not log in anymore.
                    </p>
                    {result ? (
                        <>
                            <p className={"success"}>{result}</p>
                            <div className={"request-dialog__actions"}>
                                <button type="button" onClick={onClose}>Close</button>
                            </div>
                        </>
                    ) : (
                        <>
                            <label>
                                Last working day
                                <input type="date" value={date} onChange={(e) => setDate(e.target.value)} required />
                            </label>
                            {error && <p className={"error"}>{error}</p>}
                            <div className={"request-dialog__actions"}>
                                <button type="submit" className={"btn-destructive-outline"} disabled={busy || !date}>Save last working day</button>
                                {current && (
                                    <button type="button" disabled={busy} onClick={() => save(null)}
                                            title="Holidays that were already cancelled are not restored">
                                        Call off leaving
                                    </button>
                                )}
                                <button type="button" disabled={busy} onClick={onClose}>Close</button>
                            </div>
                        </>
                    )}
                </form>
            </div>
        </div>
    );
}
