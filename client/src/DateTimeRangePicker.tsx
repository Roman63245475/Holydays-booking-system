
import { useState } from "react";

export interface DateTimeRange {
    startDate: string;
    startTime: string;
    endDate: string;
    endTime: string;
}

interface Props {
    onChange?: (range: DateTimeRange) => void;
}

export function DateTimeRangePicker({ onChange }: Props) {
    const [range, setRange] = useState<DateTimeRange>({
        startDate: "2026-11-13",
        startTime: "09:00",
        endDate: "2026-11-13",
        endTime: "17:30",
    });

    const start = new Date(
        `${range.startDate}T${range.startTime}:00`
    );

    const end = new Date(
        `${range.endDate}T${range.endTime}:00`
    );

    const isValid =
        !!range.startDate &&
        !!range.startTime &&
        !!range.endDate &&
        !!range.endTime &&
        !isNaN(start.getTime()) &&
        !isNaN(end.getTime()) &&
        end > start;

    const duration = isValid
        ? Math.round((end.getTime() - start.getTime()) / 60000)
        : 0;

    const update = (field: keyof DateTimeRange, value: string) => {
        const updated = { ...range, [field]: value };
        setRange(updated);
        onChange?.(updated);
    };

    const hours = Math.floor(duration / 60);
    const minutes = duration % 60;

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

                    <input
                        type="time"
                        value={range.startTime}
                        onChange={(e) =>
                            update("startTime", e.target.value)
                        }
                        className="w-full rounded-lg border p-3"
                    />
                </div>

                {/* END */}
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

                    <input
                        type="time"
                        value={range.endTime}
                        onChange={(e) =>
                            update("endTime", e.target.value)
                        }
                        className="w-full rounded-lg border p-3"
                    />
                </div>
            </div>

            {!isValid && (
                <p className="text-sm text-red-500">
                    Please select an end date and time
                    after the start.
                </p>
            )}

            {isValid && (
                <div className="rounded-lg bg-muted p-4">
                    <p className="text-sm text-muted-foreground">
                        Selected duration
                    </p>
                    <p className="text-xl font-semibold">
                        {hours}h {minutes}m
                    </p>
                </div>
            )}

        </div>
    );
}
