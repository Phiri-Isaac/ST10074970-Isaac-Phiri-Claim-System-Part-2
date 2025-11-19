using ClaimSystem.Data;
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
        // ------------------------------------------
        //   DB DISABLED (NO SQL NEEDED NOW)
        // ------------------------------------------


        // NEW in-memory list to temporarily store claims
        private static List<Claim> _claimStore = new List<Claim>();

        private readonly string _uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

        // ORIGINAL CONSTRUCTOR - now disabled
        /*
        public ClaimController(ApplicationDbContext db)
        {
            _db = db;
        }
        */

        // NEW constructor (no DB required)
        public ClaimController()
        {
        }

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
            if (!ModelState.IsValid)
            {
                TempData["Message"] = "Please correct the errors and try again.";
                TempData["AlertClass"] = "alert-danger";
                return View(claim);
            }

            bool exceedsHours = claim.HoursWorked > 12m;
            bool exceedsRate = claim.HourlyRate > 2000m;

            // File validation + save
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

            if ((exceedsHours || exceedsRate) && string.IsNullOrEmpty(claim.SupportingDocumentPath))
            {
                TempData["Message"] = "Hours or hourly rate exceed allowed limits. Please upload a supporting document.";
                TempData["AlertClass"] = "alert-danger";
                return View(claim);
            }

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

            claim.DateSubmitted = DateTime.UtcNow;

            // ----------------------------------------
            // SAVE INTO MEMORY LIST (TEMPORARY)
            // ----------------------------------------
            claim.Id = _claimStore.Count + 1;
            _claimStore.Add(claim);

            TempData["Message"] = $"✅ Claim #{claim.Id} submitted. Total payment: {claim.TotalAmount:C}";
            TempData["AlertClass"] = "alert-success";

            return RedirectToAction("Manage");
        }

        // GET: /Claim/Manage
        public IActionResult Manage(string? role = "HOD")
        {
            var list = _claimStore
                .OrderByDescending(c => c.DateSubmitted)
                .ToList();

            ViewData["Role"] = role ?? "HOD";
            return View(list);
        }

        // APPROVE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Approve(int id, string approverRole)
        {
            var claim = _claimStore.FirstOrDefault(c => c.Id == id);
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

        // REJECT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reject(int id, string reason, string role)
        {
            var claim = _claimStore.FirstOrDefault(c => c.Id == id);
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

        // AUTOVERIFY
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AutoVerify(string role = "HOD")
        {
            int autoApproved = 0;
            int flagged = 0;

            var pendingClaims = _claimStore.Where(c => c.Status == "Pending").ToList();

            foreach (var claim in pendingClaims)
            {
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
                    claim.Status = "Flagged";
                    claim.EscalatedToManager = true;
                    claim.HODComments = "Flagged by AutoVerify for manager review.";
                    flagged++;
                }
            }

            TempData["Message"] = $"AutoVerify completed. Approved: {autoApproved}, Flagged: {flagged}.";
            TempData["AlertClass"] = "alert-info";
            return RedirectToAction("Manage", new { role = role });
        }

        // COORDINATOR AUTO-APPROVE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CoordinatorAutoApprove()
        {
            int approved = 0;
            int escalated = 0;

            var pendingClaims = _claimStore.Where(c => c.Status == "Pending").ToList();

            foreach (var claim in pendingClaims)
            {
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

        // MANAGER AUTO-PROCESS FLAGGED
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ManagerProcessFlagged()
        {
            int approved = 0;
            int left = 0;

            var flaggedClaims = _claimStore.Where(c => c.Status == "Flagged").ToList();

            foreach (var claim in flaggedClaims)
            {
                if (!string.IsNullOrEmpty(claim.SupportingDocumentPath))
                {
                    claim.Status = "Approved";
                    claim.VerifiedBy = "ManagerAuto";
                    claim.VerifiedDate = DateTime.UtcNow;
                    claim.LastActionNote = "Manager auto-approved (supporting document present)";
                    approved++;
                }
                else
                {
                    left++;
                }
            }

            TempData["Message"] = $"Manager processing completed. Approved: {approved}, Still flagged: {left}.";
            TempData["AlertClass"] = "alert-info";
            return RedirectToAction("Manage", new { role = "Manager" });
        }

        // Debug API
        [HttpGet]
        public IActionResult Api_GetClaims()
        {
            return Json(_claimStore.ToList());
        }
    }
}