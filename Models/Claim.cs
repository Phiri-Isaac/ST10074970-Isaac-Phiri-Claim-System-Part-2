using System;
using System.ComponentModel.DataAnnotations;

namespace ClaimSystem.Models
{
    public class Claim
    {
        public int Id { get; set; }

        // --- Basic Claim Info ---
        [Required(ErrorMessage = "Lecturer name is required.")]
        public string LecturerName { get; set; } = string.Empty;

        [Range(0.01, 100000, ErrorMessage = "Hours worked must be greater than 0")]
        public decimal HoursWorked { get; set; }

        [Range(0.01, 1000000, ErrorMessage = "Hourly rate must be greater than 0")]
        public decimal HourlyRate { get; set; }

        // --- Total Calculation ---
        public decimal TotalAmount => Math.Round(HoursWorked * HourlyRate, 2);

        // Backwards compatibility for old pages expecting "TotalPayment"
        public decimal TotalPayment => TotalAmount;

        // --- File Upload ---
        public string? SupportingDocumentPath { get; set; }

        // Optional notes by lecturer
        public string? Notes { get; set; }

        // --- Submission Metadata ---
        public DateTime DateSubmitted { get; set; } = DateTime.UtcNow;

        // --- Status Tracking ---
        public string Status { get; set; } = "Pending";

        // --- HOD & Approval Workflow ---
        public string? VerifiedBy { get; set; }
        public DateTime? VerifiedDate { get; set; }
        public string? HODComments { get; set; }

        // Last action description for audit logs
        public string? LastActionNote { get; set; }

        // --- Automation Flags ---
        public bool IsAutoSafe { get; set; } = false;                // Normal claims auto-approved
        public bool RequiresManagerApproval { get; set; } = false;   // High-hour/high-rate claims
        public bool EscalatedToManager { get; set; } = false;        // Sent to manager
    }
}