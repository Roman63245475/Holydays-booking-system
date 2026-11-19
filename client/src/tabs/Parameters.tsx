import "../index.css";
import {useEffect, useState} from "react";
import {ApiException, type RuleSetDTO, type ScheduleDTO} from "@/api/Api.ts";
import {ScheduleInputs} from "@/ScheduleInputs.tsx";
import {ruleSetApi} from "@/api/client.ts";

const months = ["January", "February", "March", "April", "May", "June",
    "July", "August", "September", "October", "November", "December"];

// names of the RuleSetDTO fields of a given type, so a field description can only point to a matching field
type FieldsOfType<T> = { [K in keyof RuleSetDTO]-?: NonNullable<RuleSetDTO[K]> extends T ? K : never }[keyof RuleSetDTO];
type NumberField = FieldsOfType<number>;
type BooleanField = FieldsOfType<boolean>;

// description of one row of the form: "label: input". type decides which input is shown
type FieldDef =
    | { type: "number", field: NumberField, label: string, step?: string, min?: string }
    | { type: "month", field: NumberField, label: string }                       // month as a name (September)
    | { type: "checkbox", field: BooleanField, label: string }
    | { type: "dayOfYear", monthField: NumberField, dayField: NumberField, label: string };  // e.g. 31 August

// all rule fields of the form, grouped like in the db.
// to add a new parameter to the form, add one line here
const groups: { legend: string, fields: FieldDef[] }[] = [
    {
        legend: "Weeks 1-5 (Ferieloven)",
        fields: [
            {type: "number", field: "weeks1to5_month_rate", label: "Days earned per month", step: "0.01"},
            {type: "month", field: "weeks1to5_earning_start_month", label: "Earning period starts"},
            {type: "month", field: "weeks1to5_earning_end_month", label: "Earning period ends"},
            {type: "month", field: "weeks1to5_usage_start_month", label: "Usage period starts"},
            {type: "number", field: "weeks1to5_usage_start_year_offset", label: "Usage start, years after earning start"},
            {type: "month", field: "weeks1to5_usage_end_month", label: "Usage period ends"},
            {type: "number", field: "weeks1to5_usage_end_year_offset", label: "Usage end, years after earning start"},
        ],
    },
    {
        legend: "Week 6 (KL 05.11)",
        fields: [
            {type: "checkbox", field: "week6_enabled", label: "6th week enabled"},
            {type: "number", field: "week6_hours_per_month", label: "Hours earned per month (full time)", step: "0.001"},
            {type: "month", field: "week6_earning_start_month", label: "Earning period starts"},
            {type: "month", field: "week6_earning_end_month", label: "Earning period ends"},
            {type: "month", field: "week6_usage_start_month", label: "Usage period starts"},
            {type: "number", field: "week6_usage_start_year_offset", label: "Usage start, years after earning start"},
            {type: "month", field: "week6_usage_end_month", label: "Usage period ends"},
            {type: "number", field: "week6_usage_end_year_offset", label: "Usage end, years after earning start"},
        ],
    },
    {
        legend: "Main holiday and notice",
        fields: [
            {type: "number", field: "main_holiday_length_days", label: "Main holiday length (consecutive days)", min: "1"},
            {type: "month", field: "main_holiday_start_month", label: "Main holiday window starts"},
            {type: "month", field: "main_holiday_end_month", label: "Main holiday window ends"},
            {type: "number", field: "main_holiday_notice_months", label: "Notice for main holiday (months)"},
            {type: "number", field: "other_holiday_notice_months", label: "Notice for other holiday (months)"},
        ],
    },
    {
        legend: "Transfer to the next period",
        fields: [
            {type: "checkbox", field: "transfer_allowed", label: "Transfer allowed"},
            {type: "number", field: "transfer_max_days", label: "Max days that can be transferred"},
            {type: "dayOfYear", monthField: "transfer_weeks1to5_deadline_month", dayField: "transfer_weeks1to5_deadline_day", label: "Deadline for the 5th week"},
            {type: "dayOfYear", monthField: "transfer_week6_deadline_month", dayField: "transfer_week6_deadline_day", label: "Deadline for week 6"},
        ],
    },
    {
        legend: "Holiday debt",
        fields: [
            {type: "number", field: "max_holiday_debt_days", label: "Max holiday debt (days)"},
            {type: "dayOfYear", monthField: "non_negative_balance_month", dayField: "non_negative_balance_day", label: "Balance must be 0 or positive on"},
        ],
    },
];

// 1st of the next month as "YYYY-MM-DD", the default start of new rules
function firstOfNextMonth(): string {
    const now = new Date();
    const next = new Date(now.getFullYear(), now.getMonth() + 1, 1);
    return `${next.getFullYear()}-${String(next.getMonth() + 1).padStart(2, "0")}-01`;
}

