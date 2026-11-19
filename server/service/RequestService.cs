using be;
using database;
using LinqToDB;
using LinqToDB.Async;

namespace service;

public class RequestService
{
    private readonly Database db;

    public RequestService(Database db)
    {
        this.db = db;
    }

    public async Task<HolidayRequest> SendRequest(HolidayRequest request)
    {
        

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
