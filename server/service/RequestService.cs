using api;
using be;
using database;
using LinqToDB;
using LinqToDB.Async;

namespace service;

public class RequestService
{
    private readonly Database db;
    private readonly UserService userService;

    public RequestService(Database db, UserService userService)
    {
        this.db = db;
        this.userService = userService;
    }

    public async Task<HolidayRequest> SendRequest(CreateRequestDTO requestdto)
    {
        Guid? accountId = await userService.get_accId(requestdto.user_id, requestdto.HolidayKind);
        if (accountId is null)
            throw new InvalidOperationException("No active holiday account was found for this user and holiday kind.");

        var request = new HolidayRequest()
        {
            user_id = requestdto.user_id,
            account_id = accountId.Value,
            StartDate = requestdto.StartDate,
            EndDate = requestdto.EndDate,
            Hours = requestdto.Hours,
            created_at = requestdto.created_at,
            RequestStatus = requestdto.RequestStatus
        };

        return await db.HolidayRequest.InsertWithOutputAsync(request);
    }

    public async Task<List<HolidayRequestUser>> GetRequests() {
        return await db.HolidayRequest
            .Where(r => r.RequestStatus == RequestStatus.Pending)
            .Join(db.users, r => r.user_id, u => u.id, (r, u) => new {r, u})
            .SelectMany(
                x => db.law_holidays.LeftJoin(l => l.id == x.r.account_id),
                (x, law) => new {x.r, x.u, law}
            )
            .SelectMany(
                x => db.agreed_holidays.LeftJoin(a => a.id == x.r.account_id),
                (x, agreed) => new HolidayRequestUser
                {
                    Id = x.r.id,
                    Name = x.u.name,
                    StartDate = x.r.StartDate,
                    EndDate = x.r.EndDate,
                    HolidayType = x.law != null ? x.law.holiday_type : agreed.holiday_type,
                    Status = x.r.RequestStatus
                }
            ).ToListAsync();
    }

    public async Task DeclineRequest(Guid id) {
        await db.HolidayRequest.Where(r => r.id == id).Set(r => r.RequestStatus, RequestStatus.Rejected).UpdateAsync();
    }

    public async Task AcceptRequest(Guid id) {
        await db.HolidayRequest.Where(r => r.id == id).Set(r => r.RequestStatus, RequestStatus.Accepted).UpdateAsync();
    }
}
