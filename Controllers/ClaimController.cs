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
            // Validate model
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

            // Auto-calculate HoursWorked and TotalAmount
            claim.AutoCalculate();

            // Save in repository
            claim.Id = ClaimRepository.NextId++;
            claim.Status = "Pending";
            claim.DateSubmitted = DateTime.UtcNow;

            ClaimRepository.Claims.Add(claim);

            TempData["Message"] = $"✅ Claim #{claim.Id} submitted. Total payment: {claim.TotalAmount:C}";
            TempData["AlertClass"] = "alert-success";

            return RedirectToAction("Manage");
        }

        // GET: /Claim/Manage
        public IActionResult Manage()
        {
            return View(ClaimRepository.Claims.OrderByDescending(c => c.DateSubmitted));
        }

        // APPROVE CLAIM
        [HttpPost]
        public IActionResult Approve(int id, string? approver = null)
        {
            var claim = ClaimRepository.Claims.FirstOrDefault(c => c.Id == id);

            if (claim != null)
            {
                claim.Status = "Approved";

                // Push 8 — set verified info
                claim.VerifiedBy = approver ?? "HOD";
                claim.VerifiedDate = DateTime.UtcNow;

                TempData["Message"] = $"✅ Claim #{id} approved successfully.";
                TempData["AlertClass"] = "alert-success";
            }
            else
            {
                TempData["Message"] = "Claim not found.";
                TempData["AlertClass"] = "alert-danger";
            }

            return RedirectToAction("Manage");
        }

        // REJECT CLAIM
        [HttpPost]
        public IActionResult Reject(int id, string? reason)
        {
            var claim = ClaimRepository.Claims.FirstOrDefault(c => c.Id == id);

            if (claim != null)
            {
                claim.Status = "Rejected";
                claim.Notes = reason;          // existing field
                claim.HODComments = reason;    // ✅ Push 9 addition

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

        // AUTO VERIFY (safe version)
        [HttpPost]
        public IActionResult AutoVerify()
        {
            int autoApproved = 0;
            int flagged = 0;

            foreach (var claim in ClaimRepository.Claims.Where(c => c.Status == "Pending"))
            {
                // Simple example rules
                if (claim.HoursWorked <= 8 && claim.HourlyRate <= 1000)
                {
                    claim.Status = "Approved";
                    autoApproved++;
                }
                else if (claim.HoursWorked > 40 || claim.HourlyRate > 5000)
                {
                    claim.Status = "Flagged";
                    claim.Notes = "Flagged by AutoVerify for manual review.";
                    flagged++;
                }
            }

            TempData["Message"] = $"AutoVerify complete. Approved: {autoApproved}, Flagged: {flagged}.";
            TempData["AlertClass"] = "alert-info";

            return RedirectToAction("Manage");
        }

        // SIMPLE API ENDPOINT
        [HttpGet]
        public IActionResult Api_GetClaims()
        {
            return Json(ClaimRepository.Claims);
        }
    }
}
