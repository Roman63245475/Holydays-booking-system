import "../../index.css";
import {useState} from "react";
import {CreateUser} from "@/dialog/CreateUser.tsx";
import {userApi} from "../../api/client.ts"
import {useEffect} from "react";
import {Role, type ClientUser} from "../../api/Api.ts";
import {LeavingDialog} from "@/dialog/LeavingDialog.tsx";
import {useNavigate} from "react-router";

function formatDate(value?: Date | string): string {
    if (!value) return "—";
    const date = new Date(value);
    if (isNaN(date.getTime())) return "—";
    return date.toISOString().slice(0, 10); // YYYY-MM-DD
}

// admin: every user, create / edit / leaving. accountant: only employees, and only Edit (their data and working
// schedule). both open an employee's page (accounts, balances, history) with a double-click
function UsersTab({mode = "admin"}: {mode?: "admin" | "accountant"}){
    const isAdmin = mode === "admin";

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
                return <CreateUser setMenuOpen={setActiveMenu} submitted={loadUsers} user={null} setUser={setSelectedUser}
                                   existingRoles={users.map(u => u.role).filter((r): r is Role => r !== undefined)}/>
            case 'edit':
                return <CreateUser setMenuOpen={setActiveMenu} submitted={loadUsers} user={selectedUser} setUser={setSelectedUser}/>
            case 'leaving':
                return selectedUser && <LeavingDialog user={selectedUser} onClose={() => setActiveMenu(null)} onSaved={loadUsers}/>
            default:
                return null
        }
    }

    async function loadUsers(){
        const response: ClientUser[] = await userApi.get_all_users();
        // the accountant only works with employees, the role accounts are the system owner's business
        setUsers(isAdmin ? response : response.filter(u => u.role === Role.User));
    }


    return(
        <div className={"flex center-v p-16"}>
            {
                renderContent()
            }
            <h1>{isAdmin ? "User Management" : "Employees"}</h1>
            <p className={"users-table__hint"}>
                {isAdmin
                    ? "Double-click an employee to see their holiday accounts and history. Users are never deleted (the history must be kept): an employee who leaves the company gets a last working day."
                    : "Double-click an employee to see their holiday accounts, balances and history. Edit changes their data and working schedule."}
            </p>
            <div className={"table-border"}>
                {isAdmin && (
                    <div className={"table-tools"}>
                        <button onClick={() => setActiveMenu('create')}>Create User <span className={"icon create-user-icon"} /></button>
                    </div>
                )}
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
                                className={user.role === Role.User ? "users-table__row" : ""}
                                title={user.role === Role.User ? "Double-click to open the user's holiday accounts" : "Role account, no holiday"}
                                onDoubleClick={(e) => {
                                    // a double-click on a button is not a request to open the page.
                                    // role accounts (accountant, system owner) have no holiday to show
                                    if ((e.target as HTMLElement).closest("button") || user.role !== Role.User) return;
                                    navigate(isAdmin ? `/admin_page/users/${user.id}` : `/accountant/users/${user.id}`);
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
                                    {/* only employees can leave the company, role accounts are handed over (Edit) */}
                                    {isAdmin && user.role === Role.User && (
                                        <button onClick={() => {
                                            setSelectedUser(user)
                                            setActiveMenu('leaving')
                                        }}>Leaving</button>
                                    )}
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