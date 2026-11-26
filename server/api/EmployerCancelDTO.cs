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
