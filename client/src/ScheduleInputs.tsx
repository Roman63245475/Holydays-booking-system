import type {ScheduleDTO} from "@/api/Api.ts";

// weekdays of a working schedule, key = the prefix of the ScheduleDTO fields (monday -> monday_start / monday_end)
const weekdays = [
    {key: "monday", label: "Monday"},
    {key: "tuesday", label: "Tuesday"},
    {key: "wednesday", label: "Wednesday"},
    {key: "thursday", label: "Thursday"},
    {key: "friday", label: "Friday"},
] as const;

type Weekday = typeof weekdays[number]["key"];

// the full time week of Business Esbjerg (Mon-Thu 7.5 h, Fri 7 h = 37 h), used to prefill a schedule
export const fullTimeSchedule: ScheduleDTO = {
    monday_start: "08:00", monday_end: "15:30",
    tuesday_start: "08:00", tuesday_end: "15:30",
    wednesday_start: "08:00", wednesday_end: "15:30",
    thursday_start: "08:00", thursday_end: "15:30",
    friday_start: "08:00", friday_end: "15:00",
};

interface ScheduleInputsProps {
    value: ScheduleDTO,
    onChange: (schedule: ScheduleDTO) => void,
    // times that come back when "Day off" is unchecked again
    defaults?: ScheduleDTO,
}

// start / end time for Monday-Friday with a "Day off" checkbox per day.
// a day off is sent as start = end = undefined (null on the backend), exactly what the backend expects for a day off.
// used in the create user form (employee's schedule) and in Holiday Rules (standard working week)
export function ScheduleInputs({value, onChange, defaults = fullTimeSchedule}: ScheduleInputsProps) {

    function isDayOff(day: Weekday): boolean {
        return !value[`${day}_start`] && !value[`${day}_end`];
    }

    function setTime(field: keyof ScheduleDTO, time: string) {
        onChange({...value, [field]: time || undefined});
    }

    function setDayOff(day: Weekday, dayOff: boolean) {
        onChange({
            ...value,
            // day off: clear both times; working day again: take the default times of that day
            [`${day}_start`]: dayOff ? undefined : (defaults[`${day}_start`] ?? fullTimeSchedule[`${day}_start`]),
            [`${day}_end`]: dayOff ? undefined : (defaults[`${day}_end`] ?? fullTimeSchedule[`${day}_end`]),
        });
    }

    return (
        <>
            {weekdays.map(({key, label}) => {
                const dayOff = isDayOff(key);
                return (
                    <div key={key}>
                        <label>{label}: </label>
                        <input type="time" name={`${key}_start`} disabled={dayOff}
                               value={value[`${key}_start`] ?? ""}
                               onChange={e => setTime(`${key}_start`, e.target.value)}/>
                        <span> - </span>
                        <input type="time" name={`${key}_end`} disabled={dayOff}
                               value={value[`${key}_end`] ?? ""}
                               onChange={e => setTime(`${key}_end`, e.target.value)}/>
                        <label>
                            {" "}<input type="checkbox" name={`${key}_day_off`} checked={dayOff}
                                        onChange={e => setDayOff(key, e.target.checked)}/> Day off
                        </label>
                    </div>
                );
            })}
        </>
    );
}
