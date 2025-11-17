using System.Collections.Generic;

namespace ClaimSystem.Models
{
    public static class ClaimRepository
    {
        public static List<Claim> Claims { get; set; } = new List<Claim>();
        public static int NextId { get; set; } = 1;
    }
}
