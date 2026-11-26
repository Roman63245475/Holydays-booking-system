namespace api;

// the accountant cancels an approved holiday on the employer's decision
public class EmployerCancelDTO
{
    // why the holiday is cancelled (required, the employer must record it)
    public string? Reason { get; set; }
    // the accountant confirms: force-majeure-like situation, the employee was consulted,
    // the employee's financial loss is covered by payroll
    public bool Confirmed { get; set; }
}

public class EmployerCancelResult
{
    // true: the employee had asked to cancel this holiday, their request was approved
    public bool EmployeeRequestApproved { get; set; }
}

// the accountant moves an approved holiday to new dates ("yyyy-MM-dd")
public class EmployerRescheduleDTO
{
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    // why the holiday is moved (required, the employer must record it)
    public string? Reason { get; set; }
    // the accountant confirms: force-majeure-like situation, the new dates were agreed with the employee,
    // their financial loss is covered by payroll
    public bool Confirmed { get; set; }
}
