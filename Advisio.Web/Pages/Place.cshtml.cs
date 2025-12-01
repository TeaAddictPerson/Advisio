using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Advisio.Web.Pages;

public class PlaceModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public void OnGet()
    {
    }
}

