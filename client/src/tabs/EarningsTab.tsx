import {type ChangeEvent, useState} from "react";

export function EarningsTab(){

    const [preset, setPreset] = useState("Ferieloven + 6. ferieuge");
    const [wk15Rate, setWk15Rate] = useState<number | string>(12.5);
    const [wk6Rate, setWk6Rate] = useState<number | string>(2.5);

    function handlePresetChange(value: string) {
        setPreset(value);
        if (value === "Ferieloven + 6. ferieuge") {
            setWk15Rate(12.5);
            setWk6Rate(2.5);
        } else if (value === "Ferieloven (minimum)") {
            setWk15Rate(12.5);
            setWk6Rate(0);
        }
    };

    return (
        <div className={"flex center-v p-16 g-10"}>

            <div className={"config-panel"}>
                <h2 className={"config-title"}>Holiday Rules Configuration</h2>
                <div className={"config-controls"}>
                    <div className={"input-group"}>
                        <label className={"input-label"}>Rule Preset:</label>
                        <select value={preset} onChange={(e) => handlePresetChange(e.target.value)}>
                            <option value="Ferieloven + 6. ferieuge">Ferieloven + 6. ferieuge</option>
                            <option value="Ferieloven (minimum)">Ferieloven (minimum)</option>
                            <option value="Custom">Custom</option>
                        </select>
                    </div>

                    <div className={"input-group"}>
                        <label className={"input-label"}>1-5 Wks (%):</label>
                        <input className={"config-input"} type="number" step="0.1" value={wk15Rate} onChange={(e) => {
                            setWk15Rate(e.target.value);
                            setPreset("Custom");
                        }} />
                    </div>

                    <div className={"input-group"}>
                        <label className={"input-label"}>6th Wk (%):</label>
                        <input className={"config-input"} type="number" step="0.1" value={wk6Rate} onChange={(e) => {
                            setWk6Rate(e.target.value);
                            setPreset("Custom");
                        }} />
                    </div>

                    <div className={"input-group"}>
                        <label className={"input-label"}>Effective Date:</label>
                        <input type="date" />
                    </div>

                    <button>Apply Rules</button>
                </div>
            </div>

            <div className={"cards-container"}>
                <div className={"info-card card-blue"}>
                    <h3 className={"card-title"}>Mandatory Liability ({wk15Rate}%)</h3>
                    <p className={"card-value"}>[Wk 1-5 Total] DKK</p>
                    <span className={"card-subtext"}>Weeks 1-5 (Days)</span>
                </div>

                <div className={"info-card card-blue"}>
                    <h3 className={"card-title"}>Contractual Liability ({wk6Rate}%)</h3>
                    <p className={"card-value"}>[Wk 6 Total] DKK</p>
                    <span className={"card-subtext"}>6th Week (Hours)</span>
                </div>

                <div className={"info-card card-green"}>
                    <h3 className={"card-title"}>Total Company Liability</h3>
                    <p className={"card-value card-value-green"}>[Combined Total] DKK</p>
                    <span className={"card-subtext"}>Outstanding Balance</span>
                </div>
            </div>

            <div className={"table-border"}>
                <table>
                    <thead>
                        <tr>
                            <th>Employee ID</th>
                            <th>Wks 1-5 Balance</th>
                            <th>Wk 6 Balance</th>
                            <th>Liability (DKK)</th>
                            <th>Status</th>
                        </tr>
                    </thead>
                </table>
            </div>

        </div>
    );
}