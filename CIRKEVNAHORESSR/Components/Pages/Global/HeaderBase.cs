using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class HeaderBase : ComponentBase
{
    [Parameter]
    public required string Title { get; set; }
    
    [Parameter]
    public required string PageHeaderStyle { get; set; }
}
