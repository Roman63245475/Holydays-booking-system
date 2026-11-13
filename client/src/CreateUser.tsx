interface CreateUserProps {
    setMenuOpen: (value: (((prevState: boolean) => boolean) | boolean)) => void
}

export function CreateUser({ setMenuOpen }: CreateUserProps) {


    return (
        <div onClick={() => setMenuOpen(false)} className={"menu-bg center-v"}>
            <div onClick={(e) => e.stopPropagation()} className={"menu center-v"}>
                <h1>Create User</h1>
                <input/> <br/>
                <input/> <br/>
                <input/> <br/>
                <input/> <br/>
                <input/> <br/>
                <button>Submit</button>
            </div>
        </div>

    )
}