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

    public async Task<RequestDTO[]> GetOngoingRequests(Guid userId)
    {
        var pendingRequests = db.HolidayRequest
            .Where(request => request.user_id == userId
                              && request.RequestStatus == RequestStatus.Pending);

        var lawRequests = await pendingRequests
            .Join(db.law_holidays,
                request => request.account_id,
                account => account.id,
                (request, account) => new RequestDTO
                {
                    user_id = request.user_id,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    Hours = request.Hours,
                    created_at = request.created_at,
                    HolidayKind = "law",
                    RequestStatus = request.RequestStatus
                })
            .ToArrayAsync();

        var agreedRequests = await pendingRequests
            .Join(db.agreed_holidays,
                request => request.account_id,
                account => account.id,
                (request, account) => new RequestDTO
                {
                    user_id = request.user_id,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    Hours = request.Hours,
                    created_at = request.created_at,
                    HolidayKind = "agreed",
                    RequestStatus = request.RequestStatus
                })
            .ToArrayAsync();

        return lawRequests.Concat(agreedRequests)
            .OrderByDescending(request => request.created_at)
            .ToArray();
    }
}
