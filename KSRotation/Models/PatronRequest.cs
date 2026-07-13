// Last Edit: Jun 25, 2026 13:29 - Created PatronRequest model to store incoming requests from the web portal.
using System;

namespace KSRotation.Models
{
    public class PatronRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Song { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
