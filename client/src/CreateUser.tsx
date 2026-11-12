import {useState} from "react";
import {type CreateUserDTO, Role} from "@/api/Api.ts";
import { userApi } from "./api/client";
// all numeric values of the enum: [1, 2, 3]
const roles = Object.values(Role).filter((v): v is Role => typeof v === "number");

export function CreateUser() {

    const [name, setName] = useState('');
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [salary, setSalary] = useState(0);
    const [role, setRole] = useState<Role>(Role.User);

    async function handleSubmit(e: React.FormEvent) {
        e.preventDefault();
        const user: CreateUserDTO = {name, email, password, role, salary};
        try {
            await userApi.create(user);
        } catch (err) {
            console.error(err);
        }
    }

    return(
        <div className={"menu"}>
            <h1>Create user menu</h1>
            <form onSubmit={handleSubmit}>
                <div>
                    <label>Name: </label>
                    <input type={'text'} name={'name'} placeholder={'Enter name'} onChange={e => setName(e.target.value)}></input>
                </div>
                <div>
                    <label>Email: </label>
                    <input type={'email'} name={'email'} placeholder={'Enter email'} onChange={e => setEmail(e.target.value)}></input>
                </div>
                <div>
                    <label>Password: </label>
                    <input type={'password'} name={'password'} placeholder={'Enter password'} onChange={e => setPassword(e.target.value)}></input>
                </div>
                <div>
                    <label>Salary: </label>
                    <input type="number" step="0.01" name="salary" placeholder={'Enter salary'} onChange={e => setSalary(Number(e.target.value))}></input>
                </div>
                <div>
                    <label>Role: </label>
                    <select value={role} onChange={e => setRole(Number(e.target.value) as Role)}>
                        {roles.map(r => <option key={r} value={r}>{Role[r]}</option>)}
                    </select>
                </div>
                <button type={'submit'}>Submit</button>
            </form>
        </div>
    )
}