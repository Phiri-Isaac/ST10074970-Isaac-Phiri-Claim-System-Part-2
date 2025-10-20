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
        // GET: /Claim/Submit
        public IActionResult Submit()
        {
            return View(new Claim());
        }

        // POST: /Claim/Submit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Submit(Claim claim, IFormFile? supportingDocument)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View(claim);

                if (supportingDocument != null && supportingDocument.Length > 0)
                {
                    var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
                    var ext = Path.GetExtension(supportingDocument.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(ext))
                    {
                        TempData["Message"] = "Only PDF and Word documents are allowed for supporting documents.";
                        TempData["AlertClass"] = "alert-danger";
                        return View(claim);
                    }

                    var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    if (!Directory.Exists(uploads))
                        Directory.CreateDirectory(uploads);

                    var unique = $"{Guid.NewGuid()}{ext}";
                    var filePath = Path.Combine(uploads, unique);
                    using (var fs = new FileStream(filePath, FileMode.Create))
                    {
                        supportingDocument.CopyTo(fs);
                    }

                    claim.SupportingDocumentPath = "/uploads/" + unique;
                }

                claim.Id = ClaimRepository.NextId++;
                claim.Status = "Pending";
                ClaimRepository.Claims.Add(claim);

                TempData["Message"] = "✅ Claim submitted successfully!";
                TempData["AlertClass"] = "alert-success";

                return RedirectToAction("Manage");
            }
            catch (Exception ex)
            {
                TempData["Message"] = "An error occurred while submitting the claim: " + ex.Message;
                TempData["AlertClass"] = "alert-danger";
                return View(claim);
            }
        }

        // GET: /Claim/Manage
        public IActionResult Manage()
        {
            var model = ClaimRepository.Claims.OrderByDescending(c => c.Id).ToList();
            return View(model);
        }

        // POST: /Claim/Approve
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Approve(int id)
        {
            var claim = ClaimRepository.Claims.FirstOrDefault(c => c.Id == id);
            if (claim != null)
            {
                claim.Status = "Approved";
                TempData["Message"] = $"✅ Claim #{id} approved!";
                TempData["AlertClass"] = "alert-success";
            }
            else
            {
                TempData["Message"] = "Claim not found.";
                TempData["AlertClass"] = "alert-danger";
            }

            return RedirectToAction("Manage");
        }

        // POST: /Claim/Reject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reject(int id)
        {
            var claim = ClaimRepository.Claims.FirstOrDefault(c => c.Id == id);
            if (claim != null)
            {
                claim.Status = "Rejected";
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
    }
}

