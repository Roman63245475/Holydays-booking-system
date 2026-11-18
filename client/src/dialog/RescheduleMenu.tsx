interface RescheduleMenuProps {
    setMenuOpen: (value: (((prevState: (string | null)) => (string | null)) | string | null)) => void
}

export function RescheduleMenu({setMenuOpen}: RescheduleMenuProps) {

    return (
        <div onClick={() => setMenuOpen(null)} className={"menu-bg flex center-v"}>
            <div onClick={(e) => e.stopPropagation()} className={"menu"}>
                <div className={"flex center-v shadow p-16"}>
                    <h1>Reschedule</h1>
                    <p>Name: [name]</p>
                    <p>Account: [week]</p>
                    <p>Current dates: [date] - [date]</p>
                    <input type={"date"}/>
                    <div className={'flex g-20'}>
                        <button className={'btn-destructive-outline'}>Confirm</button>
                        <button className={'request-button'} onClick={() => setMenuOpen(null)}>Cancel</button>
                    </div>
                </div>
            </div>
        </div>
    )
}