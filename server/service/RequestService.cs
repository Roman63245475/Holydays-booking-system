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
        return await db.HolidayRequest.Where(r => r.RequestStatus == RequestStatus.Pending).Join(db.users, r => r.user_id, u => u.id, (r, u) => new HolidayRequestUser {
            Id = r.id,
            Name = u.name,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            HolidayType = r.HolidayType,
            Status = r.RequestStatus
        }).ToListAsync();
    }

    public async Task DeclineRequest(Guid id) {
        await db.HolidayRequest.Where(r => r.id == id).Set(r => r.RequestStatus, RequestStatus.Rejected).UpdateAsync();
    }

    public async Task AcceptRequest(Guid id) {
        await db.HolidayRequest.Where(r => r.id == id).Set(r => r.RequestStatus, RequestStatus.Accepted).UpdateAsync();
    }
}
