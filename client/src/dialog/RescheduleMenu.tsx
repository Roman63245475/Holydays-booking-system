interface RescheduleMenuProps {
    setMenuOpen: (value: (((prevState: (string | null)) => (string | null)) | string | null)) => void
}

export function RescheduleMenu({setMenuOpen}: RescheduleMenuProps) {

    return (
        <div onClick={() => setMenuOpen(null)} className={"menu-bg flex center-v"}>
            <div onClick={(e) => e.stopPropagation()} className={"menu"}>
                <div className={"flex center-v min-w-500 shadow p-16"}>
                    <h1>Reschedule</h1>
                    <div className={"flex center-v"}>
                        <p>Name: [name]</p>
                        <p>Account: [week]</p>
                        <p>Current dates: [date] - [date]</p>
                    </div>
                    <label>New from date: <input type={"date"}/></label>
                    <label>New until date: <input type={"date"}/></label>
                    <p>Reason for rescheduling:</p>
                    <textarea></textarea> <br/>
                    <div className={'flex g-20'}>
                        <button className={'request-button'}>Confirm</button>
                        <button className={'btn-destructive-outline'} onClick={() => setMenuOpen(null)}>Cancel</button>
                    </div>
                </div>
            </div>
        </div>
    )
}