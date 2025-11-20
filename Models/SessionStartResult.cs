// 📄 Models/SessionStartResult.cs
using System;

namespace Steam_Nexus_API.Models
{
    public class SessionStartResult
    {
        public bool Success { get; set; }
        public Guid SessionId { get; set; }
        public string Status { get; set; } // "SUCCESS", "REQUIRES_2FA", "FAILURE"
        public string Message { get; set; }
    }
}