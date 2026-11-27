import { useEffect, useState } from "react";
import { transferApi } from "@/api/client.ts";
import { errorText } from "@/api/errors.ts";
import { HolidayType, type TransferRequestView } from "@/api/Api.ts";
import { day, hours, hoursAndDays } from "@/balanceStatus.ts";

// employees' requests to transfer unused holiday to the next holiday period. approving one is the written
// agreement the law asks for; the rules are checked again on approval (the balance may have changed)
export function TransferRequestsTab() {
    const [transfers, setTransfers] = useState<TransferRequestView[]>([]);
    const [message, setMessage] = useState<{ text: string; error: boolean } | null>(null);
    const [declining, setDeclining] = useState<TransferRequestView | null>(null);
    const [note, setNote] = useState("");

    useEffect(() => {
        load();
    }, []);

    async function load() {
        try {
            setTransfers(await transferApi.getPendingTransfers());
        } catch (err) {
            setMessage({ text: errorText(err, "Could not load the transfer requests."), error: true });
        }
    }

    // every action reloads the list, also when it failed: e.g. 409 = already handled
    async function run(action: () => Promise<unknown>, success: string, fallback: string) {
        try {
            await action();
            setMessage({ text: success, error: false });
        } catch (err) {
            setMessage({ text: errorText(err, fallback), error: true });
        }
        await load();
    }

    const accept = (t: TransferRequestView) => run(() => transferApi.acceptTransfer(t.id as string),
        `The transfer of ${hours(t.hours)} for ${t.name} was approved.`, "Could not approve the transfer.");

    async function decline(e: React.FormEvent) {
        e.preventDefault();
        if (!declining?.id) return;
        const t = declining;
        setDeclining(null);
        await run(() => transferApi.declineTransfer(t.id as string, { note: note.trim() || undefined }),
            "The transfer was declined.", "Could not decline the transfer.");
        setNote("");
    }

    return (
        <div className={"flex center-v p-16"}>
            {declining && (
                <div onClick={() => setDeclining(null)} className={"menu-bg flex center-v"}>
                    <div onClick={(e) => e.stopPropagation()} className={"menu"}>
                        <form onSubmit={decline} className={"request-dialog shadow"}>
                            <h1>Decline transfer</h1>
                            <p className={"request-dialog__hint"}>
                                {declining.name} asked to transfer {hours(declining.hours)}. The hours stay in the current balance.
                            </p>
                            <label>
                                Note for the employee (optional)
                                <textarea maxLength={500} rows={3} value={note} onChange={(e) => setNote(e.target.value)} />
                            </label>
                            <div className={"request-dialog__actions"}>
                                <button type="submit" className={"btn-destructive-outline"}>Decline</button>
                                <button type="button" onClick={() => setDeclining(null)}>Close</button>
                            </div>
                        </form>
                    </div>
                </div>
            )}
            <h1>Transfer Requests</h1>
            <p className={"users-table__hint"}>
                Unused holiday moved to the next holiday period (weeks 1-5: only the 5th week, agreed by 31 December;
                week 6: agreed by 30 September). Approving is the written agreement.
            </p>
            {message && <p className={message.error ? "error" : "success"}>{message.text}</p>}
            <div className={"flex direction-column table-border p-16 g-20"}>
                {transfers.length === 0 && <p>No pending transfer requests.</p>}
                {transfers.map((t) => (
                    <div key={t.id} className={"flex p-16 shadow space-between"}>
                        <div>
                            <h1>Name: {t.name}</h1>
                            <p>
                                {t.holiday_type === HolidayType.FerieUge ? "Week 6" : "Weeks 1-5"} earned
                                {" "}{day(t.earning_period_start)} – {day(t.earning_period_end)}, expires {day(t.usage_period_end)}
                            </p>
                            <p>Transfer {hoursAndDays(t.hours, t.day_hours)} to the next period (usable until {day(t.to_usage_period_end)})</p>
                            <p>Must be agreed by <strong>{day(t.deadline)}</strong></p>
                            <p className={"request-reason"}>{t.comment ? `Comment: ${t.comment}` : "No comment"}</p>
                            {!t.can_approve && <p className={"error"}>Can not be approved: {t.not_approvable_reason}</p>}
                        </div>
                        <div className={"flex g-10 center-v"}>
                            <button onClick={() => accept(t)} className={"request-button"} disabled={!t.can_approve}>Approve transfer</button>
                            <button onClick={() => setDeclining(t)} className={"btn-destructive-outline"}>Decline</button>
                        </div>
                    </div>
                ))}
            </div>
        </div>
    );
}
