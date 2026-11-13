import {useState} from "react";
import {EarningsTab} from "@/tabs/EarningsTab.tsx";
import {UsersTab} from "@/tabs/UsersTab.tsx";
import {ScheduleTab} from "@/tabs/ScheduleTab.tsx";
import {CalendarTab} from "@/tabs/CalendarTab.tsx";
import {ActivityTab} from "@/tabs/ActivityTab.tsx";

export function Admin(){

    const [activeTab, setActiveTab] = useState('users')

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

    return(
        <div className={"main-container"}>
            <aside className={"side-menu"}>
                <h1>Admin Page</h1>
                <p>Welcome [admin]</p>
                <div className={"center-v"}>
                    <button onClick={() => setActiveTab('users')}>Users and Roles <span className={"icon users-icon"} /></button>
                    <button onClick={() => setActiveTab('earnings')}>Holiday Earnings <span className={"icon earn-icon"} /></button>
                    <button onClick={() => setActiveTab('schedule')}>Work Schedules <span className={"icon schedule-icon"} /></button>
                    <button onClick={() => setActiveTab('calendar')}>Company Calendar <span className={"icon calendar-icon"} /></button>
                    <button onClick={() => setActiveTab('activity')}>Recent Activity <span className={"icon activity-icon"} /></button>
                </div>
                <button className={"g-6"}>Log Out <span className={"icon log-out-icon"} /></button>
            </aside>
            <div className={"w-100"}>
                {
                    renderContent()
                }
            </div>
        </div>
    )
}