import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import { loginApi, userApi } from "./api/client";
import type { ClientUser } from "./api/Api";
import storage from "./storage";
import { DateTimeRangePicker } from "./DateTimeRangePicker";


export function User() {
    const [user, setUser] = useState<ClientUser | null>(null);
    const [activeTab, setActiveTab] = useState<"home" | "history" | "request">("home");
    const navigate = useNavigate();

    useEffect(() => {
        userApi.me()
            .then(setUser)
            .catch(() => navigate("/"));
    }, [navigate]);

    async function logOut() {
        await loginApi.logout();
        storage.remove("user");
        navigate("/");
    }

    return (
        <div className="main-container">
            <aside className="side-menu">
                <h1>Welcome</h1>

                <div className="flex center-v">
                    <button onClick={() => setActiveTab("home")}>Home</button>
                    <button onClick={() => setActiveTab("history")}>Holiday History</button>
                    <button onClick={() => setActiveTab("request")}>request for a holiday</button>
                </div>

                <button className="g-6" onClick={logOut}>Log Out</button>
            </aside>

            <main className="w-100 p-16">
                {activeTab === "home" ? (
                    <>
                        <h1>Good Morning, {user?.name}</h1>
                        <h2>Earned Holiday hours: {user?.earned_holiday_hours ?? "—"} h</h2>
                        <h2>Used Holiday hours: {user?.used_holiday_hours}</h2>
                        <h2>Balance of available days for a holiday:{user?.available_holiday_hours}h ({(user?.available_holiday_hours/24).toFixed(2)} Days) </h2>
                    </>
                ) : activeTab === "history" ? (
                    <h2>Your holidays in the past</h2>
                ) : activeTab === "request" ? (
                    <div className="p-8">
                        <DateTimeRangePicker
                            onChange={(range) => {
                                console.log("Selected range:", range);
                            }}
                        />
                    </div>
                ) : null}
            </main>
        </div>
    );
}