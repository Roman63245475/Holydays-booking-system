import {useState} from "react";

export function BalanceTab(){

    const [week6Active, setWeek6Active] = useState(false)

    return(
        <div className={"flex center-v p-16"}>
            <h1>{week6Active ? 'Week 6 Balances' : 'Week 1-5 Balances'}</h1>
            <div className={"table-border"}>
                <div className={"table-tools"}>
                    <button onClick={() => setWeek6Active(true)}>Week 6</button>
                    <button onClick={() => setWeek6Active(false)}>Week 1-5</button>
                </div>
                <table>
                    <thead>
                    <tr>
                        <th>Name</th>
                        <th>Earned</th>
                        <th>Used</th>
                        <th>Available</th>
                    </tr>
                    </thead>
                </table>
            </div>
        </div>
    )
}