import {useEffect, useState} from "react";
import {DesConfirmation} from "@/dialog/DesConfirmation.tsx";
import {requestApi} from "@/api/client.ts";
import {errorText} from "@/api/errors.ts";
import {type CancellationRequestView, HolidayType} from "@/api/Api.ts";

// employees' requests to cancel an approved holiday of the chosen account type
export function CancellationRequestsTab() {

    const [week6Active, setWeek6Active] = useState(false)
    const [cancellations, setCancellations] = useState<CancellationRequestView[]>([])
    const [selectedId, setSelectedId] = useState<string>('')
    const [activeMenu, setActiveMenu] = useState<string | null>(null)
    const [message, setMessage] = useState<{ text: string, error: boolean } | null>(null)

    const targetKind = week6Active ? HolidayType.FerieUge : HolidayType.FerieEfterFerieloven;
    const filtered = cancellations.filter((c) => c.holidayType === targetKind)

    useEffect(() => {
        load();
    }, [])

    async function load() {
        try {
            setCancellations(await requestApi.getCancellationRequests())
        } catch (err) {
            setMessage({text: errorText(err, "Could not load the cancellation requests."), error: true})
        }
    }

    // every action reloads the list, also when it failed: e.g. 409 = already handled
    async function run(action: () => Promise<unknown>, success: string, fallback: string) {
        try {
            await action()
            setMessage({text: success, error: false})
        } catch (err) {
            setMessage({text: errorText(err, fallback), error: true})
        }
        await load();
    }

    const accept = (id: string) => run(() => requestApi.acceptCancellation(id),
        "The holiday was cancelled and its hours were returned.", "Could not cancel the holiday.")
    const decline = () => run(() => requestApi.declineCancellation(selectedId),
        "The cancellation was declined, the holiday stays approved.", "Could not decline the cancellation.")

    return (
        <div className={"flex center-v p-16"}>
            {
                activeMenu === 'decline' && <DesConfirmation setMenuOpen={setActiveMenu} title={'Confirmation'} description={"Decline the cancellation? The holiday stays approved."} onConfirm={decline}/>
            }
            <h1>Cancellation Requests for {week6Active ? 'Week 6' : 'Week 1-5'}</h1>
            <div className={"flex table-border table-tools"}>
                <button onClick={() => setWeek6Active(true)}>Week 6</button>
                <button onClick={() => setWeek6Active(false)}>Week 1-5</button>
            </div>
            {message && <p className={message.error ? "error" : "success"}>{message.text}</p>}
            <div className={"flex direction-column table-border p-16 g-20"}>
                {filtered.length === 0 && <p>No pending cancellation requests.</p>}
                {
                    filtered.map((c) => (
                        <div key={c.id} className={'flex p-16 shadow space-between'}>
                            <div>
                                <h1>Name: {c.name}</h1>
                                <p>Approved holiday: {c.startDate} to {c.endDate} ({c.hours?.toFixed(2)} h)</p>
                                <p className={"request-reason"}>{c.reason ? `Reason: ${c.reason}` : "No reason given"}</p>
                            </div>
                            <div className={'flex g-10 center-v'}>
                                <button onClick={() => accept(c.id as string)} className={'request-button'}>Approve cancellation</button>
                                <button onClick={() => {
                                    setSelectedId(c.id as string);
                                    setActiveMenu('decline')
                                }} className={'btn-destructive-outline'}>Decline</button>
                            </div>
                        </div>
                    ))
                }
            </div>
        </div>
    )
}
