import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import { loginApi, userApi } from "../api/client";
import type { HolidayAccountView, UserViewPage } from "../api/Api";
import storage from "../storage";
import { DateTimeRangePicker } from "../DateTimeRangePicker";
import { accountTitle, HolidayBalance } from "../HolidayBalance";
import { OnGoingRequest } from "../OnGoingRequest";

export function User() {
    const [user, setUser] = useState<UserViewPage | null>(null);
    const [activeTab, setActiveTab] = useState<"home" | "onGoing" | "history" | "request">("home");
    const [selectedAccount, setSelectedAccount] = useState<HolidayAccountView | null>(null);
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

    function handleSelectAccount(account: HolidayAccountView) {
        if (!account.id) return;
        setSelectedAccount(account);
        setActiveTab("request");
    }

    return (
        <div className="main-container">
            <aside className="side-menu">
                <h1>Welcome</h1>

                <div className="flex center-v">
                    <button onClick={() => setActiveTab("home")}>Home</button>
                    <button onClick={() => setActiveTab("onGoing")}>Ongoing Requests</button>
                    <button onClick={() => setActiveTab("history")}>Holiday History</button>
                </div>

                <button className="g-6" onClick={logOut}>Log Out</button>
            </aside>

            <main className="w-100 p-16">
                {activeTab === "home" ? (
                    <>
                        <h1>Good Morning, {user?.name}</h1>

                        {/* every account that has not expired yet: up to 2 of each type at the same time */}
                        <div className="holiday-balances">
                            {user?.accounts?.map((account) => (
                                <HolidayBalance
                                    key={account.id}
                                    account={account}
                                    onSelect={handleSelectAccount}
                                />
                            ))}
                        </div>
                    </>
                ) : activeTab === "onGoing" ? (
                    <OnGoingRequest />
                ) : activeTab === "history" ? (
                    <h2>Your holidays in the past</h2>
                ) : activeTab === "request" ? (
                    <div className="p-8">
                        <h2>
                            Requesting from {selectedAccount ? accountTitle(selectedAccount) : "holidays"}
                        </h2>
                        {selectedAccount?.id ? (
                            <DateTimeRangePicker
                                accountId={selectedAccount.id}
                                onChange={(range) => {
                                    console.log("Selected range:", range);
                                }}
                            />
                        ) : <p>Please select a holiday balance first.</p>}
                    </div>
                ) : null}
            </main>
        </div>
    );
}
