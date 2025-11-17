using System;
using System.ComponentModel.DataAnnotations;

namespace ClaimSystem.Models
{
    public class Claim
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Lecturer name is required.")]
        public string LecturerName { get; set; } = string.Empty;

        [Range(0.01, 10000, ErrorMessage = "Hours worked must be greater than 0")]
        public decimal HoursWorked { get; set; }

        [Range(0.01, 1000000, ErrorMessage = "Hourly rate must be greater than 0")]
        public decimal HourlyRate { get; set; }

        // Computed total
        public decimal TotalAmount => Math.Round(HoursWorked * HourlyRate, 2);

        public string Status { get; set; } = "Pending";

        public string? SupportingDocumentPath { get; set; }

        public string? Notes { get; set; }

        // HOD/Verification fields
        public string? VerifiedBy { get; set; }
        public DateTime? VerifiedDate { get; set; }
        public string? HODComments { get; set; }

        // ✅ Add this property to fix the error
        public DateTime DateSubmitted { get; set; } = DateTime.UtcNow;
    }
}
