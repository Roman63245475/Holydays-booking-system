import {useEffect, useState} from "react";
import {EmployerRescheduleDialog} from "@/dialog/EmployerRescheduleDialog.tsx";
import {EmployerCancelDialog} from "@/dialog/EmployerCancelDialog.tsx";
import {requestApi} from "@/api/client.ts";
import {errorText} from "@/api/errors.ts";
import {type ApprovedHolidayView, HolidayType} from "@/api/Api.ts";

// approved holidays of all employees that are not over yet, of the chosen account type.
// the accountant can cancel one that has not started (employer's decision, or approving the employee's request)
// or move it to new dates (employer's decision; not while the employee asks to cancel it)
export function ApprovedTab(){

    const [week6Active, setWeek6Active] = useState(false)
    const [holidays, setHolidays] = useState<ApprovedHolidayView[]>([])
    const [cancelling, setCancelling] = useState<ApprovedHolidayView | null>(null)
    const [rescheduling, setRescheduling] = useState<ApprovedHolidayView | null>(null)
    const [message, setMessage] = useState<{ text: string, error: boolean } | null>(null)

    const targetKind = week6Active ? HolidayType.FerieUge : HolidayType.FerieEfterFerieloven;
    const filtered = holidays.filter((h) => h.holidayType === targetKind)

    useEffect(() => {
        load();
    }, [])

    async function load() {
        try {
            setHolidays(await requestApi.getAllApprovedRequests())
        } catch (err) {
            setMessage({text: errorText(err, "Could not load the approved holidays."), error: true})
        }
    }

    async function changed(text: string) {
        setMessage({text, error: false})
        await load()
    }

    return(
        <div className={"flex center-v p-16"}>
            {cancelling && <EmployerCancelDialog holiday={cancelling} onClose={() => setCancelling(null)} onDone={changed}/>}
            {rescheduling && <EmployerRescheduleDialog holiday={rescheduling} onClose={() => setRescheduling(null)} onDone={changed}/>}
            <h1>Approved Holidays for {week6Active ? 'Week 6' : 'Week 1-5'}</h1>
            <div className={"flex table-border table-tools"}>
                <button onClick={() => setWeek6Active(true)}>Week 6</button>
                <button onClick={() => setWeek6Active(false)}>Week 1-5</button>
            </div>
            {message && <p className={message.error ? "error" : "success"}>{message.text}</p>}
            <div className={"flex direction-column table-border p-16 g-20"}>
                {filtered.length === 0 && <p>No approved holidays coming up.</p>}
                {
                    filtered.map((h) => (
                        <div key={h.id} className={'flex p-16 shadow space-between'}>
                            <div>
                                <h1>Name: {h.name}</h1>
                                <p>Holiday: {h.startDate} to {h.endDate}</p>
                                <p>
                                    Hours: {h.hours?.toFixed(2)} h
                                    {h.isMainHoliday && <span className={"ongoing-requests__tag"}>Main holiday</span>}
                                    {h.started && <span className={"ongoing-requests__tag"}>In progress</span>}
                                </p>
                                {h.hasPendingCancellation && (
                                    <p className={"request-reason"}>
                                        The employee asked to cancel it{h.pendingCancellationReason ? `: ${h.pendingCancellationReason}` : ""}
                                    </p>
                                )}
                            </div>
                            <div className={"flex center-v g-10"}>
                                <button onClick={() => setRescheduling(h)} className={"w-100 btn-warning"}
                                        disabled={h.started || h.hasPendingCancellation}
                                        title={h.started ? "A holiday that has started can not be moved"
                                            : h.hasPendingCancellation ? "The employee asked to cancel it: answer their request first" : undefined}>
                                    Reschedule
                                </button>
                                <button onClick={() => setCancelling(h)} className={"w-100 btn-destructive-outline"}
                                        disabled={h.started}
                                        title={h.started ? "A holiday that has started can not be cancelled" : undefined}>
                                    Cancel Holiday
                                </button>
                            </div>
                        </div>
                    ))
                }
            </div>
        </div>
    )
}
