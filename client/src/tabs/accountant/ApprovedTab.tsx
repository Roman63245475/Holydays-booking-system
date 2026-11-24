import {useState} from "react";
import {DesConfirmation} from "@/dialog/DesConfirmation.tsx";
import {RescheduleMenu} from "@/dialog/RescheduleMenu.tsx";
import {IllnessMenu} from "@/dialog/IllnessMenu.tsx";

export function ApprovedTab(){

    const [week6Active, setWeek6Active] = useState(false)
    const [activeMenu, setActiveMenu] = useState<string | null>(null)

    const renderContent = () => {
        switch(activeMenu){
            case 'reschedule':
                return <RescheduleMenu setMenuOpen={setActiveMenu} />
            case 'illness':
                return <IllnessMenu setMenuOpen={setActiveMenu} />
            case 'destruct':
                return <DesConfirmation setMenuOpen={setActiveMenu} title={'Confirmation'} description={"Are you sure you want to cancel this user's holiday?"} onConfirm={cancelApproved}/>
            default:
                return null
        }
    }

    async function cancelApproved(){

    }

    return(
        <div className={"flex center-v p-16"}>
            {
                renderContent()
            }
            <h1>Approved Holidays for {week6Active ? 'Week 6' : 'Week 1-5'}</h1>
            <div className={"flex table-border table-tools"}>
                <button onClick={() => setWeek6Active(true)}>Week 6</button>
                <button onClick={() => setWeek6Active(false)}>Week 1-5</button>
            </div>
            <div className={"flex direction-column table-border p-16 g-20"}>
                <div className={'flex p-16 shadow space-between'}>
                    <div>
                        <h1>Title</h1>
                        <p>Approved date: date</p>
                        <p>Employee Name: name</p>
                        <p>Holiday duration: duration</p>
                    </div>
                    <div className={"flex center-v g-10"}>
                        <button onClick={() => setActiveMenu('reschedule')} className={"w-100 btn-warning"}>Reschedule</button>
                        {/*<button onClick={() => setActiveMenu('illness')} className={"w-100 btn-info"}>Register Illness</button>*/}
                        <button onClick={() => setActiveMenu('destruct')} className={"w-100 btn-destructive-outline"}>Cancel Holiday</button>
                    </div>
                </div>
            </div>
        </div>
    )
}