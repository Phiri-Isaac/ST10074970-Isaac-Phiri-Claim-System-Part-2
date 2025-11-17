using ClaimSystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;

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

        // POST: Submit claim
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Submit(Claim claim, IFormFile? supportingDocument)
        {
            // Server-side base model validation
            if (!ModelState.IsValid)
            {
                TempData["Message"] = "Please correct the errors and try again.";
                TempData["AlertClass"] = "alert-danger";
                return View(claim);
            }

            // Business rules:
            // Default allowed thresholds for direct submission:
            // - Hours <= 12
            // - HourlyRate <= 2000
            // If either threshold exceeded, a supporting document is REQUIRED to allow submission.
            bool exceedsHours = claim.HoursWorked > 12m;
            bool exceedsRate = claim.HourlyRate > 2000m;

            // Validate file upload (if provided) and save
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

                if (!Directory.Exists(_uploadFolder))
                    Directory.CreateDirectory(_uploadFolder);

                var fileName = $"{Guid.NewGuid()}{ext}";
                var savePath = Path.Combine(_uploadFolder, fileName);

                using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    supportingDocument.CopyTo(stream);
                }

                claim.SupportingDocumentPath = "/uploads/" + fileName;
            }

            // If thresholds are exceeded but there's no supporting doc, reject submission.
            if ((exceedsHours || exceedsRate) && string.IsNullOrEmpty(claim.SupportingDocumentPath))
            {
                TempData["Message"] = "Hours or hourly rate exceed allowed limits. Please upload a supporting document to submit.";
                TempData["AlertClass"] = "alert-danger";
                return View(claim);
            }

            // If thresholds exceed very large values, auto-flag for manager review
            // (example thresholds — adjust if needed)
            if (claim.HoursWorked > 40 || claim.HourlyRate > 50000m)
            {
                claim.Status = "Flagged";
                claim.EscalatedToManager = true;
                claim.HODComments = "Automatically flagged for manager review due to extreme values.";
            }
            else
            {
                claim.Status = "Pending";
            }

            // Assign ID and save
            claim.Id = ClaimRepository.NextId++;
            claim.DateSubmitted = DateTime.UtcNow;
            ClaimRepository.Claims.Add(claim);

            TempData["Message"] = $"✅ Claim #{claim.Id} submitted. Total payment: {claim.TotalAmount:C}";
            TempData["AlertClass"] = "alert-success";

            return RedirectToAction("Manage");
        }

        // GET: /Claim/Manage?role=HOD|Coordinator|Manager
        public IActionResult Manage(string? role = "HOD")
        {
            // Keep the presentation identical, but filter/sort depending on role
            var list = ClaimRepository.Claims
                        .OrderByDescending(c => c.DateSubmitted)
                        .ToList();

            ViewData["Role"] = role ?? "HOD";
            return View(list);
        }

        // Approve single claim (used by HOD/Coordinator/Manager buttons)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Approve(int id, string approverRole)
        {
            var claim = ClaimRepository.Claims.FirstOrDefault(c => c.Id == id);
            if (claim != null)
            {
                claim.Status = "Approved";
                claim.VerifiedBy = approverRole;
                claim.VerifiedDate = DateTime.UtcNow;
                claim.LastActionNote = $"{approverRole} approved on {DateTime.UtcNow:yyyy-MM-dd HH:mm}";

                TempData["Message"] = $"✅ Claim #{id} approved by {approverRole}.";
                TempData["AlertClass"] = "alert-success";
            }
            else
            {
                TempData["Message"] = "Claim not found.";
                TempData["AlertClass"] = "alert-danger";
            }
            return RedirectToAction("Manage", new { role = approverRole });
        }

        // Reject single claim
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reject(int id, string reason, string role)
        {
            var claim = ClaimRepository.Claims.FirstOrDefault(c => c.Id == id);
            if (claim != null)
            {
                claim.Status = "Rejected";
                claim.HODComments = reason;
                claim.LastActionNote = $"{role} rejected on {DateTime.UtcNow:yyyy-MM-dd HH:mm}. Reason: {reason}";

                TempData["Message"] = $"⚠ Claim #{id} rejected.";
                TempData["AlertClass"] = "alert-warning";
            }
            else
            {
                TempData["Message"] = "Claim not found.";
                TempData["AlertClass"] = "alert-danger";
            }
            return RedirectToAction("Manage", new { role = role });
        }

        // Standard AutoVerify: small claims auto-approved
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AutoVerify(string role = "HOD")
        {
            int autoApproved = 0;
            int flagged = 0;

            foreach (var claim in ClaimRepository.Claims.Where(c => c.Status == "Pending"))
            {
                // Auto-approve very small claims
                if (claim.HoursWorked <= 8 && claim.HourlyRate <= 1000m)
                {
                    claim.Status = "Approved";
                    claim.VerifiedBy = "AutoVerifier";
                    claim.VerifiedDate = DateTime.UtcNow;
                    claim.LastActionNote = "Auto-verified (small claim)";
                    autoApproved++;
                }
                else if (claim.HoursWorked > 40 || claim.HourlyRate > 50000m)
                {
                    // Very large -> flag for manager
                    claim.Status = "Flagged";
                    claim.HODComments = "Flagged by AutoVerify for manager review.";
                    claim.EscalatedToManager = true;
                    flagged++;
                }
                else
                {
                    // leave as Pending for manual review
                }
            }

            TempData["Message"] = $"AutoVerify completed. Approved: {autoApproved}, Flagged: {flagged}.";
            TempData["AlertClass"] = "alert-info";
            return RedirectToAction("Manage", new { role = role });
        }

        // Coordinator-level automation:
        // CoordinatorAutoApprove: approves moderate claims that are pending and either within coordinator thresholds
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CoordinatorAutoApprove()
        {
            int approved = 0;
            int escalated = 0;

            foreach (var claim in ClaimRepository.Claims.Where(c => c.Status == "Pending"))
            {
                // Coordinator policy:
                // - If Hours <= 20 AND HourlyRate <= 5000 => can auto-approve
                // - If exceeds coordinator thresholds but has supporting document => escalate to Manager for final signoff
                if (claim.HoursWorked <= 20m && claim.HourlyRate <= 5000m)
                {
                    claim.Status = "Approved";
                    claim.VerifiedBy = "CoordinatorAuto";
                    claim.VerifiedDate = DateTime.UtcNow;
                    claim.LastActionNote = "Auto-approved by Coordinator policy";
                    approved++;
                }
                else if (!string.IsNullOrEmpty(claim.SupportingDocumentPath))
                {
                    claim.Status = "Flagged";
                    claim.EscalatedToManager = true;
                    claim.LastActionNote = "Escalated to Manager (supporting doc present)";
                    escalated++;
                }
                else
                {
                    claim.Status = "Flagged";
                    claim.LastActionNote = "Flagged for Manager (no supporting doc)";
                    escalated++;
                }
            }

            TempData["Message"] = $"Coordinator automation: Approved: {approved}, Escalated: {escalated}.";
            TempData["AlertClass"] = "alert-info";
            return RedirectToAction("Manage", new { role = "Coordinator" });
        }

        // Manager-level automation: manager can auto-approve flagged claims IF they have a supporting document
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ManagerProcessFlagged()
        {
            int approved = 0;
            int left = 0;

            foreach (var claim in ClaimRepository.Claims.Where(c => c.Status == "Flagged"))
            {
                if (!string.IsNullOrEmpty(claim.SupportingDocumentPath))
                {
                    claim.Status = "Approved";
                    claim.VerifiedBy = "ManagerAuto";
                    claim.VerifiedDate = DateTime.UtcNow;
                    claim.LastActionNote = "Manager auto-approved because supporting document present";
                    approved++;
                }
                else
                {
                    // still flagged, manager must review manually
                    left++;
                }
            }

            TempData["Message"] = $"Manager processing completed. Approved: {approved}, Still flagged (no doc): {left}.";
            TempData["AlertClass"] = "alert-info";
            return RedirectToAction("Manage", new { role = "Manager" });
        }

        // Simple API endpoint for debugging/testing
        [HttpGet]
        public IActionResult Api_GetClaims()
        {
            return Json(ClaimRepository.Claims);
        }
    }
}