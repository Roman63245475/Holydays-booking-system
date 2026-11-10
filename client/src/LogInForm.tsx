import {useState} from "react";
import type {ReactFormState} from "react-dom/client";
import { api } from "./api/client";
import type { ClientUser, LoginUserDTO } from "./api/Api";
const LogInForm = () => {
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");

    async function handleSubmit(e: React.FormEvent){
        e.preventDefault();
        const user: LoginUserDTO = {email, password};
        const response = await api.login(user);
        console.log(response);
    }
    return (
        <form onSubmit={handleSubmit}>
            <div>
                <label>Email: </label>
                <input type={'email'} name={'email'} placeholder={'Enter email'} onChange={e => setEmail(e.target.value)}></input>
            </div>
            <div>
                <label>Password: </label>
                <input type={'password'} name={'password'} placeholder={'Enter password'} onChange={e => setPassword(e.target.value)}></input>
            </div>
            <button type={'submit'}>Log in</button>
        </form>
    )
}

export default LogInForm;