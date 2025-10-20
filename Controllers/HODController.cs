using ClaimSystem.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;

namespace ClaimSystem.Controllers
{
    public class HODController : Controller
    {
        // GET: /HOD
        public IActionResult Index()
        {
            var model = ClaimRepository.Claims.OrderByDescending(c => c.Id).ToList();
            return View(model);
        }

        // POST: /HOD/Verify
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Verify(int id, string? comments)
        {
            var claim = ClaimRepository.Claims.FirstOrDefault(c => c.Id == id);
            var role = HttpContext.Session.GetString("Role") ?? "HOD";
            if (claim != null)
            {
                claim.Status = "Verified";
                claim.VerifiedBy = role;
                claim.VerifiedDate = DateTime.Now;
                claim.HODComments = comments;
                TempData["Message"] = $"✅ Claim #{id} verified by {role}.";
                TempData["AlertClass"] = "alert-success";
            }
            else
            {
                TempData["Message"] = "Claim not found.";
                TempData["AlertClass"] = "alert-danger";
            }
            return RedirectToAction("Index");
        }

        // POST: /HOD/Return
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Return(int id, string? comments)
        {
            var claim = ClaimRepository.Claims.FirstOrDefault(c => c.Id == id);
            var role = HttpContext.Session.GetString("Role") ?? "HOD";
            if (claim != null)
            {
                claim.Status = "Returned";
                claim.VerifiedBy = role;
                claim.VerifiedDate = DateTime.Now;
                claim.HODComments = comments;
                TempData["Message"] = $"⚠️ Claim #{id} returned for correction by {role}.";
                TempData["AlertClass"] = "alert-warning";
            }
            else
            {
                TempData["Message"] = "Claim not found.";
                TempData["AlertClass"] = "alert-danger";
            }
            return RedirectToAction("Index");
        }
    }
}

