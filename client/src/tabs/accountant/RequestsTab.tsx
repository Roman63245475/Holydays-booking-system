import {useEffect, useState} from "react";
import {DesConfirmation} from "@/dialog/DesConfirmation.tsx";
import {requestApi} from "@/api/client.ts";
import {errorText} from "@/api/errors.ts";
import {type HolidayRequestUser, HolidayType} from "@/api/Api.ts";

// pending holiday requests of the chosen account type
export function RequestsTab(){

    const [week6Active, setWeek6Active] = useState(false)
    const [currentRequests, setCurrentRequests] = useState<HolidayRequestUser[]>([])
    const [selectedRequestId, setSelectedRequestId] = useState<string>('')
    const [activeMenu, setActiveMenu] = useState<string | null>(null)
    const [message, setMessage] = useState<{ text: string, error: boolean } | null>(null)

    const targetKind = week6Active ? HolidayType.FerieUge : HolidayType.FerieEfterFerieloven;
    const filteredRequests = currentRequests.filter((r) => r.holidayType === targetKind)

    useEffect(() => {
        load();
    }, [])

    async function load(){
        try{
            setCurrentRequests(await requestApi.getRequests())
        }catch(err){
            setMessage({text: errorText(err, "Could not load the requests."), error: true})
        }
    }

    // every action reloads the lists, also when it failed: e.g. 409 = already handled by another tab
    async function run(action: () => Promise<unknown>, success: string, fallback: string) {
        try {
            await action()
            setMessage({text: success, error: false})
        } catch (err) {
            // e.g. "The request can not be approved: Not enough holiday: ..."
            setMessage({text: errorText(err, fallback), error: true})
        }
        await load();
    }

    const declineRequest = () => run(() => requestApi.declineRequest(selectedRequestId),
        "The request was declined.", "Could not decline the request.")
    const acceptRequest = (id: string) => run(() => requestApi.acceptRequest(id),
        "The request was approved.", "Could not approve the request.")

    return(
        <div className={"flex center-v p-16"}>
            {
                activeMenu === 'decline' && <DesConfirmation setMenuOpen={setActiveMenu} title={'Confirmation'} description={"Are you sure you want to decline this user's request?"} onConfirm={declineRequest}/>
            }
            <h1>Pending Requests for {week6Active ? 'Week 6' : 'Week 1-5'}</h1>
            <div className={"flex table-border table-tools"}>
                <button onClick={() => setWeek6Active(true)}>Week 6</button>
                <button onClick={() => setWeek6Active(false)}>Week 1-5</button>
            </div>
            {message && <p className={message.error ? "error" : "success"}>{message.text}</p>}
            <div className={"flex direction-column table-border p-16 g-20"}>
                {filteredRequests.length === 0 && <p>No pending requests.</p>}
                {
                    filteredRequests.map((r) => (
                        <div key={r.id} className={'flex p-16 shadow space-between'}>
                            <div>
                                <h1>Name: {r.name}</h1>
                                <p>Holiday: {r.startDate} to {r.endDate}</p>
                                <p>Hours: {r.hours?.toFixed(2)} h {r.isMainHoliday && <span className={"ongoing-requests__tag"}>Main holiday</span>}</p>
                                {r.isShortBlock && (
                                    <p className={"request-reason"}>Shorter than 5 days: blocks of at least 5 days are recommended (allowed anyway).</p>
                                )}
                            </div>
                            <div className={'flex g-10 center-v'}>
                                <button onClick={() => acceptRequest(r.id as string)} className={'request-button'}>Approve</button>
                                <button onClick={() => {
                                    setSelectedRequestId(r.id as string);
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
