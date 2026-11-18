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
}
