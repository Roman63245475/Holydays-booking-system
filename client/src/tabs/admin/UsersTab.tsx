import "../../index.css";
import {useState} from "react";
import {CreateUser} from "@/dialog/CreateUser.tsx";
import {userApi} from "../../api/client.ts"
import {useEffect} from "react";
import {Role, type ClientUser} from "../../api/Api.ts";
import {errorText} from "@/api/errors.ts";
import {RescheduleMenu} from "@/dialog/RescheduleMenu.tsx";
import {LeavingDialog} from "@/dialog/LeavingDialog.tsx";
import {DesConfirmation} from "@/dialog/DesConfirmation.tsx";
import {useNavigate} from "react-router";

function formatDate(value?: Date | string): string {
    if (!value) return "—";
    const date = new Date(value);
    if (isNaN(date.getTime())) return "—";
    return date.toISOString().slice(0, 10); // YYYY-MM-DD
}

function UsersTab(){

    const [activeMenu, setActiveMenu] = useState<string | null>(null);
    const [selectedUser, setSelectedUser] = useState<ClientUser | null>(null);
    const [users, setUsers] = useState<ClientUser[]>([]);
    const navigate = useNavigate();

    useEffect(()=>{
         loadUsers();
    }, [])

    const renderContent = () => {
        switch(activeMenu){
            case 'create':
                return <CreateUser setMenuOpen={setActiveMenu} submitted={loadUsers} user={null} setUser={setSelectedUser}/>
            case 'edit':
                return <CreateUser setMenuOpen={setActiveMenu} submitted={loadUsers} user={selectedUser} setUser={setSelectedUser}/>
            case 'leaving':
                return selectedUser && <LeavingDialog user={selectedUser} onClose={() => setActiveMenu(null)} onSaved={loadUsers}/>
            case 'delete':
                return <DesConfirmation setMenuOpen={setActiveMenu} title={'Confirmation'} description={"Are you sure you want to delete " + selectedUser?.name + "? All their holiday accounts, history and requests will be removed."} onConfirm={deleteUser}/>
            default:
                return null
        }
    }

    async function loadUsers(){
        const response: ClientUser[] = await userApi.get_all_users();
        setUsers(response);
    }

    async function deleteUser(){
        if (!selectedUser?.id) return;
        try {
            await userApi.delete_user(selectedUser.id);
        } catch (err) {
            // e.g. "You can not delete yourself"
            window.alert(errorText(err, "Could not delete the user"));
        }
        setSelectedUser(null);
        await loadUsers();
    }

    return(
        <div className={"flex center-v p-16"}>
            {
                renderContent()
            }
            <h1>User Management</h1>
            <p className={"users-table__hint"}>Double-click a user to see their holiday accounts and history.</p>
            <div className={"table-border"}>
                <div className={"table-tools"}>
                    <button onClick={() => setActiveMenu('create')}>Create User <span className={"icon create-user-icon"} /></button>
                </div>
                <table>
                    <thead>
                    <tr>
                        <th>Name</th>
                        <th>Email</th>
                        <th>Role</th>
                        <th>Start date</th>
                        <th>Last working day</th>
                        <th>Actions</th>
                    </tr>
                    </thead>
                    <tbody>
                        {users.map((user) => (
                            <tr key={user.id}
                                className={"users-table__row"}
                                title={"Double-click to open the user's holiday accounts"}
                                onDoubleClick={(e) => {
                                    // a double-click on Edit / Delete is not a request to open the page
                                    if ((e.target as HTMLElement).closest("button")) return;
                                    navigate(`/admin_page/users/${user.id}`);
                                }}>
                                <td>{user.name}</td>
                                <td>{user.email}</td>
                                <td>{user.role !== undefined ? Role[user.role] : "—"}</td>
                                <td>{formatDate(user.start_date)}</td>
                                <td>{user.end_date ? formatDate(user.end_date) : "—"}</td>
                                <td>
                                    <button onClick={() => {
                                        setSelectedUser(user)
                                        setActiveMenu('edit')
                                    }}>Edit</button>
                                    {user.role !== Role.Admin && (
                                        <button onClick={() => {
                                            setSelectedUser(user)
                                            setActiveMenu('leaving')
                                        }}>Leaving</button>
                                    )}
                                    <button onClick={() => {
                                        setSelectedUser(user)
                                        setActiveMenu('delete')
                                    }}>Delete</button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    )
}

export default UsersTab