using Harry.Interception;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using WindivertDotnet;

namespace Harry.Models
{
    public class Packet : IDisposable
    {
        public int Length => Payload.Length;
        public ReadOnlySpan<byte> Payload => ParseResult!.DataSpan;

        public WinDivertPacket OriginalPacket;
        public WinDivertAddress Addr;
        public WinDivertParseResult ParseResult;

        public string RemoteAddress => Inbound ? $"{SrcAddr}:{SrcPort}" : $"{DstAddr}:{DstPort}";
        public IPAddress? SrcAddr;
        public IPAddress? DstAddr;
        public ushort SrcPort;
        public ushort DstPort;

        public bool Inbound;
        public bool Outbound => !Inbound;

        public DateTime CreatedAt;
        public PacketProviderBase SourceProvider;

        public bool Delayed = false;
        public uint? AckNum;
        public uint? SeqNum;
        public TcpFlags Flags;

        public Packet()
        {
        }

        public unsafe Packet(WinDivertPacket packet, WinDivertAddress addr, int start, int finish, PacketProviderBase source)
        {
            CreatedAt = DateTime.Now;
            SourceProvider = source;
            OriginalPacket = packet;
            Addr = addr;
            ParseResult = OriginalPacket.GetParseResult();
            Recalc();
            Inbound = SrcPort >= start && SrcPort <= finish;
        }

        public unsafe void Recalc()
        {
            if (ParseResult is null)
                throw new ArgumentNullException(nameof(ParseResult));

            OriginalPacket.CalcChecksums(Addr);

            SrcAddr = ParseResult.IPV4Header != null
                ? ParseResult.IPV4Header->SrcAddr
                : ParseResult.IPV6Header->SrcAddr;

            DstAddr = ParseResult.IPV4Header != null
                ? ParseResult.IPV4Header->DstAddr
                : ParseResult.IPV6Header->DstAddr;

            if (ParseResult.UdpHeader != null)
            {
                SrcPort = ParseResult.UdpHeader->SrcPort;
                DstPort = ParseResult.UdpHeader->DstPort;
            }

            if (ParseResult.TcpHeader != null)
            {
                ParseTcpFlags();
                DstPort = ParseResult.TcpHeader->DstPort;
                SrcPort = ParseResult.TcpHeader->SrcPort;
            }
        }

        public unsafe void ParseTcpFlags()
        {
            Flags = TcpFlags.None;
            if (ParseResult.TcpHeader->Ack)
            {
                Flags |= TcpFlags.ACK;
                AckNum = ParseResult.TcpHeader->AckNum;
            }
            SeqNum = ParseResult.TcpHeader->SeqNum;
            if (ParseResult.TcpHeader->Fin)
                Flags |= TcpFlags.FIN;
            if (ParseResult.TcpHeader->Psh)
                Flags |= TcpFlags.PSH;
            if (ParseResult.TcpHeader->Rst)
                Flags |= TcpFlags.RST;
            if (ParseResult.TcpHeader->Syn)
                Flags |= TcpFlags.SYN;
            if (ParseResult.TcpHeader->Urg)
                Flags |= TcpFlags.URG;
        }

        public bool IsSaved;
        public bool IsSent;

        public void Dispose()
        {
            OriginalPacket?.Dispose();
            Addr?.Dispose();
        }
    }
}
