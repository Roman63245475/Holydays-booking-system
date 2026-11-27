import {useEffect, useState} from "react";
import {type ClientUser, type CreateUserDTO, Role, type ScheduleDTO, type UpdateUserDTO} from "@/api/Api.ts";
import {errorText} from "@/api/errors.ts";
import {userApi} from "../api/client.ts";
import {fullTimeSchedule, ScheduleInputs} from "@/ScheduleInputs.tsx";
// all numeric values of the enum: [1, 2, 3]
const roles = Object.values(Role).filter((v): v is Role => typeof v === "number");

// the API sends times as "HH:mm:ss", the time inputs work with "HH:mm"
function toFormSchedule(schedule: ScheduleDTO): ScheduleDTO {
    const result: Record<string, string | null> = {};
    for (const [key, value] of Object.entries(schedule)) {
        result[key] = typeof value === "string" ? value.slice(0, 5) : null;
    }
    return result as ScheduleDTO;
}

interface CreateUserProps {
    submitted: () => Promise<void>,
    setMenuOpen: (value: (((prevState: (string | null)) => (string | null)) | string | null)) => void,
    user: ClientUser | null,
    setUser: (value: (((prevState: (ClientUser | null)) => (ClientUser | null)) | ClientUser | null)) => void,
    // roles of the existing users: there is only one accountant and one system owner account
    existingRoles?: Role[]
}

export function CreateUser({submitted, setMenuOpen, user, setUser, existingRoles = []}: CreateUserProps) {

    const [name, setName] = useState(user?.name ?? '');
    const [email, setEmail] = useState(user?.email ?? '');
    const [password, setPassword] = useState('');
    const [role, setRole] = useState<Role>(user?.role ?? Role.User);
    // prefilled with the full time week, for a part time employee the admin changes times or marks days off
    const [schedule, setSchedule] = useState<ScheduleDTO>(fullTimeSchedule);
    const [error, setError] = useState<string | null>(null);
    const isEdit = user !== null;
    // only employees have a working schedule (and holiday). the accountant and the system owner are role accounts
    const isEmployee = role === Role.User;
    // edit mode: the form is shown once the employee's current schedule is loaded
    const [loading, setLoading] = useState(isEdit && user?.role === Role.User);

    // edit mode: prefill the schedule with the one the employee has now
    useEffect(() => {
        if (!user?.id || user.role !== Role.User) return;
        userApi.get_user_schedule(user.id)
            .then(current => setSchedule(toFormSchedule(current)))
            .catch(err => setError(errorText(err, "Could not load the working schedule")))
            .finally(() => setLoading(false));
    }, [user?.id]);

    function close() {
        setUser(null);
        setMenuOpen(null);
    }

    async function handleSubmit(e: React.FormEvent) {
        e.preventDefault();
        setError(null);
        try {
            if (isEdit && user?.id) {
                // an empty password keeps the old one. a changed schedule starts a new schedule period on the backend
                const changes: UpdateUserDTO = {name, email, password: password || undefined, role,
                    schedule: isEmployee ? schedule : undefined};
                await userApi.update_user(user.id, changes);
            } else {
                const newUser: CreateUserDTO = {name, email, password, role, schedule: isEmployee ? schedule : undefined};
                await userApi.create(newUser);
            }
            await submitted();
            close();
        } catch (err) {
            // the backend returns the validation message (e.g. "Monday: end time must be after start time")
            setError(errorText(err, isEdit ? "Failed to save the user" : "Failed to create user"));
            console.error(err);
        }
    }

    return (
        <div onClick={close} className={"menu-bg flex center-v"}>
            <div onClick={(e) => e.stopPropagation()} className={"menu flex center-v"}>
                <form onSubmit={handleSubmit} className={"w-50 flex center-v g-6"}>
                    <h1>{isEdit ? 'Edit' : 'Create New'} User</h1>
                    <div>
                        <label>Name: </label>
                        <input required={true} type={'text'} name={'name'} placeholder={'Enter name'}
                               onChange={e => setName(e.target.value)} value={name}></input>
                    </div>
                    <div>
                        <label>Email: </label>
                        <input required={true} type={'email'} name={'email'} placeholder={'Enter email'}
                               onChange={e => setEmail(e.target.value)} value={email}></input>
                    </div>
                    <div>
                        <label>Password: </label>
                        <input required={!isEdit} type={'password'} name={'password'}
                               placeholder={isEdit ? 'Leave empty to keep the password' : 'Enter password'}
                               onChange={e => setPassword(e.target.value)} value={password}></input>
                    </div>
                    <div>
                        <label>Role: </label>
                        {/* the role can not be changed later; only one accountant and one system owner account exist */}
                        <select value={role} disabled={isEdit} onChange={e => setRole(Number(e.target.value) as Role)}>
                            {roles.map(r => (
                                <option key={r} value={r} disabled={!isEdit && r !== Role.User && existingRoles.includes(r)}>
                                    {Role[r]}{!isEdit && r !== Role.User && existingRoles.includes(r) ? " (already exists)" : ""}
                                </option>
                            ))}
                        </select>
                    </div>

                    {isEmployee ? (
                        <>
                            <h3>Working schedule</h3>
                            {isEdit && <p>A changed schedule applies from today, the old one stays in the history. The remaining holiday is converted so the number of days stays the same.</p>}
                            {loading ? <p>Loading schedule…</p> : <ScheduleInputs value={schedule} onChange={setSchedule}/>}
                        </>
                    ) : (
                        <p className={"users-table__hint"}>
                            {Role[role]} is a role account (a separate login), not an employee: it has no working schedule and no
                            holiday. The person has their own employee account for that.
                            {isEdit && " To hand the account over to another person, change its name, email and password."}
                        </p>
                    )}

                    {error && <p className={"error"}>{error}</p>}
                    <button type={'submit'} disabled={loading}>{isEdit ? 'Save' : 'Submit'}</button>
                </form>
            </div>
        </div>
    )
}