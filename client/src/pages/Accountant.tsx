import "../index.css";
import {useEffect, useState} from "react";
import {HolidayCalendar} from "@/HolidayCalendar.tsx";
import {useNavigate, useSearchParams} from "react-router";
import {loginApi, timeApi} from "@/api/client.ts";
import storage from "@/storage.tsx";
import {RequestsTab} from "@/tabs/accountant/RequestsTab.tsx";
import {ApprovedTab} from "@/tabs/accountant/ApprovedTab.tsx";
import {BalanceTab} from "@/tabs/accountant/BalanceTab.tsx";
import {CancellationRequestsTab} from "@/tabs/accountant/CancellationRequestsTab.tsx";
import {TransferRequestsTab} from "@/tabs/accountant/TransferRequestsTab.tsx";
import UsersTab from "@/tabs/admin/UsersTab.tsx";

export function Accountant(){

    const [name, setName] = useState("");
    // ?tab=users: "Back" from an employee's page returns to the Users tab
    const [searchParams] = useSearchParams()
    const [activeTab, setActiveTab] = useState(searchParams.get('tab') ?? 'requests')
    // changes on every menu click: the tab is mounted again and reloads its data, also when it is already open
    // (otherwise new requests only appear after switching to another tab and back)
    const [reloadKey, setReloadKey] = useState(0)

    function openTab(tab: string) {
        setActiveTab(tab)
        setReloadKey(k => k + 1)
    }
    const navigate = useNavigate();

    useEffect(() => {
        const userData = localStorage.getItem("user");
        if (userData){
            const user = JSON.parse(userData);
            setName(user.name);
        }else{
            navigate("/")
        }
    }, [])

    const renderContent = () => {
        switch(activeTab){
            case 'cancellations':
                return <CancellationRequestsTab />
            case 'transfers':
                return <TransferRequestsTab />
            case 'approved':
                return <ApprovedTab />
            case 'calendar':
                return <HolidayCalendar title="Holiday Calendar" />
            case 'balances':
                return <BalanceTab />
            case 'users':
                return <UsersTab mode="accountant" />
            default:
                return <RequestsTab />
        }
    }

    async function simulate_month(){
        const response = await timeApi.next_month()
        console.log(response)
    }


    async function logOut(){
        await loginApi.logout()
        storage.remove('user')
        navigate('/')
    }

    return(
        <div className={"main-container"}>
            <aside className={"side-menu"}>
                <h1>Accountant Page</h1>
                <p>Welcome {name}</p>
                <div className={"flex center-v"}>
                    <button onClick={() => openTab('requests')}>Pending Requests <span className={"icon pending-icon"} /></button>
                    <button onClick={() => openTab('cancellations')}>Cancellation Requests <span className={"icon pending-icon"} /></button>
                    <button onClick={() => openTab('transfers')}>Transfer Requests <span className={"icon pending-icon"} /></button>
                    <button onClick={() => openTab('approved')}>Approved Holidays <span className={"icon approved-icon"} /></button>
                    <button onClick={() => openTab('calendar')}>Holiday Calendar <span className={"icon calendar-icon"} /></button>
                    <button onClick={() => openTab('balances')}>Employee Balances <span className={"icon balance-icon"} /></button>
                    <button onClick={() => openTab('users')}>Users <span className={"icon users-icon"} /></button>
                </div>
                <button className={"g-6"} onClick={logOut}>Log Out <span className={"icon log-out-icon"} /></button>
            </aside>
            <div className={"w-100"}>
                <div key={reloadKey}>
                    {renderContent()}
                </div>
                <button onClick={() => simulate_month()}>Simulate month</button>
            </div>
        </div>
    )
}