export function Parameters() {

    // the rule set shown in the form: starts as the rules that apply now, every input changes one of its fields,
    // and Save sends exactly this object to set_parameters
    const [ruleset, setRuleset] = useState<RuleSetDTO | null>(null);
    // true after a rule was changed, Save is only possible then (saving unchanged rules would just duplicate them)
    const [edited, setEdited] = useState(false);
    const [currentFrom, setCurrentFrom] = useState<string>("");
    // the standard week as loaded, its times come back when "Day off" is unchecked
    const [loadedWeek, setLoadedWeek] = useState<ScheduleDTO | undefined>(undefined);
    const [message, setMessage] = useState<{ text: string, error: boolean } | null>(null);

    useEffect(() => {
        loadRules();
    }, []);

    async function loadRules() {
        try {
            const current = await ruleSetApi.get_parameters();
            setCurrentFrom(String(current.effective_from ?? ""));
            setLoadedWeek(current.standard_week);
            // the new rules start on the 1st of the next month by default, the admin can pick another month.
            // the generated type expects a Date, but the backend sends and reads DateOnly as a "YYYY-MM-DD" string
            setRuleset({...current, effective_from: firstOfNextMonth() as unknown as Date});
            setEdited(false);
        } catch (err) {
            setMessage({text: "Failed to load the current rules", error: true});
            console.error(err);
        }
    }

    // setter for one field of the rule set
    function setField<K extends keyof RuleSetDTO>(field: K, value: RuleSetDTO[K]) {
        setRuleset(prev => prev ? {...prev, [field]: value} : prev);
        setEdited(true);
    }

    // the start month alone is not a change of the rules, so it does not set edited.
    // year and month come from two separate inputs (<input type="month"> is a plain text field in Firefox),
    // so effective_from is always a valid "YYYY-MM-01"
    function setEffectiveFrom(year: number, month: number) {
        const value = `${year}-${String(month).padStart(2, "0")}-01`;
        setRuleset(prev => prev ? {...prev, effective_from: value as unknown as Date} : prev);
    }

    async function handleSubmit(e: React.FormEvent) {
        e.preventDefault();
        if (!ruleset || !edited) return;
        setMessage(null);

        try {
            await ruleSetApi.set_parameters(ruleset);
            setMessage({text: `Saved. The new rules apply from ${ruleset.effective_from}.`, error: false});
            await loadRules();
        } catch (err) {
            // the backend returns the reason (e.g. "The rules can not start before 2027-05-01: ...")
            setMessage({text: err instanceof ApiException ? err.response : "Failed to save the rules", error: true});
            console.error(err);
        }
    }

    function monthOptions() {
        return months.map((name, i) => <option key={name} value={i + 1}>{name}</option>);
    }

    // the input for one field description, chosen by its type
    function renderInput(def: FieldDef, rs: RuleSetDTO) {
        switch (def.type) {
            case "number":
                return <input required type="number" step={def.step ?? "1"} min={def.min ?? "0"}
                              value={rs[def.field] ?? ""}
                              onChange={e => setField(def.field, Number(e.target.value))}/>;
            case "month":
                return <select value={rs[def.field] ?? 1} onChange={e => setField(def.field, Number(e.target.value))}>
                    {monthOptions()}
                </select>;
            case "checkbox":
                return <input type="checkbox" checked={rs[def.field] ?? false}
                              onChange={e => setField(def.field, e.target.checked)}/>;
            case "dayOfYear":
                return <>
                    <input required type="number" min="1" max="31" value={rs[def.dayField] ?? ""}
                           onChange={e => setField(def.dayField, Number(e.target.value))}/>
                    <select value={rs[def.monthField] ?? 1} onChange={e => setField(def.monthField, Number(e.target.value))}>
                        {monthOptions()}
                    </select>
                </>;
        }
    }

    if (!ruleset) {
        return (
            <div className={"flex center-v p-16"}>
                <h1>Holiday Rules</h1>
                {message && <p className={message.error ? "error" : ""}>{message.text}</p>}
            </div>
        );
    }

    // effective_from is kept as "YYYY-MM-01" in the rule set, the two inputs show its year and month
    const [effectiveYear = new Date().getFullYear(), effectiveMonth = 1] = String(ruleset.effective_from ?? "").split("-").map(Number);

    return (
        <div className={"flex center-v p-16"}>
            <h1>Holiday Rules</h1>
            <p>The form shows the rules that apply now (since {currentFrom}). A change is saved as a new rule set
                from the chosen month, the rules of earlier months are never changed.</p>

            <form onSubmit={handleSubmit} className={"parameters-form flex center-v g-10"}>

                <fieldset>
                    <legend>New rules apply from</legend>
                    <select value={effectiveMonth} onChange={e => setEffectiveFrom(effectiveYear, Number(e.target.value))}>
                        {monthOptions()}
                    </select>
                    <input required type="number" min="2000" max="9999" value={effectiveYear}
                           onChange={e => setEffectiveFrom(Number(e.target.value), effectiveMonth)}/>
                </fieldset>

                {groups.map(group => (
                    <fieldset key={group.legend}>
                        <legend>{group.legend}</legend>
                        {group.fields.map(def => (
                            <div key={def.label}>
                                <label>{def.label}: </label>
                                {renderInput(def, ruleset)}
                            </div>
                        ))}
                    </fieldset>
                ))}

                {/* the standard week is nested (standard_week: ScheduleDTO), so it has its own block */}
                <fieldset>
                    <legend>Standard working week (full time)</legend>
                    <ScheduleInputs value={ruleset.standard_week ?? {}} defaults={loadedWeek}
                                    onChange={week => setField("standard_week", week)}/>
                </fieldset>

                {message && <p className={message.error ? "error" : "success"}>{message.text}</p>}
                {!edited && <p>Change a rule to save a new rule set.</p>}
                <button type="submit" disabled={!edited}>Save new rules</button>
            </form>
        </div>
    );
}
