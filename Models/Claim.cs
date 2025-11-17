using System;
using System.ComponentModel.DataAnnotations;

namespace ClaimSystem.Models
{
    public class Claim
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Lecturer name is required.")]
        [StringLength(100, ErrorMessage = "Lecturer name cannot exceed 100 characters.")]
        public string LecturerName { get; set; } = string.Empty;

        // NEW — Start and End times
        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        [Required(ErrorMessage = "Hours worked is required.")]
        [Range(0.25, 1000, ErrorMessage = "Hours must be at least 0.25.")]
        public decimal HoursWorked { get; set; }

        [Required(ErrorMessage = "Hourly rate is required.")]
        [Range(0.01, 1000000, ErrorMessage = "Hourly rate must be positive.")]
        public decimal HourlyRate { get; set; }

        // Computed total (your original logic)
        public decimal TotalAmount => Decimal.Round(HoursWorked * HourlyRate, 2);

        [Required]
        public string Status { get; set; } = "Pending";

        // ✅ NEW FIELDS YOU ASKED TO ADD
        public string? VerifiedBy { get; set; }
        public DateTime? VerifiedDate { get; set; }
        public string? HODComments { get; set; }

        public string? SupportingDocumentPath { get; set; }
        public string? Notes { get; set; }

        public DateTime DateSubmitted { get; set; } = DateTime.UtcNow;

        // NEW — AutoCalculate method
        public void AutoCalculate()
        {
            if (EndTime > StartTime)
            {
                HoursWorked = (decimal)(EndTime - StartTime).TotalHours;
            }
            else
            {
                HoursWorked = 0;
            }

            // Force recalculation by accessing computed property
            var _ = TotalAmount;
        }
    }
}
