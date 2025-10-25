using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Harry.Database
{
    public class DbPacket
    {
        public byte[] Payload { get; set; }
        public string? Flags { get; set; }
        public int Length { get; set; }

        public string SrcAddr { get; set; }
        public string DstAddr { get; set; }
        public int? SrcPort { get; set; }
        public int? DstPort { get; set; }

        public bool IsInbound { get; set; }
        public bool IsSent { get; set; }
        [Key]
        public ulong Id { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class DbLog
    {
        [Key]
        public ulong Id { get; set; }
        public LogLevel Type { get; set; }
        public string? Text { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal,
        Keypresses
    }
}
