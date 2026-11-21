import {useState} from "react";
import {ApiException, type CreateUserDTO, Role, type ScheduleDTO} from "@/api/Api.ts";
import {userApi} from "../api/client.ts";
import {fullTimeSchedule, ScheduleInputs} from "@/ScheduleInputs.tsx";
// all numeric values of the enum: [1, 2, 3]
const roles = Object.values(Role).filter((v): v is Role => typeof v === "number");

interface CreateUserProps {
    setMenuOpen: (value: (((prevState: boolean) => boolean) | boolean)) => void,
    submitted: () => Promise<void>
}

export function CreateUser({setMenuOpen, submitted}: CreateUserProps) {

    const [name, setName] = useState('');
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [role, setRole] = useState<Role>(Role.User);
    // prefilled with the full time week, for a part time employee the admin changes times or marks days off
    const [schedule, setSchedule] = useState<ScheduleDTO>(fullTimeSchedule);
    const [error, setError] = useState<string | null>(null);

    async function handleSubmit(e: React.FormEvent) {
        e.preventDefault();
        setError(null);
        const user: CreateUserDTO = {name, email, password, role, schedule};
        try {
            await userApi.create(user);
            await submitted();
            setMenuOpen(false);
        } catch (err) {
            // the backend returns the validation message (e.g. "Monday: end time must be after start time")
            setError(err instanceof ApiException ? err.response : "Failed to create user");
            console.error(err);
        }
    }

    return (
        <div onClick={() => setMenuOpen(false)} className={"menu-bg flex center-v"}>
            <div onClick={(e) => e.stopPropagation()} className={"menu flex center-v"}>
                <form onSubmit={handleSubmit} className={"w-50 flex center-v g-6"}>
                    <h1>Create New User</h1>
                    <div>
                        <label>Name: </label>
                        <input required={true} type={'text'} name={'name'} placeholder={'Enter name'}
                               onChange={e => setName(e.target.value)}></input>
                    </div>
                    <div>
                        <label>Email: </label>
                        <input required={true} type={'email'} name={'email'} placeholder={'Enter email'}
                               onChange={e => setEmail(e.target.value)}></input>
                    </div>
                    <div>
                        <label>Password: </label>
                        <input required={true} type={'password'} name={'password'} placeholder={'Enter password'}
                               onChange={e => setPassword(e.target.value)}></input>
                    </div>
                    <div>
                        <label>Role: </label>
                        <select value={role} onChange={e => setRole(Number(e.target.value) as Role)}>
                            {roles.map(r => <option key={r} value={r}>{Role[r]}</option>)}
                        </select>
                    </div>

                    <h3>Working schedule</h3>
                    <ScheduleInputs value={schedule} onChange={setSchedule}/>

                    {error && <p className={"error"}>{error}</p>}
                    <button type={'submit'}>Submit</button>
                </form>
            </div>
        </div>
    )
}