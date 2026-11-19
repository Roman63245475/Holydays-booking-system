import {useState} from "react";
import {ApiException, type CreateUserDTO, Role, type ScheduleDTO} from "@/api/Api.ts";
import {userApi} from "../api/client.ts";
// all numeric values of the enum: [1, 2, 3]
const roles = Object.values(Role).filter((v): v is Role => typeof v === "number");

// weekdays of the working schedule, key = the prefix of the ScheduleDTO fields (monday -> monday_start / monday_end)
const weekdays = [
    {key: "monday", label: "Monday"},
    {key: "tuesday", label: "Tuesday"},
    {key: "wednesday", label: "Wednesday"},
    {key: "thursday", label: "Thursday"},
    {key: "friday", label: "Friday"},
] as const;

type Weekday = typeof weekdays[number]["key"];
type DayTimes = { start: string, end: string };

// prefilled with the full time schedule of Business Esbjerg (Mon-Thu 7.5 h, Fri 7 h = 37 h a week),
// for a part time employee the admin changes the times or clears both fields of a day off
const defaultTimes: Record<Weekday, DayTimes> = {
    monday: {start: "08:00", end: "15:30"},
    tuesday: {start: "08:00", end: "15:30"},
    wednesday: {start: "08:00", end: "15:30"},
    thursday: {start: "08:00", end: "15:30"},
    friday: {start: "08:00", end: "15:00"},
};

interface CreateUserProps {
    setMenuOpen: (value: (((prevState: boolean) => boolean) | boolean)) => void,
    submitted: () => Promise<void>
}

export function CreateUser({setMenuOpen, submitted}: CreateUserProps) {

    const [name, setName] = useState('');
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [role, setRole] = useState<Role>(Role.User);
    const [times, setTimes] = useState<Record<Weekday, DayTimes>>(defaultTimes);
    const [error, setError] = useState<string | null>(null);

    function setTime(day: Weekday, field: keyof DayTimes, value: string) {
        setTimes(prev => ({...prev, [day]: {...prev[day], [field]: value}}));
    }

    // collects the time fields into the ScheduleDTO, an empty field becomes undefined (= day off)
    function buildSchedule(): ScheduleDTO {
        const schedule: ScheduleDTO = {};
        for (const {key} of weekdays) {
            schedule[`${key}_start`] = times[key].start || undefined;
            schedule[`${key}_end`] = times[key].end || undefined;
        }
        return schedule;
    }

    async function handleSubmit(e: React.FormEvent) {
        e.preventDefault();
        setError(null);
        const user: CreateUserDTO = {name, email, password, role, schedule: buildSchedule()};
        try {
            await userApi.create(user);
            await submitted();
            setMenuOpen(false);
        } catch (err) {
            // the backend returns the validation message (e.g. "Monday: end time must be after start time")
            setError(err instanceof ApiException ? err.response : "Failed to create user");
            console.error(err);
        }
    }

    return (
        <div onClick={() => setMenuOpen(false)} className={"menu-bg flex center-v"}>
            <div onClick={(e) => e.stopPropagation()} className={"menu flex center-v"}>
                <form onSubmit={handleSubmit} className={"w-50 flex center-v g-6"}>
                    <h1>Create New User</h1>
                    <div>
                        <label>Name: </label>
                        <input required={true} type={'text'} name={'name'} placeholder={'Enter name'}
                               onChange={e => setName(e.target.value)}></input>
                    </div>
                    <div>
                        <label>Email: </label>
                        <input required={true} type={'email'} name={'email'} placeholder={'Enter email'}
                               onChange={e => setEmail(e.target.value)}></input>
                    </div>
                    <div>
                        <label>Password: </label>
                        <input required={true} type={'password'} name={'password'} placeholder={'Enter password'}
                               onChange={e => setPassword(e.target.value)}></input>
                    </div>
                    <div>
                        <label>Role: </label>
                        <select value={role} onChange={e => setRole(Number(e.target.value) as Role)}>
                            {roles.map(r => <option key={r} value={r}>{Role[r]}</option>)}
                        </select>
                    </div>

                    <h3>Working schedule</h3>
                    <p>Leave both fields of a day empty for a day off.</p>
                    {weekdays.map(({key, label}) => (
                        <div key={key}>
                            <label>{label}: </label>
                            <input type={'time'} name={`${key}_start`} value={times[key].start}
                                   onChange={e => setTime(key, "start", e.target.value)}></input>
                            <span> - </span>
                            <input type={'time'} name={`${key}_end`} value={times[key].end}
                                   onChange={e => setTime(key, "end", e.target.value)}></input>
                        </div>
                    ))}

                    {error && <p className={"error"}>{error}</p>}
                    <button type={'submit'}>Submit</button>
                </form>
            </div>
        </div>
    )
}