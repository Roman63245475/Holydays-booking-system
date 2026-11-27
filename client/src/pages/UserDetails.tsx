import "../index.css";
import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { userApi } from "@/api/client.ts";
import { ApiException, Role, type ScheduleDTO, type UserDetailsDTO } from "@/api/Api.ts";
import { errorText } from "@/api/errors.ts";
import { HolidayBalance } from "@/HolidayBalance.tsx";
import { HolidayHistory } from "@/HolidayHistory.tsx";

const DAYS: { name: string; start: keyof ScheduleDTO; end: keyof ScheduleDTO }[] = [
    { name: "Mon", start: "monday_start", end: "monday_end" },
    { name: "Tue", start: "tuesday_start", end: "tuesday_end" },
    { name: "Wed", start: "wednesday_start", end: "wednesday_end" },
    { name: "Thu", start: "thursday_start", end: "thursday_end" },
    { name: "Fri", start: "friday_start", end: "friday_end" },
];

// "08:00:00" -> "08:00"
function time(value: unknown): string {
    return value ? String(value).slice(0, 5) : "";
}

const MAIN_HOLIDAY: Record<string, string> = {
    approved: "approved",
    pending: "requested, waiting for approval",
    none: "not requested yet",
};

// the system owner's page of one user (double-click on a row of Users and Roles): their data, the schedule that
// applies now, their holiday accounts and their holiday history
export function UserDetails() {
    const { id } = useParams();
    const navigate = useNavigate();
    const [user, setUser] = useState<UserDetailsDTO | null>(null);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        if (!localStorage.getItem("user")) {
            navigate("/");
            return;
        }
        if (!id) return;
        userApi.get_user_details(id)
            .then(setUser)
            .catch((reason) => {
                if (reason instanceof ApiException && reason.status === 401) {
                    navigate("/");
                    return;
                }
                setError(errorText(reason, "Could not load the user."));
            });
    }, [id, navigate]);

    const page = user?.page;

    return (
        <div className="user-details">
            <button type="button" className="user-details__back" onClick={() => navigate("/admin_page")}>‹ Back to users</button>

            {error ? (
                <p className="ongoing-requests__message ongoing-requests__message--error" role="alert">{error}</p>
            ) : !user ? (
                <p className="ongoing-requests__message" role="status">Loading…</p>
            ) : (
                <>
                    <section className="user-details__card">
                        <div>
                            <p className="ongoing-requests__eyebrow">EMPLOYEE</p>
                            <h1>{user.name}</h1>
                            <p className="ongoing-requests__subtitle">{user.email}</p>
                        </div>
                        <dl className="user-details__facts">
                            <div><dt>Role</dt><dd>{user.role !== undefined ? Role[user.role] : "—"}</dd></div>
                            <div><dt>Start date</dt><dd>{String(user.start_date ?? "—")}</dd></div>
                            <div><dt>Weekly hours</dt><dd>{(user.weekly_hours ?? 0).toFixed(2)} h</dd></div>
                            <div>
                                <dt>Main holiday {page?.main_holiday_year}</dt>
                                <dd>{MAIN_HOLIDAY[page?.main_holiday_status ?? "none"] ?? "—"}</dd>
                            </div>
                        </dl>
                        {user.schedule && (
                            <div className="user-details__schedule">
                                {DAYS.map((d) => {
                                    const start = time(user.schedule?.[d.start]);
                                    const end = time(user.schedule?.[d.end]);
                                    return (
                                        <div key={d.name} className={start ? "" : "is-off"}>
                                            <span>{d.name}</span>
                                            <strong>{start ? `${start}–${end}` : "Off"}</strong>
                                        </div>
                                    );
                                })}
                            </div>
                        )}
                    </section>

                    <h2 className="user-details__heading">Holiday accounts</h2>
                    {(page?.accounts ?? []).length === 0 ? (
                        <p className="ongoing-requests__message">This user has no holiday accounts that can still be used.</p>
                    ) : (
                        <div className="holiday-balances">
                            {page?.accounts?.map((account) => <HolidayBalance key={account.id} account={account} />)}
                        </div>
                    )}

                    <HolidayHistory userId={user.id} title={`${user.name}'s holiday history`} />
                </>
            )}
        </div>
    );
}
