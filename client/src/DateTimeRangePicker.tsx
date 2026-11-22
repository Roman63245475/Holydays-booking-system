
import {useState} from "react";
import {requestApi} from "./api/client";

export interface DateTimeRange {
    startDate: string;
    endDate: string;
}

interface Props {
    accountId: string;
    onChange?: (range: DateTimeRange) => void;
}

export function DateTimeRangePicker({ accountId, onChange }: Props) {
    const [range, setRange] = useState<DateTimeRange>({
        startDate: "2026-11-13",
        endDate: "2026-11-13",
    });

    const start = range.startDate ? new Date(`${range.startDate}T00:00:00`) : new Date(NaN);
    const end = range.endDate ? new Date(`${range.endDate}T00:00:00`) : new Date(NaN);

    const isValid =
        !!range.startDate &&
        !!range.endDate &&
        !isNaN(start.getTime()) &&
        !isNaN(end.getTime()) &&
        end >= start;

    // Count whole selected weekdays: 7.5 hours Monday–Thursday and 7 hours Friday.
    const requestedWorkMinutes = isValid
        ? getWorkingMinutes(start, end)
        : 0;

    const update = (field: keyof DateTimeRange, value: string) => {
        const updated = { ...range, [field]: value };
        setRange(updated);
        onChange?.(updated);
    };

    const hours = Math.floor(requestedWorkMinutes / 60);
    const minutes = requestedWorkMinutes % 60;
    
    async function sendRequest() {
        try {
            await requestApi.create({
                startDate: range.startDate,
                endDate: range.endDate,
                hours: Math.ceil(requestedWorkMinutes / 60),
                account_id: accountId,
            });
            window.alert("Your holiday request was submitted.");
        } catch (error) {
            window.alert(error instanceof Error ? error.message : "Could not submit your request.");
        }
    }
    return (
        <div className="w-full max-w-2xl rounded-xl border p-5 space-y-5">

            <h3 className="text-lg font-semibold">
                Select date & time range
            </h3>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">

                {/* START */}
                <div className="space-y-3">
                    <label className="text-sm font-medium">
                        Start
                    </label>

                    <input
                        type="date"
                        value={range.startDate}
                        onChange={(e) =>
                            update("startDate", e.target.value)
                        }
                        className="w-full rounded-lg border p-3"
                    />

                    
                </div>

                <div className="space-y-3">
                    <label className="text-sm font-medium">
                        End
                    </label>

                    <input
                        type="date"
                        min={range.startDate}
                        value={range.endDate}
                        onChange={(e) =>
                            update("endDate", e.target.value)
                        }
                        className="w-full rounded-lg border p-3"
                    />

                    
                </div>
            </div>

            {!isValid && (
                <p className="text-sm text-red-500">
                    Please select an end date on or after the start date.
                </p>
            )}

            {isValid && (
                <div className="rounded-lg bg-muted p-4">
                    
                    <p className="text-xl font-semibold">
                        <button className="request-button" type="button" onClick={sendRequest}>
                            Send request for {range.startDate} to {range.endDate}
                        </button>
                        
                    </p>
                </div>
            )}

        </div>
    );
}

function getWorkingMinutes(start: Date, end: Date): number {
    let totalMinutes = 0;
    const day = new Date(start.getFullYear(), start.getMonth(), start.getDate());

    while (day <= end) {
        const weekday = day.getDay(); // Sunday = 0, Friday = 5, Saturday = 6
        if (weekday >= 1 && weekday <= 5) {
            totalMinutes += weekday === 5 ? 7 * 60 : 7.5 * 60;
        }
        day.setDate(day.getDate() + 1);
    }

    return Math.round(totalMinutes);
}
