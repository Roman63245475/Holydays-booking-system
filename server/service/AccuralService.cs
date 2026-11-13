using database;

namespace service;

public class AccuralService
{
    private Database db;
    
    public AccuralService(Database db)
    {
        this.db = db;
    }

    public async Task accrue_holidays()
    {
        
    }
}