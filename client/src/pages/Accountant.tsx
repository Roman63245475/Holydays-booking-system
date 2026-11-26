import "../index.css";
import {useEffect, useState} from "react";
import {EarningsTab} from "@/tabs/admin/EarningsTab.tsx";
import UsersTab from "@/tabs/admin/UsersTab.tsx";
import {ScheduleTab} from "@/tabs/admin/ScheduleTab.tsx";
import {CalendarTab} from "@/tabs/admin/CalendarTab.tsx";
import {ActivityTab} from "@/tabs/admin/ActivityTab.tsx";
import {useNavigate} from "react-router";
import {loginApi, timeApi} from "@/api/client.ts";
import storage from "@/storage.tsx";
import {RequestsTab} from "@/tabs/accountant/RequestsTab.tsx";
import {ApprovedTab} from "@/tabs/accountant/ApprovedTab.tsx";
import {BalanceTab} from "@/tabs/accountant/BalanceTab.tsx";
import {CancellationRequestsTab} from "@/tabs/accountant/CancellationRequestsTab.tsx";

export function Accountant(){

    const [name, setName] = useState("");
    const [activeTab, setActiveTab] = useState('requests')
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
            case 'approved':
                return <ApprovedTab />
            case 'balances':
                return <BalanceTab />
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
                    <button onClick={() => setActiveTab('requests')}>Pending Requests <span className={"icon pending-icon"} /></button>
                    <button onClick={() => setActiveTab('cancellations')}>Cancellation Requests <span className={"icon pending-icon"} /></button>
                    <button onClick={() => setActiveTab('approved')}>Approved Holidays <span className={"icon approved-icon"} /></button>
                    <button onClick={() => setActiveTab('balances')}>Employee Balances <span className={"icon balance-icon"} /></button>
                </div>
                <button className={"g-6"} onClick={logOut}>Log Out <span className={"icon log-out-icon"} /></button>
            </aside>
            <div className={"w-100"}>
                {
                    renderContent()
                }
                <button onClick={() => simulate_month()}>Simulate month</button>
            </div>
        </div>
    )
}