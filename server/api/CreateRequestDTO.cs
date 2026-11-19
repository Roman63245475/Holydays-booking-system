using be;

namespace api;

public class CreateRequestDTO
{
    public string startDate { get; set; } = string.Empty;
    public string endDate { get; set; } = string.Empty;
    public HolidayType holidayType { get; set; }
    public int hours { get; set; }
}
