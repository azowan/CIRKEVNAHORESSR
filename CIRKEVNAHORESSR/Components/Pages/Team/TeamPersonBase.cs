using Microsoft.AspNetCore.Components;

public class TeamPersonBase : ComponentBase
{
    [Parameter]
    public string? Name { get; set; }

    [Parameter]
    public string? PhotoPath { get; set; }

    [Parameter]
    public string? Description { get; set; }
    
    [Parameter]
    public string? Role { get; set; }
}
