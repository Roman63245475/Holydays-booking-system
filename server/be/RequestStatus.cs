namespace be;

// stored in the db as a number, so the existing values must never change: new statuses are only added at the end
public enum RequestStatus
{
    Accepted = 0,
    Rejected = 1,
    Pending = 2,
    // an approved holiday that was cancelled before it started (an approved cancellation request), its hours are given back
    Cancelled = 3,
    // a pending request the employee took back: withdrawn, or replaced by a request with new dates (Edit)
    Withdrawn = 4
}
