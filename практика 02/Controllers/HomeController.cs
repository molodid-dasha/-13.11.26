using Microsoft.AspNetCore.Mvc;

namespace molodid.Controllers;

public class HomeController : Controller
{
    public IActionResult Error()
    {
        return View();
    }
}
