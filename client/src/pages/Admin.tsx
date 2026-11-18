import "../index.css";
import {useEffect, useState} from "react";
import {EarningsTab} from "@/tabs/admin/EarningsTab.tsx";
import {UsersTab} from "@/tabs/admin/UsersTab.tsx";
import {ScheduleTab} from "@/tabs/admin/ScheduleTab.tsx";
import {CalendarTab} from "@/tabs/admin/CalendarTab.tsx";
import {ActivityTab} from "@/tabs/admin/ActivityTab.tsx";
import {useNavigate} from "react-router";
import {loginApi, timeApi} from "@/api/client.ts";
import storage from "@/storage.tsx";

export function Admin(){

    const [name, setName] = useState("");
    const [activeTab, setActiveTab] = useState('users')
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
            case 'earnings':
                return <EarningsTab />
            case 'schedule':
                return <ScheduleTab />
            case 'calendar':
                return <CalendarTab />
            case 'activity':
                return <ActivityTab />
            default:
                return <UsersTab />
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
                <h1>Admin Page</h1>
                <p>Welcome {name}</p>
                <div className={"flex center-v"}>
                    <button onClick={() => setActiveTab('users')}>Users and Roles <span className={"icon users-icon"} /></button>
                    {/*<button onClick={() => setActiveTab('earnings')}>Holiday Earnings <span className={"icon earn-icon"} /></button>*/}
                    {/*<button onClick={() => setActiveTab('schedule')}>Work Schedules <span className={"icon schedule-icon"} /></button>*/}
                    {/*<button onClick={() => setActiveTab('calendar')}>Company Calendar <span className={"icon calendar-icon"} /></button>*/}
                    {/*<button onClick={() => setActiveTab('activity')}>Recent Activity <span className={"icon activity-icon"} /></button>*/}
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