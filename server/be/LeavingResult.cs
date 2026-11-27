namespace be;

// what happened when the leaving date of an employee was saved
public class LeavingResult
{
    // approved holidays after the last working day, cancelled (their hours are back in the balance)
    public int cancelled_holidays { get; set; }
    // pending holiday requests that go past the last working day, declined
    public int declined_requests { get; set; }
    // pending transfer requests, declined (the balance is settled with the final salary instead)
    public int declined_transfers { get; set; }
}
