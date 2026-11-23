
import {useState} from "react";
import {requestApi} from "./api/client";
import {ApiException} from "./api/Api";

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
        startDate: "",
        endDate: "",
    });

    const start = range.startDate ? new Date(`${range.startDate}T00:00:00`) : new Date(NaN);
    const end = range.endDate ? new Date(`${range.endDate}T00:00:00`) : new Date(NaN);

    const isValid =
        !!range.startDate &&
        !!range.endDate &&
        !isNaN(start.getTime()) &&
        !isNaN(end.getTime()) &&
        end >= start;

    const update = (field: keyof DateTimeRange, value: string) => {
        const updated = { ...range, [field]: value };
        setRange(updated);
        onChange?.(updated);
    };

    // the server calculates the hours from the employee's schedule, public holidays and company days off
    // and checks the balance, so only the dates are sent
    async function sendRequest() {
        try {
            const created = await requestApi.create({
                startDate: range.startDate,
                endDate: range.endDate,
                account_id: accountId,
            });
            window.alert(`Your holiday request was submitted (${created.hours?.toFixed(2)} hours).`);
        } catch (error) {
            // the backend returns the reason (e.g. "Not enough holiday: the request costs 37.00 h, ...")
            window.alert(error instanceof ApiException ? error.response : "Could not submit your request.");
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
