export function ActivityTab() {



    return (
        <div className={"flex center-v p-16"}>
            <h1>Accrual History</h1>
            <div className={"table-border"}>
                <table>
                    <thead>
                    <tr>
                        <th>Record ID</th>
                        <th>Holiday Type</th>
                        <th>Accrual Period</th>
                        <th>Accrued Amount</th>
                    </tr>
                    </thead>
                </table>
            </div>
        </div>
    )
}