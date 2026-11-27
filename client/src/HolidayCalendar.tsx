import { useEffect, useMemo, useState } from "react";
import { calendarApi } from "./api/client";
import { HolidayType, type CalendarHolidayView, type CalendarMonthView } from "./api/Api";
import { errorText } from "./api/errors";

// month calendar of everybody's holidays (shared by the employee, accountant and admin pages).
// a holiday is shown on the working days it covers: weekends and public holidays / company days off are not holiday
// (they don't cost holiday hours), so they are greyed out instead
const WEEKDAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
const MONTHS = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October",
    "November", "December"];
// how many names fit in one day, the rest is "+N more" (all names are in the tooltip)
const MAX_CHIPS = 4;

function pad(value: number): string {
    return String(value).padStart(2, "0");
}

// "yyyy-MM-dd": compared as text, no time zones involved
function dayKey(year: number, month: number, day: number): string {
    return `${year}-${pad(month)}-${pad(day)}`;
}

function firstName(name?: string): string {
    return (name ?? "").split(" ")[0] || "—";
}

interface Day {
    key: string;
    day: number;
    weekend: boolean;
    dayOff?: string;
    holidays: CalendarHolidayView[];
}

export function HolidayCalendar({ title = "Holiday Calendar" }: { title?: string }) {
    const now = new Date();
    const [year, setYear] = useState(now.getFullYear());
    const [month, setMonth] = useState(now.getMonth() + 1);
    const [data, setData] = useState<CalendarMonthView | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [onlyMine, setOnlyMine] = useState(false);
    // the first load jumps to the month of the simulated "today" (the time simulator can be ahead of the real date)
    const [jumped, setJumped] = useState(false);

    useEffect(() => {
        let cancelled = false;
        calendarApi.get_month(year, month)
            .then((result) => {
                if (cancelled) return;
                const today = String(result.today ?? "");
                if (!jumped && today) {
                    setJumped(true);
                    const [y = year, m = month] = today.split("-").map(Number);
                    if (y !== year || m !== month) {
                        setYear(y);
                        setMonth(m);
                        return;
                    }
                }
                setData(result);
                setError(null);
            })
            .catch((reason) => !cancelled && setError(errorText(reason, "Could not load the calendar.")));
        return () => {
            cancelled = true;
        };
    }, [year, month]);

    function move(step: number) {
        const index = year * 12 + (month - 1) + step;
        setYear(Math.floor(index / 12));
        setMonth((index % 12) + 1);
    }

    const today = String(data?.today ?? "");
    const hasMine = (data?.holidays ?? []).some((h) => h.is_mine);

    // the month as weeks of 7 cells (Monday first), null = a day of the month before / after
    const weeks = useMemo(() => {
        if (!data) return [];
        const holidays = (data.holidays ?? []).filter((h) => !onlyMine || h.is_mine);
        const daysOff = new Map((data.days_off ?? []).map((d) => [String(d.date), d.name ?? "Day off"]));
        const daysInMonth = new Date(year, month, 0).getDate();
        // getDay(): 0 = Sunday, the grid starts on Monday
        const offset = (new Date(year, month - 1, 1).getDay() + 6) % 7;

        const cells: (Day | null)[] = Array(offset).fill(null);
        for (let day = 1; day <= daysInMonth; day++) {
            const key = dayKey(year, month, day);
            const weekend = (offset + day - 1) % 7 >= 5;
            const dayOff = daysOff.get(key);
            cells.push({
                key,
                day,
                weekend,
                dayOff,
                holidays: weekend || dayOff ? [] : holidays.filter((h) => (h.start_date ?? "") <= key && key <= (h.end_date ?? "")),
            });
        }
        while (cells.length % 7 !== 0) cells.push(null);

        const result: (Day | null)[][] = [];
        for (let i = 0; i < cells.length; i += 7) result.push(cells.slice(i, i + 7));
        return result;
    }, [data, onlyMine, year, month]);

    return (
        <section className="ongoing-requests holiday-calendar">
            <header className="ongoing-requests__header">
                <div>
                    <p className="ongoing-requests__eyebrow">HOLIDAY PLANNING</p>
                    <h2>{title}</h2>
                    <p className="ongoing-requests__subtitle">Who is on holiday, so plans can take into account how many people are away.</p>
                </div>
                <div className="holiday-calendar__filter" role="group" aria-label="Whose holidays">
                    <button type="button" className={!onlyMine ? "is-active" : ""} onClick={() => setOnlyMine(false)}>Everyone</button>
                    <button type="button" className={onlyMine ? "is-active" : ""} onClick={() => setOnlyMine(true)}
                            disabled={!hasMine && !onlyMine} title={!hasMine ? "You have no holidays this month" : undefined}>Only me</button>
                </div>
            </header>

            <div className="holiday-calendar__nav">
                <button type="button" onClick={() => move(-1)}>‹ Previous</button>
                <strong>{MONTHS[month - 1]} {year}</strong>
                <button type="button" onClick={() => move(1)}>Next ›</button>
            </div>

            {error ? (
                <p className="ongoing-requests__message ongoing-requests__message--error" role="alert">{error}</p>
            ) : !data ? (
                <p className="ongoing-requests__message" role="status">Loading the calendar…</p>
            ) : (
                <div className="holiday-calendar__scroll">
                    <div className="holiday-calendar__grid">
                        {WEEKDAYS.map((name, i) => (
                            <div key={name} className={`holiday-calendar__weekday${i >= 5 ? " is-weekend" : ""}`}>{name}</div>
                        ))}
                        {weeks.flat().map((cell, i) => cell === null ? (
                            <div key={`empty-${i}`} className="holiday-calendar__day is-outside" />
                        ) : (
                            <DayCell key={cell.key} cell={cell} today={today} employees={data.employee_count ?? 0} />
                        ))}
                    </div>
                </div>
            )}

            <div className="holiday-calendar__legend">
                <span><i className="holiday-chip holiday-chip--law" /> Weeks 1-5</span>
                <span><i className="holiday-chip holiday-chip--week6" /> Week 6</span>
                <span><i className="holiday-chip holiday-chip--law holiday-chip--pending" /> Waiting for approval</span>
                <span><i className="holiday-calendar__swatch" /> Weekend, public holiday or company day off</span>
            </div>
        </section>
    );
}

