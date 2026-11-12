import {useState} from "react";
import type {ReactFormState} from "react-dom/client";
import { loginApi } from "./api/client";
import {type ClientUser, type LoginUserDTO, Role} from "./api/Api";
import {useNavigate} from "react-router";
import storage from "@/storage.tsx";
const LogInForm = () => {
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const navigator = useNavigate();

    async function handleSubmit(e: React.FormEvent){
        e.preventDefault();
        const user: LoginUserDTO = {email, password};
        const response = await loginApi.login(user);
        storage.set('user', response);
        redirect(response.role)
    }

    function redirect(role: Role | undefined){
        switch(role){
            case Role.User:
                console.log("Redirect to user's page");
                break;
            case Role.Accountant:
                console.log("Redirect to accountant's page");
                break;
            case Role.Admin:
                navigator('admin_page/')
                break;
            default:
                console.log("Login Failed");
        }
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