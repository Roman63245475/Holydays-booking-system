import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import { loginApi, transferApi, userApi } from "../api/client";
import { errorText } from "../api/errors";
import { TransferDialog } from "../dialog/TransferDialog";
import { TransferRequests } from "../TransferRequests";
import type { HolidayAccountView, UserViewPage } from "../api/Api";
import storage from "../storage";
import { DateTimeRangePicker } from "../DateTimeRangePicker";
import { accountTitle, HolidayBalance } from "../HolidayBalance";
import { OnGoingRequest } from "../OnGoingRequest";
import { ApprovedRequests } from "../ApprovedRequests";
import { HolidayCalendar } from "../HolidayCalendar";
import { HolidayHistory } from "../HolidayHistory";
import { PlanAhead } from "../PlanAhead";

export function User() {
    const [user, setUser] = useState<UserViewPage | null>(null);
    const [activeTab, setActiveTab] = useState<"home" | "onGoing" | "approved" | "history" | "calendar" | "request">("home");
    const [selectedAccount, setSelectedAccount] = useState<HolidayAccountView | null>(null);
    // the balance the employee wants to transfer to the next holiday period (dialog open)
    const [transferAccount, setTransferAccount] = useState<HolidayAccountView | null>(null);
    const [transferError, setTransferError] = useState<string | null>(null);
    // changes on every menu click: the tab is mounted again and reloads its data, also when it is already open
    const [reloadKey, setReloadKey] = useState(0);
    const navigate = useNavigate();

    // reloaded every time Home is opened: balances and the main holiday notice change when requests are
    // approved, edited, withdrawn or cancelled in the other tabs
    useEffect(() => {
        if (activeTab !== "home") return;
        userApi.me()
            .then(setUser)
            .catch(() => navigate("/"));
    }, [navigate, activeTab, reloadKey]);

    function openTab(tab: "home" | "onGoing" | "approved" | "history" | "calendar") {
        setActiveTab(tab);
        setReloadKey((k) => k + 1);
    }

    async function logOut() {
        await loginApi.logout();
        storage.remove("user");
        navigate("/");
    }

    // reloads Home (balances and the transfer list)
    function reloadHome() {
        setReloadKey((k) => k + 1);
    }

    async function withdrawTransfer(account: HolidayAccountView) {
        if (!account.pending_transfer_id) return;
        if (!window.confirm("Withdraw the transfer request?")) return;
        try {
            await transferApi.withdrawTransfer(account.pending_transfer_id);
            setTransferError(null);
        } catch (err) {
            setTransferError(errorText(err, "Could not withdraw the transfer request."));
        }
        reloadHome();
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
                    <button onClick={() => openTab("home")}>Home</button>
                    <button onClick={() => openTab("onGoing")}>Ongoing Requests</button>
                    <button onClick={() => openTab("approved")}>Approved Holidays</button>
                    <button onClick={() => openTab("history")}>Holiday History</button>
                    <button onClick={() => openTab("calendar")}>Team Calendar</button>
                </div>

                <button className="g-6" onClick={logOut}>Log Out</button>
            </aside>

            <main className="w-100 p-16" key={reloadKey}>
                {activeTab === "home" ? (
                    <>
                        <h1>Good Morning, {user?.name}</h1>

                        {user?.end_date && (
                            <p className="main-holiday-notice">
                                Your employment ends on {String(user.end_date)}. Holiday can be requested until that day,
                                what is left then is settled with your final salary.
                            </p>
                        )}
                        {user && <MainHolidayNotice user={user} />}
                        {transferError && <p className="error" role="alert">{transferError}</p>}
                        {transferAccount && (
                            <TransferDialog account={transferAccount} onClose={() => setTransferAccount(null)} onSent={reloadHome} />
                        )}

                        {/* every account that has not expired yet: up to 2 of each type at the same time */}
                        <div className="holiday-balances">
                            {user?.accounts?.map((account) => (
                                <HolidayBalance
                                    key={account.id}
                                    account={account}
                                    onSelect={handleSelectAccount}
                                    onTransfer={setTransferAccount}
                                    onWithdrawTransfer={withdrawTransfer}
                                />
                            ))}
                        </div>

                        {/* by default: the start of the next main holiday window (how much is there for the summer) */}
                        {user && <PlanAhead initialDate={String(user.main_holiday_window_start ?? "")} />}

                        {/* only shown when the employee has ever asked for a transfer */}
                        <TransferRequests />
                    </>
                ) : activeTab === "onGoing" ? (
                    <OnGoingRequest />
                ) : activeTab === "approved" ? (
                    <ApprovedRequests />
                ) : activeTab === "history" ? (
                    <HolidayHistory />
                ) : activeTab === "calendar" ? (
                    <HolidayCalendar title="Team Calendar" />
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

// the employee has the right to a main holiday (hovedferie) of 3 weeks in the summer. reminds them while it is not
// requested yet; a main holiday needs a longer notice than other holiday
function MainHolidayNotice({ user }: { user: UserViewPage }) {
    const from = formatDay(user.main_holiday_window_start);
    const to = formatDay(user.main_holiday_window_end);
    const weeks = (user.main_holiday_length_days ?? 15) / 5;

    if (user.main_holiday_status === "approved") {
        return <p className="main-holiday-notice main-holiday-notice--ok">Your main holiday for {user.main_holiday_year} is approved.</p>;
    }
    if (user.main_holiday_status === "pending") {
        return <p className="main-holiday-notice main-holiday-notice--ok">Your main holiday for {user.main_holiday_year} is requested and waits for approval.</p>;
    }
    return (
        <p className="main-holiday-notice">
            Your main holiday for {user.main_holiday_year} is not planned yet: you have the right to {weeks} weeks
            in a row between {from} and {to}. A main holiday must be requested {user.main_holiday_notice_months} months in advance.
        </p>
    );
}

// dates come from the API as "yyyy-MM-dd" strings (typed as Date by the generated client)
function formatDay(value?: Date | string): string {
    const date = value ? new Date(value) : null;
    return date && !Number.isNaN(date.getTime())
        ? date.toLocaleDateString("en-GB", { day: "numeric", month: "long" })
        : "—";
}
