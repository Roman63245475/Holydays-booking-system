import {useState} from "react";
import {CreateUser} from "@/CreateUser.tsx";

export function UsersTab(){

    const [createUserOpen, setCreateUserOpen] = useState(false);

    return(
        <div className="center-v">
            {
                createUserOpen && <CreateUser />
            }
            <h1>Users</h1>
            <button onClick={() => setCreateUserOpen(true)}>Create User</button>
            <table>
                <thead>
                    <tr>
                        <th>Name</th>
                        <th>Email</th>
                        <th>Role</th>
                        <th>Schedule</th>
                        <th>Status</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody>

                </tbody>
            </table>
        </div>
    )
}