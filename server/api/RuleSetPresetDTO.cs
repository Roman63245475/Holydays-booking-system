namespace api;

// one ready-made rule set (RuleSetPresets) with all its values, to fill the admin's rules form
public class RuleSetPresetDTO
{
    public string key { get; set; } = "";
    public string name { get; set; } = "";
    public string description { get; set; } = "";
    public RuleSetDTO rules { get; set; } = new RuleSetDTO();
}
