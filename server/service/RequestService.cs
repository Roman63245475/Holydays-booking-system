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

    public async Task<HolidayRequest> SendRequest(RequestDTO requestdto)
    {
        if(!await db.law_holidays.AnyAsync(l => l.id == requestdto.account_id && l.user_id == requestdto.user_id && l.active)
           && !await db.agreed_holidays.AnyAsync(a => a.id == requestdto.account_id && a.user_id == requestdto.user_id && a.active)){
            throw new InvalidOperationException("No active holiday account was found for this user");
        }

        var request = new HolidayRequest()
        {
            user_id = requestdto.user_id,
            account_id = requestdto.account_id,
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
        await using var transaction = await db.BeginTransactionAsync();
        var request = await db.HolidayRequest.FirstOrDefaultAsync(r => r.id == id && r.RequestStatus == RequestStatus.Pending);
        
        if (request == null) return;
        
        await db.HolidayRequest.Where(r => r.id == id).Set(r => r.RequestStatus, RequestStatus.Accepted).UpdateAsync();

        var updateLaw = await db.law_holidays.Where(l => l.id == request.account_id)
            .Set(l => l.used_holiday_hours, l => l.used_holiday_hours + request.Hours).UpdateAsync();

        if (updateLaw == 0) {
            await db.agreed_holidays.Where(a => a.id == request.account_id)
                .Set(r => r.used_holiday_hours, r => r.used_holiday_hours + request.Hours).UpdateAsync();
        }
        
        await transaction.CommitAsync();
    }

    // public async Task<RequestDTO[]> GetOngoingRequests(Guid userId)
    // {
    //     var pendingRequests = db.HolidayRequest
    //         .Where(request => request.user_id == userId
    //                           && request.RequestStatus == RequestStatus.Pending);
    //
    //     var lawRequests = await pendingRequests
    //         .Join(db.law_holidays,
    //             request => request.account_id,
    //             account => account.id,
    //             (request, account) => new RequestDTO
    //             {
    //                 user_id = request.user_id,
    //                 StartDate = request.StartDate,
    //                 EndDate = request.EndDate,
    //                 Hours = request.Hours,
    //                 created_at = request.created_at,
    //                 HolidayKind = "law",
    //                 RequestStatus = request.RequestStatus
    //             })
    //         .ToArrayAsync();
    //
    //     var agreedRequests = await pendingRequests
    //         .Join(db.agreed_holidays,
    //             request => request.account_id,
    //             account => account.id,
    //             (request, account) => new RequestDTO
    //             {
    //                 user_id = request.user_id,
    //                 StartDate = request.StartDate,
    //                 EndDate = request.EndDate,
    //                 Hours = request.Hours,
    //                 created_at = request.created_at,
    //                 HolidayKind = "agreed",
    //                 RequestStatus = request.RequestStatus
    //             })
    //         .ToArrayAsync();
    //
    //     return lawRequests.Concat(agreedRequests)
    //         .OrderByDescending(request => request.created_at)
    //         .ToArray();
    // }
}
