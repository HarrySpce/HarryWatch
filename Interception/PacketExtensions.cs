using Microsoft.EntityFrameworkCore.Diagnostics;
using Harry.Database;
using Harry.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using WindivertDotnet;

namespace Harry.Interception.Modules
{
    public static class PacketExtensions
    {
        static int[] InReconnect = new int[] { 58 };
        static int[] Reconnect = new int[] { 1300, 100, 315, 305, 306, 307, 308, 309, 310 };
        static int[] PVE_Reconnect = new int[] { 42, 128, 136 };
        static int[] PVE_Service = new int[] { 36, 42, 43, 51, 57, 64, 128, 136, 233 };
        const int PVE_ReconnectRequestLength = 1166;


        public static bool IsNewPlayerConnection(this IEnumerable<Packet> packets)
        {
            if (packets.Count() < 5)
                return packets.All(x => Reconnect.Contains(x.Length) || InReconnect.Contains(x.Length));

            var flag = packets.Take(4).All(x => Reconnect.Contains(x.Length) || InReconnect.Contains(x.Length));
            var flag2 = packets.Skip(4).All(x => !x.IsSent);

            return flag && flag2;
        }
        public static bool IsReadonlyPlayerConnection(this IEnumerable<Packet> packets)
        {
            try
            {
                return packets.TakeLast(50).All(x => x.Inbound && x.Length < 100);
            }
            catch (Exception)
            {
                return false;
            }
        }
        public static bool IsOutboundShot(this Packet p) => p.Outbound && p.Length <= 1235 && p.Length >= 1205; //(p.Length == 1211 || p.Length == 1227 || p.Length == 1237);
        public static bool IsInboundShot(this Packet p) => p.Inbound && p.Length <= 1235 && p.Length >= 1205;
        public static bool IsPveReconnectRequest(this Packet p) => PVE_Reconnect.Contains(p.Length);
        public static bool IsPveQuestionableReconnectRequest(this Packet p) => p.Outbound && p.Length == PVE_ReconnectRequestLength;
        public static bool IsPveService(this Packet p) => PVE_Service.Contains(p.Length);

        public static uint? GetPveId(this Packet packet)
        {
            try
            {
                var Payload = packet.Payload;
                if (Payload.Length < 4)
                    return null;
                return BitConverter.ToUInt32(new byte[] { Payload[3], Payload[2], Payload[1], Payload[0] }, 0);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
            return null;
        }
        public static uint GetPveId(this DbPacket packet)
        {
            try
            {
                var Payload = packet.Payload;
                if (Payload.Length < 4)
                    return 0;
                return BitConverter.ToUInt32(new byte[] { Payload[3], Payload[2], Payload[1], Payload[0] }, 0);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
            return 0;
        }

        public static unsafe Packet BuildSameDirection(this Packet p)
        {
            var id = p.SourceProvider.Connections[p.RemoteAddress].Last(x => x.Inbound == p.Inbound).ParseResult.IPV4Header->Id;
            
            var ipHeader = new IPV4Header
            {
                TTL = 128,
                Version =  IPVersion.V4,
                DstAddr = p.ParseResult.IPV4Header->DstAddr,
                SrcAddr = p.ParseResult.IPV4Header->SrcAddr,
                FragmentFlags = p.ParseResult.IPV4Header->FragmentFlags,
                FragOff0 = p.ParseResult.IPV4Header->FragOff0,
                Protocol = ProtocolType.Tcp,
                HdrLength = (byte)(sizeof(IPV4Header) / 4),
                Length = (ushort)(sizeof(IPV4Header) + sizeof(TcpHeader)),
                Id = ++id,
            };

            var packet = new WinDivertPacket(ipHeader.Length);
            var router = new WinDivertRouter(p.ParseResult.IPV4Header->DstAddr, p.ParseResult.IPV4Header->SrcAddr);
            var addr = router.CreateAddress();
            var writer = packet.GetWriter();
            writer.Write(ipHeader);

            if (p.ParseResult.TcpHeader is not null)
            {
                var tcpHeader = new TcpHeader
                {
                    Ack = false,
                    SrcPort = p.ParseResult.TcpHeader->SrcPort,
                    DstPort = p.ParseResult.TcpHeader->DstPort,
                    SeqNum = 0,
                    AckNum = 0,
                    Window = p.ParseResult.TcpHeader->Window,
                    HdrLength = (byte)(sizeof(TcpHeader) / 4),
                };
                writer.Write(tcpHeader);
            }

            if (p.ParseResult.UdpHeader is not null)
            {
                var udpHeader = new UdpHeader
                {
                    DstPort = p.ParseResult.UdpHeader->DstPort,
                    SrcPort = p.ParseResult.UdpHeader->SrcPort,
                    Length = (byte)(sizeof(UdpHeader) / 4),
                };
                writer.Write(udpHeader);
            }

            packet.CalcChecksums(addr);
            return new Packet(packet, addr, p.SourceProvider.PortRangeStart, p.SourceProvider.PortRangeEnd, p.SourceProvider)
            {
                Inbound = p.Inbound,
                IsSent = false,
                IsSaved = false,
            };
        }
        public static unsafe Packet Clone(this Packet p)
        {
            var addr = p.Addr.Clone();
            var pack = p.OriginalPacket.Clone();
            return new Packet(pack, addr, p.SourceProvider.PortRangeStart, p.SourceProvider.PortRangeEnd, p.SourceProvider)
            {
                Inbound = p.Inbound,
                CreatedAt = p.CreatedAt,
                IsSent = false,
                IsSaved = false
            };
        }
        public static Packet ClearTcpFlags(this Packet p)
        {
            unsafe
            {
                p.ParseResult.TcpHeader->Rst = false;
                p.ParseResult.TcpHeader->Psh = false;
                p.ParseResult.TcpHeader->Syn = false;
                p.ParseResult.TcpHeader->Urg = false;
                p.ParseResult.TcpHeader->Fin = false;
                p.ParseResult.TcpHeader->Ack = false;
                p.ParseResult.TcpHeader->AckNum = 0;
            }
            return p;
        }


        public static string BuildTcpFlagsString(this Packet p)
        {
            var flags = p.Flags;
            var sb = new StringBuilder();
            if (p.SeqNum.HasValue) 
                sb.Append($"SEQ {p.SeqNum}.");
            if (flags.HasFlag(TcpFlags.ACK))
                sb.Append($"ACK {p.AckNum}.");
            if (flags.HasFlag(TcpFlags.PSH))
                sb.Append("PSH.");
            if (flags.HasFlag(TcpFlags.FIN))
                sb.Append("FIN.");
            if (flags.HasFlag(TcpFlags.SYN))
                sb.Append("SYN.");
            if (flags.HasFlag(TcpFlags.URG))
                sb.Append("URG.");
            if (flags.HasFlag(TcpFlags.RST))
                sb.Append("RST.");
            return sb.Length > 0 ? sb.ToString().Replace('.', ' ').Trim() : null;
        }
    }
}
