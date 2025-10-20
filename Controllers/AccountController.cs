using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace ClaimSystem.Controllers
{
    public class AccountController : Controller
    {
        // Simple role selector page
        [HttpGet]
        public IActionResult Role()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Role(string role)
        {
            if (string.IsNullOrWhiteSpace(role))
                role = "Lecturer";

            HttpContext.Session.SetString("Role", role);
            TempData["Message"] = $"Role set to {role}";
            TempData["AlertClass"] = "alert-success";
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Remove("Role");
            TempData["Message"] = "Role cleared.";
            TempData["AlertClass"] = "alert-info";
            return RedirectToAction("Index", "Home");
        }
    }
}

