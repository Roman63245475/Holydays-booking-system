import { HolidayType, type HolidayAccountView } from "./api/Api";

// dates come from the API as "yyyy-MM-dd" strings (typed as Date by the generated client)
export function day(value?: Date | string | null): string {
    if (!value) return "—";
    return String(value).slice(0, 10);
}

export function hours(value?: number | null): string {
    return `${(value ?? 0).toFixed(2)} h`;
}

// "12.50 h (≈ 1.7 days)" with the employee's day length
export function hoursAndDays(value: number | undefined, dayHours: number | undefined): string {
    if (!dayHours) return hours(value);
    return `${hours(value)} (≈ ${((value ?? 0) / dayHours).toFixed(1)} days)`;
}

export type FlagTone = "warning" | "danger" | "info";

// the status of a balance in plain words (server: AccountStatusService). who = "you" on the employee's own page,
// "employee" for the accountant / system owner
export function flagMessage(account: HolidayAccountView, who: "you" | "employee"): { tone: FlagTone; title: string; text: string } | null {
    const law = account.holiday_type === HolidayType.FerieEfterFerieloven;
    const expires = day(account.usage_period_end);
    const unplanned = hoursAndDays(account.unplanned_hours, account.day_hours);
    const transfer = account.can_transfer
        ? ` or ${who === "you" ? "ask" : "the employee can ask"} to transfer up to ${hours(account.transfer_max_hours)} to the next holiday period (agreed by ${day(account.transfer_deadline)})`
        : "";
    const leftover = law
        ? "the 5th week is paid out, the first 4 weeks go to the holiday fund (handled by payroll)"
        : "they are paid out with the next salary (handled by payroll)";

    switch (account.flag) {
        case "plan_soon":
            return {
                tone: "warning",
                title: `Expires on ${expires}`,
                text: `${unplanned} are not planned yet. ${who === "you" ? "Take them" : "They must be taken"} before ${expires}${transfer}. Hours that are neither taken nor transferred: ${leftover}.`,
            };
        case "expired_unused":
            return {
                tone: "danger",
                title: `Expired on ${expires}`,
                text: (account.available_holiday_hours ?? 0) >= 0
                    ? `${hours(account.available_holiday_hours)} were neither taken nor transferred: ${leftover}.`
                    : `The balance ended with ${hours(account.available_holiday_hours)} of holiday debt, settled by payroll.`,
            };
        case "leaving":
            return {
                tone: "info",
                title: "Final settlement",
                text: `${hours(account.settlement_hours)} left on the last working day (incl. what is still earned until then), settled with the final salary by payroll.`
                    + ((account.pending_hours ?? 0) > 0 ? ` ${hours(account.pending_hours)} are still requested and not counted.` : ""),
            };
        default:
            return null;
    }
}
