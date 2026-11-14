import "../index.css";
import {useState} from "react";
import {CreateUser} from "@/CreateUser.tsx";
import {userApi} from "../api/client"
import {useEffect} from "react";
import {Role, type ClientUser} from "../api/Api.ts";

function formatDate(value?: Date | string): string {
    if (!value) return "—";
    const date = new Date(value);
    if (isNaN(date.getTime())) return "—";
    return date.toISOString().slice(0, 10); // YYYY-MM-DD
}

export function UsersTab(){

    const [createUserOpen, setCreateUserOpen] = useState(false);
    const [users, setUsers] = useState<ClientUser[]>([]);

    useEffect(()=>{
         loadUsers();
    }, [])

    async function loadUsers(){
        const response: ClientUser[] = await userApi.get_all_users();
        setUsers(response);
    }
    return(
        <div className={"flex center-v p-16"}>
            {
                createUserOpen && <CreateUser setMenuOpen={setCreateUserOpen} />
            }
            <h1>User Management</h1>
            <div className={"table-border"}>
                <div className={"table-tools"}>
                    <button onClick={() => setCreateUserOpen(true)}>Create User <span className={"icon create-user-icon"} /></button>
                </div>
                <table>
                    <thead>
                    <tr>
                        <th>Name</th>
                        <th>Email</th>
                        <th>Role</th>
                        <th>Salary</th>
                        <th>Start date</th>
                        {/*<th>Actions</th>*/}
                    </tr>
                    </thead>
                    <tbody>
                        {users.map((user) => (
                            <tr key={user.id}>
                                <td>{user.name}</td>
                                <td>{user.email}</td>
                                <td>{user.role !== undefined ? Role[user.role] : "—"}</td>
                                <td>{user.salary}</td>
                                <td>{formatDate(user.start_date)}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    )
}