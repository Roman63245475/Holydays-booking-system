interface DesConfirmationProps {
    setMenuOpen: (value: (((prevState: (string | null)) => (string | null)) | string | null)) => void,
    title: string,
    description: string
}

export function DesConfirmation({setMenuOpen, title, description}: DesConfirmationProps) {

    return (
        <div onClick={() => setMenuOpen(null)} className={"menu-bg flex center-v"}>
            <div onClick={(e) => e.stopPropagation()} className={"menu"}>
                <div className={"flex center-v shadow p-16"}>
                    <h1>{title}</h1>
                    <p>{description}</p>
                    <div className={'flex g-20'}>
                        <button className={'btn-destructive-outline'}>Confirm</button>
                        <button className={'request-button'} onClick={() => setMenuOpen(null)}>Cancel</button>
                    </div>
                </div>
            </div>
        </div>
    )
}