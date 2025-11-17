using ClaimSystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace ClaimSystem.Controllers
{
    public class ClaimController : Controller
    {
        private readonly string _uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

        // GET: /Claim/Submit
        public IActionResult Submit()
        {
            return View(new Claim());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Submit(Claim claim, IFormFile? supportingDocument)
        {
            // Server-side validation
            if (!ModelState.IsValid)
            {
                TempData["Message"] = "Please correct the errors and try again.";
                TempData["AlertClass"] = "alert-danger";
                return View(claim);
            }

            // Ensure upload folder exists
            if (!Directory.Exists(_uploadFolder))
                Directory.CreateDirectory(_uploadFolder);

            // Handle file upload (optional)
            if (supportingDocument != null && supportingDocument.Length > 0)
            {
                var allowed = new[] { ".pdf", ".doc", ".docx" };
                var ext = Path.GetExtension(supportingDocument.FileName).ToLowerInvariant();
                if (!allowed.Contains(ext))
                {
                    TempData["Message"] = "Unsupported file type.";
                    TempData["AlertClass"] = "alert-danger";
                    return View(claim);
                }
                if (supportingDocument.Length > 5 * 1024 * 1024)
                {
                    TempData["Message"] = "File too large (max 5 MB).";
                    TempData["AlertClass"] = "alert-danger";
                    return View(claim);
                }

                var fileName = $"{Guid.NewGuid()}{ext}";
                var savePath = Path.Combine(_uploadFolder, fileName);
                using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    supportingDocument.CopyTo(stream);
                }
                claim.SupportingDocumentPath = "/uploads/" + fileName;
            }

            // Calculate total payment server-side (automation)
            // This uses the computed property on the model, but ensure values are sane.
            if (claim.HoursWorked < 0 || claim.HourlyRate < 0)
            {
                TempData["Message"] = "Hours and hourly rate must be positive.";
                TempData["AlertClass"] = "alert-danger";
                return View(claim);
            }

            // Assign Id and save to repository
            claim.Id = ClaimRepository.NextId++;
            claim.Status = "Pending";
            claim.SubmittedDate = DateTime.UtcNow;
            ClaimRepository.Claims.Add(claim);

            TempData["Message"] = $"✅ Claim #{claim.Id} submitted. Total payment: {claim.TotalPayment:C}";
            TempData["AlertClass"] = "alert-success";

            return RedirectToAction("Manage");
        }

        // GET: /Claim/Manage
        public IActionResult Manage()
        {
            return View(ClaimRepository.Claims.OrderByDescending(c => c.SubmittedDate));
        }

        // Approve
        [HttpPost]
        public IActionResult Approve(int id, string? approver)
        {
            var claim = ClaimRepository.Claims.FirstOrDefault(c => c.Id == id);
            if (claim != null)
            {
                claim.Status = "Approved";
                claim.VerifiedBy = approver ?? "HOD";
                claim.VerifiedDate = DateTime.UtcNow;
                TempData["Message"] = $"✅ Claim #{id} approved.";
                TempData["AlertClass"] = "alert-success";
            }
            else
            {
                TempData["Message"] = "Claim not found.";
                TempData["AlertClass"] = "alert-danger";
            }

            return RedirectToAction("Manage");
        }

        // Reject
        [HttpPost]
        public IActionResult Reject(int id, string? reason)
        {
            var claim = ClaimRepository.Claims.FirstOrDefault(c => c.Id == id);
            if (claim != null)
            {
                claim.Status = "Rejected";
                claim.HODComments = reason;
                TempData["Message"] = $"⚠️ Claim #{id} rejected.";
                TempData["AlertClass"] = "alert-warning";
            }
            else
            {
                TempData["Message"] = "Claim not found.";
                TempData["AlertClass"] = "alert-danger";
            }

            return RedirectToAction("Manage");
        }

        // Automation: Auto-verify claims based on simple predefined rules.
        // This action can be invoked by an admin/HOD to automatically approve or flag claims.
        [HttpPost]
        public IActionResult AutoVerify()
        {
            int autoApproved = 0;
            int flagged = 0;
            // Example rules:
            // - Auto-approve if HoursWorked <= 8 and HourlyRate <= 1000
            // - Flag for manual review if HoursWorked > 40 or HourlyRate > 5000
            foreach (var claim in ClaimRepository.Claims.Where(c => c.Status == "Pending"))
            {
                if (claim.HoursWorked <= 8 && claim.HourlyRate <= 1000)
                {
                    claim.Status = "Approved";
                    claim.VerifiedBy = "AutoVerifier";
                    claim.VerifiedDate = DateTime.UtcNow;
                    autoApproved++;
                }
                else if (claim.HoursWorked > 40 || claim.HourlyRate > 5000)
                {
                    claim.Status = "Flagged";
                    claim.HODComments = "Flagged by AutoVerify for manual review (suspicious hours or rate).";
                    flagged++;
                }
                // else remains pending for manual review
            }

            TempData["Message"] = $"AutoVerify completed. Approved: {autoApproved}, Flagged: {flagged}.";
            TempData["AlertClass"] = "alert-info";
            return RedirectToAction("Manage");
        }

        // Simple API endpoint to return claims (could be used by frontend or reporting)
        [HttpGet]
        public IActionResult Api_GetClaims()
        {
            return Json(ClaimRepository.Claims);
        }
    }
}
