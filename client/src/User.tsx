import { DateTimeRangePicker } from "./DateTimeRangePicker.tsx";

export function User() {
    
    function RequestBody(){
        return (
        <div>
            <p>From what date</p>
            <input/>
            <p>From what time(in hours)</p>
            <input/>
            <p>Untill waht date</p>
            <input/>
            <p>Untill what time(hours)</p>
            <input/>
        </div>);
        
    }
    
    return (
        <>
            <h1>Request a holiday</h1>
            <div className="p-8">
                <DateTimeRangePicker
                    onChange={(range) => {
                        console.log("Selected range:", range);
                    }}
                />
            </div>
        </>
    );
}