import {useState} from "react";
import {DesConfirmation} from "@/dialog/DesConfirmation.tsx";

export function RequestsTab(){

    const [week6Active, setWeek6Active] = useState(false)
    const [destructMenu, setDestructMenu] = useState(false)

    return(
        <div className={"flex center-v p-16"}>
            {
                destructMenu && <DesConfirmation setMenuOpen={setDestructMenu} title={'Confirmation'} description={"Are you sure you want to decline this user's request?"}/>
            }
            <h1>Pending Requests for {week6Active ? 'Week 6' : 'Week 1-5'}</h1>
            <div className={"flex table-border table-tools"}>
                <button onClick={() => setWeek6Active(true)}>Week 6</button>
                <button onClick={() => setWeek6Active(false)}>Week 1-5</button>
            </div>
            <div className={"flex direction-column table-border p-16 g-20"}>
                <div className={'flex p-16 shadow space-between'}>
                    <div>
                        <h1>My Request Title</h1>
                        <p>My Description (if I can have one)</p>
                    </div>
                    <div className={'flex g-10 center-v'}>
                        <button className={'request-button'}>Approve</button>
                        <button onClick={() => setDestructMenu(true)} className={'btn-destructive-outline'}>Decline</button>
                    </div>
                </div>
            </div>
        </div>
    )
}