function DayCell({ cell, today, employees }: { cell: Day; today: string; employees: number }) {
    const approved = new Set(cell.holidays.filter((h) => h.status === "approved").map((h) => h.user_id));
    const shown = cell.holidays.slice(0, MAX_CHIPS);
    const hidden = cell.holidays.length - shown.length;
    const classes = ["holiday-calendar__day"];
    if (cell.weekend || cell.dayOff) classes.push("is-off");
    if (cell.key < today) classes.push("is-past");
    if (cell.key === today) classes.push("is-today");

    return (
        <div className={classes.join(" ")}>
            <div className="holiday-calendar__day-head">
                <span className="holiday-calendar__date">{cell.day}</span>
                {approved.size > 0 && (
                    <span className="holiday-calendar__count" title={`${approved.size} of ${employees} employees on holiday`}>
                        {approved.size} away
                    </span>
                )}
            </div>
            {cell.dayOff && <span className="holiday-calendar__day-off">{cell.dayOff}</span>}
            {shown.map((h) => <HolidayChip key={h.request_id} holiday={h} />)}
            {hidden > 0 && (
                <span className="holiday-calendar__more" title={cell.holidays.slice(MAX_CHIPS).map((h) => h.user_name).join(", ")}>
                    +{hidden} more
                </span>
            )}
        </div>
    );
}

function HolidayChip({ holiday }: { holiday: CalendarHolidayView }) {
    const week6 = holiday.holiday_type === HolidayType.FerieUge;
    const pending = holiday.status === "pending";
    const classes = ["holiday-chip", week6 ? "holiday-chip--week6" : "holiday-chip--law"];
    if (pending) classes.push("holiday-chip--pending");
    if (holiday.is_mine) classes.push("holiday-chip--mine");
    const kind = week6 ? "week 6" : "weeks 1-5";

    return (
        <span className={classes.join(" ")}
              title={`${holiday.user_name}: ${holiday.start_date} to ${holiday.end_date} (${kind}${pending ? ", waiting for approval" : ""})`}>
            {holiday.is_mine ? "You" : firstName(holiday.user_name)}
        </span>
    );
}
