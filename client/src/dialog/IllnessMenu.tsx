import {useState} from "react";

interface IllnessMenuProps {
    setMenuOpen: (value: (((prevState: (string | null)) => (string | null)) | string | null)) => void
}

export function IllnessMenu({setMenuOpen}: IllnessMenuProps) {

    const [chosenOption, setChosenOption] = useState('before')

    return (
        <div onClick={() => setMenuOpen(null)} className={"menu-bg flex center-v"}>
            <div onClick={(e) => e.stopPropagation()} className={"menu"}>
                <div className={"flex center-v shadow p-16 g-6"}>
                    <h1>Register Illness</h1>
                    <p>Name: [name]</p>
                    <div>
                        <label>Illness started BEFORE the holiday begun <input type={"radio"} value={'before'} checked={chosenOption === 'before'} onChange={(e) => setChosenOption(e.target.value)} name={"illnessHoliday"} /></label>
                    </div>
                    <div>
                        <label>Illness occurred DURING the holiday <input type={"radio"} value={'during'} checked={chosenOption === 'during'} onChange={(e) => setChosenOption(e.target.value)} name={"illnessHoliday"}/></label>
                    </div>
                    <div>
                        { chosenOption === 'before' ?
                            <p>Full holiday will be canceled and employee will get a refund.</p>
                            :
                            <div className={"flex center-v g-6"}>
                                <div>
                                    <label>
                                        Sick from:
                                        <input type={"date"} />
                                    </label>
                                </div>
                                <div>
                                    <label>
                                        Sick to:
                                        <input type={"date"} />
                                    </label>
                                </div>
                            </div>
                        }
                    </div>
                    <div className={'flex g-20'}>
                        <button className={'btn-destructive-outline'}>Confirm</button>
                        <button className={'request-button'} onClick={() => setMenuOpen(null)}>Cancel</button>
                    </div>
                </div>
            </div>
        </div>
    )
}