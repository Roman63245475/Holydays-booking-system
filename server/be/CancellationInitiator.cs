namespace be;

// who started a cancellation of an approved holiday. stored as a number, values must never change
public enum CancellationInitiator
{
    // the employee asked, the accountant approved or declined it
    Employee = 1,
    // the accountant cancelled the holiday on the employer's decision (force majeure), with a reason
    Employer = 2
}
