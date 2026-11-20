import "../index.css";
import {useEffect, useState} from "react";
import {type CompanyDayOffDTO, type DayOff, TypeOfHolidayDay} from "@/api/Api.ts";
import {dayOffApi} from "@/api/client.ts";
import {errorText} from "@/api/errors.ts";

// the generated type says Date, but the backend sends DateOnly as a "YYYY-MM-DD" string
function formatDate(value?: Date | string): string {
    if (!value) return "—";
    const date = new Date(String(value));
    if (isNaN(date.getTime())) return String(value);
    return date.toLocaleDateString("en-GB", {weekday: "short", day: "2-digit", month: "short", year: "numeric"});
}

interface AddHolidayProps {
    setMenuOpen: (open: boolean) => void,
    submitted: () => Promise<void>
}

// form for one new company day off, opened by "Add new holiday"
function AddHoliday({setMenuOpen, submitted}: AddHolidayProps) {

    const [name, setName] = useState("");
    const [date, setDate] = useState("");
    const [error, setError] = useState<string | null>(null);

    async function handleSubmit(e: React.FormEvent) {
        e.preventDefault();
        setError(null);
        // the backend reads DateOnly from the "YYYY-MM-DD" string of <input type="date">
        const day = {name, date} as unknown as CompanyDayOffDTO;
        try {
            await dayOffApi.add_company_days_off(day);
            await submitted();
            setMenuOpen(false);
        } catch (err) {
            // the backend returns the reason (e.g. "2027-12-25 already is a day off (Juledag)")
            setError(errorText(err, "Failed to add the holiday"));
            console.error(err);
        }
    }

    return (
        <div onClick={() => setMenuOpen(false)} className={"menu-bg flex center-v"}>
            <div onClick={(e) => e.stopPropagation()} className={"menu flex center-v"}>
                <form onSubmit={handleSubmit} className={"w-50 flex center-v g-6"}>
                    <h1>Add New Holiday</h1>
                    <div>
                        <label>Name: </label>
                        <input required type="text" name="name" placeholder="e.g. Juleaftensdag"
                               value={name} onChange={e => setName(e.target.value)}/>
                    </div>
                    <div>
                        <label>Date: </label>
                        <input required type="date" name="date" value={date} onChange={e => setDate(e.target.value)}/>
                    </div>
                    {error && <p className={"error"}>{error}</p>}
                    <button type="submit">Add</button>
                </form>
            </div>
        </div>
    );
}

// days off of the chosen year: Danish public holidays and company days off.
// the same year is used to show the days off and to load the Danish public holidays.
// a day off is never counted as a holiday day
export function HolidaysTab() {

    const [year, setYear] = useState(new Date().getFullYear());
    const [daysOff, setDaysOff] = useState<DayOff[]>([]);
    const [addOpen, setAddOpen] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [loading, setLoading] = useState(false);
    const [loadMessage, setLoadMessage] = useState<{ text: string, error: boolean } | null>(null);

    // the backend accepts 2000-2100, while typing a year ("20", "202") nothing is requested
    const validYear = year >= 2000 && year <= 2100;

    // reload the table every time another year is chosen
    useEffect(() => {
        setLoadMessage(null);
        if (validYear) {
            loadDaysOff();
        } else {
            setDaysOff([]);
        }
    }, [year]);

    async function loadDaysOff() {
        try {
            setDaysOff(await dayOffApi.get_days_off(year));
            setError(null);
        } catch (err) {
            setError("Failed to load the holidays");
            console.error(err);
        }
    }

    async function loadDanishHolidays() {
        setLoading(true);
        setLoadMessage(null);
        try {
            // the backend skips dates that already are days off, so loading a year twice adds nothing new
            const added = await dayOffApi.add_danish_holidays(year);
            setLoadMessage({
                text: added.length === 0
                    ? `The Danish public holidays of ${year} are already added`
                    : `${added.length} Danish public holidays added for ${year}`,
                error: false
            });
            await loadDaysOff();
        } catch (err) {
            // e.g. "The public holidays service is not available, try again later"
            setLoadMessage({text: errorText(err, "Failed to load the Danish holidays"), error: true});
            console.error(err);
        } finally {
            setLoading(false);
        }
    }

    return (
        <div className={"flex center-v p-16"}>
            {addOpen && <AddHoliday setMenuOpen={setAddOpen} submitted={loadDaysOff}/>}
            <h1>Holidays</h1>
            <div className={"year-picker"}>
                <button onClick={() => setYear(year - 1)} title="Previous year">&lt;</button>
                <input type="number" min="2000" max="2100" value={year} title="Year"
                       onChange={e => setYear(Number(e.target.value))}/>
                <button onClick={() => setYear(year + 1)} title="Next year">&gt;</button>
            </div>
            {!validYear && <p className={"error"}>Choose a year between 2000 and 2100</p>}
            {error && <p className={"error"}>{error}</p>}
            {loadMessage && <p className={loadMessage.error ? "error" : "success"}>{loadMessage.text}</p>}
            <div className={"table-border"}>
                <div className={"table-tools"}>
                    <button onClick={() => setAddOpen(true)}>Add new holiday</button>
                    <button onClick={loadDanishHolidays} disabled={loading || !validYear}>
                        {loading ? "Loading..." : `Load Danish holidays for ${year}`}
                    </button>
                </div>
                <table>
                    <thead>
                    <tr>
                        <th>Date</th>
                        <th>Name</th>
                        <th>Type</th>
                    </tr>
                    </thead>
                    <tbody>
                    {daysOff.length === 0 && (
                        <tr>
                            <td colSpan={3}>No holidays for {year} yet</td>
                        </tr>
                    )}
                    {daysOff.map(day => (
                        <tr key={day.id}>
                            <td>{formatDate(day.date)}</td>
                            <td>{day.name}</td>
                            <td>{day.holiday_type === TypeOfHolidayDay.Danish ? "Danish public holiday" : "Company day off"}</td>
                        </tr>
                    ))}
                    </tbody>
                </table>
            </div>
        </div>
    );
}
