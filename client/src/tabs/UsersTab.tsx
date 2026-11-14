import "../index.css";
import {useState} from "react";
import {CreateUser} from "@/CreateUser.tsx";

export function UsersTab(){

    const [createUserOpen, setCreateUserOpen] = useState(false);

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
                        <th>Status</th>
                        {/*<th>Actions</th>*/}
                    </tr>
                    </thead>
                    <tbody>

                    </tbody>
                </table>
            </div>
        </div>
    )
}