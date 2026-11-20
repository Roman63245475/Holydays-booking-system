export type HolidayKind = "law" | "agreed";

interface HolidayBalanceProps {
    title: string;
    earned?: number;
    used?: number;
    remaining?: number;
    unit: "days" | "hours";
    start_period?: Date | string;
    value: HolidayKind;
    onSelect: (value: HolidayKind) => void;
}

export function HolidayBalance({
    title,
    earned,
    used,
    remaining,
    unit,
    value,
    start_period,
    onSelect,
}: HolidayBalanceProps) {
    const startPeriodDate = start_period ? new Date(start_period) : null;
    const formattedStartPeriod = startPeriodDate && !Number.isNaN(startPeriodDate.getTime())
        ? startPeriodDate.toLocaleDateString()
        : "—";

    return (
        <section className="holiday-balance-card">
            <h2 className="holiday-balance-title">{title}</h2>
            <p className="holiday-balance-caption">Your holiday balance</p>
            <div className="holiday-balance-remaining">
                <span>Available</span>
                <strong>{remaining ?? "—"}</strong>
                <span>{unit}</span>
            </div>
            <div className="holiday-balance-details">
                <p><span>Earning Period Start</span><strong>{formattedStartPeriod}</strong></p>
                <p><span>Earned</span><strong>{earned ?? "—"} {unit}</strong></p>
                <p><span>Used</span><strong>{used ?? "—"} {unit}</strong></p>
            </div>
            <button
                className="holiday-balance-action"
                type="button"
                onClick={() => onSelect(value)}
            >
                Request from this balance
                
            </button>
        </section>
    );
}
