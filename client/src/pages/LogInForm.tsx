import "../index.css";
import {useState} from "react";
import type {ReactFormState} from "react-dom/client";
import { loginApi } from "../api/client.ts";
import {type ClientUser, type LoginUserDTO, Role} from "../api/Api.ts";
import {useNavigate} from "react-router";
import storage from "@/storage.tsx";
import {errorText} from "@/api/errors.ts";
const LogInForm = () => {
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const [error, setError] = useState<string | null>(null);
    const navigator = useNavigate();

    async function handleSubmit(e: React.FormEvent){
        e.preventDefault();
        const user: LoginUserDTO = {email, password};

        try {
            const response = await loginApi.login(user);
            setError(null);
            storage.set('user', response);
            redirect(response.role)
        } catch (err) {
            // wrong email / password, or the account was closed because the employee left the company
            setError(errorText(err, "Could not log in."));
        }
    }

    function redirect(role: Role | undefined){
        switch(role){
            case Role.User:
                console.log("Redirect to user's page");
                navigator('/user_page')
                break;
            case Role.Accountant:
                navigator('/accountant')
                break;
            case Role.Admin:
                navigator('/admin_page')
                break;
            default:
                console.log("Login Failed");
        }
    }

    return (
        <div className={"flex center-v h-100 w-100"}>
            <form onSubmit={handleSubmit} className={"flex center-v pane g-10"}>
                <h1>Authentication</h1>
                <div>
                    <label>Email: </label>
                    <input type={'email'} name={'email'} placeholder={'Enter email'} onChange={e => setEmail(e.target.value)}></input>
                </div>
                <div>
                    <label>Password: </label>
                    <input type={'password'} name={'password'} placeholder={'Enter password'} onChange={e => setPassword(e.target.value)}></input>
                </div>
                {error && <p className={"error"} role="alert">{error}</p>}
                <button type={'submit'}>Log in</button>
            </form>
        </div>
    )
}

export default LogInForm;
