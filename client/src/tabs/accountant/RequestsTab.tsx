import {useEffect, useState} from "react";
import {DesConfirmation} from "@/dialog/DesConfirmation.tsx";
import {requestApi} from "@/api/client.ts";
import {errorText} from "@/api/errors.ts";
import {type HolidayRequestUser, HolidayType, RequestStatus} from "@/api/Api.ts";

export function RequestsTab(){

    const [week6Active, setWeek6Active] = useState(false)
    const [currentRequests, setCurrentRequests] = useState<HolidayRequestUser[]>([])
    const [selectedRequestId, setSelectedRequestId] = useState<string>('')
    const [activeMenu, setActiveMenu] = useState<string | null>(null)

    const targetKind = week6Active ? HolidayType.FerieUge : HolidayType.FerieEfterFerieloven;
    const filteredRequests = currentRequests.filter((r) => (r.holidayType) === targetKind)

    useEffect(() => {
        getRequests();
    }, [])

    async function getRequests(){
        try{
            const response = await requestApi.getRequests()
            setCurrentRequests(response)
        }catch(err){
            console.log(err)
        }
    }

    async function declineRequest() {
        if (!selectedRequestId) return;
        try {
            await requestApi.declineRequest(selectedRequestId)
        } catch (err) {
            // e.g. 409: the request was already handled (by another tab or accountant)
            console.log(errorText(err, "Could not decline the request."))
        }
        // reload in both cases, so a request that is not pending anymore disappears from the list
        await getRequests();
    }

    async function acceptRequest(id : string) {
        try{
            await requestApi.acceptRequest(id)
        }catch(err){
            console.log(errorText(err, "Could not approve the request."))
        }
        await getRequests();
    }

    return(
        <div className={"flex center-v p-16"}>
            {
                activeMenu === 'destruct' && <DesConfirmation setMenuOpen={setActiveMenu} title={'Confirmation'} description={"Are you sure you want to decline this user's request?"} onConfirm={() => declineRequest()}/>
            }
            <h1>Pending Requests for {week6Active ? 'Week 6' : 'Week 1-5'}</h1>
            <div className={"flex table-border table-tools"}>
                <button onClick={() => setWeek6Active(true)}>Week 6</button>
                <button onClick={() => setWeek6Active(false)}>Week 1-5</button>
            </div>
            <div className={"flex direction-column table-border p-16 g-20"}>
                {
                    filteredRequests.map((r) => (
                        <div key={r.id} className={'flex p-16 shadow space-between'}>
                            <div>
                                <h1>Name: {r.name}</h1>
                                <p>Holiday start: {r.startDate}</p>
                                <p>Holiday end: {r.endDate}</p>
                                <p>Status: {RequestStatus[r.status as number]}</p>
                            </div>
                            <div className={'flex g-10 center-v'}>
                                <button onClick={() => acceptRequest(r.id as string)} className={'request-button'}>Approve</button>
                                <button onClick={() => {
                                    setSelectedRequestId(r.id as string);
                                    setActiveMenu('destruct')
                                }} className={'btn-destructive-outline'}>Decline</button>
                            </div>
                        </div>
                    ))
                }
            </div>
        </div>
    )
}