import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import { loginApi, userApi } from "../api/client";
import type { ClientUser } from "../api/Api";
import storage from "../storage";
import { DateTimeRangePicker } from "../DateTimeRangePicker";
import { HolidayBalance, type HolidayKind } from "../HolidayBalance";

export function User() {
    const [user, setUser] = useState<ClientUser | null>(null);
    const [activeTab, setActiveTab] = useState<"home" | "history" | "request">("home");
    const [selectedHolidayKind, setSelectedHolidayKind] = useState<HolidayKind | null>(null);
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
                </div>

                <button className="g-6" onClick={logOut}>Log Out</button>
            </aside>

            <main className="w-100 p-16">
                {activeTab === "home" ? (
                    <>
                    
                        <h1>Good Morning, {user?.name}</h1>

                        <div className="holiday-balances">
                            <HolidayBalance
                                title="Weeks 1-5"
                                earned={user?.law_earned_holiday_day}
                                used={user?.law_used_holiday_day}
                                remaining={user?.available_holiday_days}
                                unit="days"
                                value="law"
                                onSelect={(kind) => {
                                    setSelectedHolidayKind(kind);
                                    setActiveTab("request");
                                }}
                            />
                            <HolidayBalance
                                title="Week 6"
                                earned={user?.agreed_earned_holiday_hours}
                                used={user?.agreed_used_holiday_hours}
                                remaining={user?.available_holiday_hours}
                                unit="hours"
                                value="agreed"
                                onSelect={(kind) => {
                                    setSelectedHolidayKind(kind);
                                    setActiveTab("request");
                                }}
                            />
                        </div>
                    </>
                ) : activeTab === "history" ? (
                    <h2>Your holidays in the past</h2>
                ) : activeTab === "request" ? (
                    <div className="p-8">
                        <h2>
                            Requesting {selectedHolidayKind === "law"
                                ? "statutory holidays (days)"
                                : selectedHolidayKind === "agreed"
                                    ? "agreed holidays (hours)"
                                    : "holidays"}
                        </h2>
                        <DateTimeRangePicker
                            holidayKind={selectedHolidayKind ?? "law"}
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
