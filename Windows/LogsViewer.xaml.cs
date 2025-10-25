using Harry.Controls;
using Harry.Database;
using Harry.Interception.Modules;
using Harry.Utility;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Harry.Windows
{
    public partial class LogsViewer : Window
    {
        HashSet<LogLevel> TagsFilter = new()
        {
            LogLevel.Info,
            LogLevel.Warning,
            LogLevel.Fatal,
            LogLevel.Error,
            LogLevel.Keypresses
        };

        DateTime AfterDateFilter = DateTime.MinValue;
        DateTime BeforeDateFilter = DateTime.MaxValue;

        public ObservableCollection<LogObject> LogsContainer { get; set; }
        public ObservableCollection<ConnectionModel> ConnectionsContainer { get; set; }

        // Initialization
        public LogsViewer()
        {
            InitializeComponent();
            DataContext = this;
            LogsContainer = new ObservableCollection<LogObject>();
            ConnectionsContainer = new ObservableCollection<ConnectionModel>();
            Loaded += WindowLoaded;
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            Info.FixInitState(true);
            Warning.FixInitState(true);
            Error.FixInitState(true);
            Fatal.FixInitState(true);
            Debug.FixInitState(true);

            PacketsFilters.Visibility = Visibility.Collapsed;
            Packets.FixInitState(false);

            InboundFilter.FixInitState(true);
            OutboundFilter.FixInitState(true);
            BlockedFilter.FixInitState(true);

            // TODO: Change
            AfterDateFilter = MainWindow.Instance.AppStart; // DateTime.MinValue;//
            AfterDateFilter_TextBox.Text = AfterDateFilter.ToString("dd/MM/yy HH:mm:ss");
            BeforeDateFilter_TextBox.Text = AfterDateFilter.AddMinutes(50).ToString("dd/MM/yy HH:mm:ss");

            TagsFilter.Add(LogLevel.Debug);

            Refresh(null, null);
        }






        // Adding content

        // ADD ITEMS FROM START IF SCROLL UP
        private void Refresh(object sender, RoutedEventArgs e)
        {
            DateTime.TryParse(AfterDateFilter_TextBox.Text, out AfterDateFilter);
            if (string.IsNullOrEmpty(AfterDateFilter_TextBox.Text))
            {
                AfterDateFilter = MainWindow.Instance.AppStart;
                AfterDateFilter_TextBox.Text = AfterDateFilter.ToString("dd/MM/yy HH:mm:ss");
            }
                
            DateTime.TryParse(BeforeDateFilter_TextBox.Text, out BeforeDateFilter);

            var packet = Packets.Checked;
            var inb = InboundFilter.Checked;
            var outb = OutboundFilter.Checked;
            var block = BlockedFilter.Checked;

            var start = AfterDateFilter;
            var finish = BeforeDateFilter;
            var len = 0;
            var maxlen = 0;
            int.TryParse(Length.Text, out len);
            int.TryParse(MaxLength.Text, out maxlen);
            if (maxlen == 0)
                maxlen = int.MaxValue;

            var conns = Connections.SelectedItems.OfType<ConnectionModel>().Cast<ConnectionModel>();
            
            var iterations = 30;
            var timeDelta = (finish - start) / 30;

            void loadPart(int iteration = 1)
            {
                using var db = new HarryDBC();
                var result = new List<LogObject>();
                var partialFinish = start + timeDelta * iteration;
                var partialStart = start + timeDelta * (iteration - 1);
                if (packet)
                {
                    List<LogObject> packets;
                    if (conns.Count() == 0)
                    {
                        packets = db.Packets
                            .Where(x => x.CreatedAt >= partialStart && x.CreatedAt <= partialFinish && x.Length >= len && x.Length <= maxlen && (x.IsSent || block) && (x.IsInbound && inb || !x.IsInbound && outb))
                            .OrderByDescending(x => x.CreatedAt).Select(x => new PacketModel(x) as LogObject).ToList();
                    }
                    else
                    {
                        var targets = conns.Select(x => x.Address).Distinct().ToList();
                        var addrs = targets.Select(x => x.Split(':')[0]).ToList();
                        var ports = targets.Select(x => int.Parse(x.Split(':')[1])).Distinct().ToList();
                        packets = db.Packets
                            .Where(x => x.CreatedAt >= partialStart && x.CreatedAt <= partialFinish && x.Length >= len && x.Length <= maxlen && (x.IsSent || block) && (x.IsInbound && inb || !x.IsInbound && outb) && (ports.Contains(x.DstPort.Value) || ports.Contains(x.SrcPort.Value)))
                            .OrderByDescending(x => x.CreatedAt).ToList().Where(x => addrs.Contains(x.DstAddr) || addrs.Contains(x.SrcAddr)).Select(x => new PacketModel(x) as LogObject).ToList();
                    }

                    Logger.Debug($"Loaded {packets.Count} packets");
                    result.AddRange(packets);
                }

                result.AddRange(db.Log
                    .Where(x =>
                        x.CreatedAt >= partialStart && x.CreatedAt <= partialFinish &&
                        TagsFilter.Contains(x.Type))
                    .Select(x => new LogModel(x) as LogObject).ToList());

                result = result.OrderBy(x => x.Date).ToList();
                result.ForEach(x => LogsContainer.Add(x));
                result.Clear();
                if (iteration < iterations)
                    Dispatcher.BeginInvoke(() => loadPart(++iteration));
                else if (iteration == iterations)
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (packet)
                        {
                            ConnectionsContainer.Clear();

                            var packets = LogsContainer.OfType<PacketModel>().Cast<PacketModel>().ToList();
                            var targets = packets.Where(x => !x.Direction).Select(x => x.Target).Distinct().OrderBy(x => x).ToList();
                            int precision = 40;
                            var iteration = (finish - start) / precision;

                            foreach (var target in targets)
                            {
                                var t_packets = packets.Where(x => x.Target == target || x.Source == target);
                                var data = new List<int>(); // for linechart
                                var download = new List<int>();
                                var upload = new List<int>();
                                for (var i = start; i < finish; i += iteration)
                                {
                                    var part = t_packets.Where(x => x.Date >= i && x.Date < i + iteration);
                                    if (part.Any())
                                    {
                                        var dw = part.Where(x => x.Direction);
                                        var up = part.Where(x => !x.Direction);
                                        download.Add(dw.Any() ? dw.Sum(x => x.Bytes.Length / 2) : 0);
                                        upload.Add(up.Any() ? up.Sum(x => x.Bytes.Length / 2) : 0);
                                        data.Add(part.Sum(x => x.Bytes.Length / 2));
                                    }
                                    else
                                    {
                                        download.Add(0);
                                        upload.Add(0);
                                        data.Add(0);
                                    }
                                }

                                string info = $"In:{download.Sum().BytesLenghtToString()} Out:{upload.Sum().BytesLenghtToString()}";
                                // TODO: add player tags (not sure tho)

                                //var plot = new PlotModel();
                                //var series = new LineSeries();
                                //for (int i = 0; i < data.Count; i++)
                                //    series.Points.Add(new DataPoint(i, data[i]));
                                //plot.Series.Add(series);

                                ConnectionsContainer.Add(new ConnectionModel()
                                {
                                    Address = target,
                                    Info = info,
                                    //Chart = plot
                                });
                            }
                        }
                    });
            }

            LogsContainer.Clear();
            Dispatcher.BeginInvoke(() => loadPart());
        }






        private void ExitButtonClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Packets_CheckboxChecked(object sender, RoutedEventArgs e)
        {
            PacketsFilters.Visibility = Packets.Checked ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LogCheckboxChecked(object sender, RoutedEventArgs e)
        {
            var check = sender as Harry.Controls.FilterCheckbox;
            var type = (LogLevel)Enum.Parse(typeof(LogLevel), check.Name);
            if (check.Checked)
            {
                TagsFilter.Add(type);
                Refresh(null, null);
            }
            else
            {
                TagsFilter.Remove(type);
                foreach (var l in LogsContainer.Where(x => x is LogModel l && l.Type == type).ToArray())
                    LogsContainer.Remove(l);
            }
        }

        private void InboundFilter_CheckboxChecked(object sender, RoutedEventArgs e)
        {

        }

        private void OutboundFilter_CheckboxChecked(object sender, RoutedEventArgs e)
        {

        }

        private void ConnectionsSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }






        public class ConnectionModel
        {
            public string Address { get; set; }
            public string Info { get; set; }
        }

        public class LogObject
        {
            public DateTime Date { get; set; }
        }

        public class LogModel : LogObject
        {
            public LogModel() {}
            public LogModel(DbLog log)
            {
                Date = log.CreatedAt;
                Text = log.Text;
                Type = log.Type;
                Id = log.Id;
            }
            public string Text { get; set; }
            public LogLevel Type { get; set; }
            public ulong Id { get; set; }
            public Brush TypeForeground
            {
                get
                {
                    return Type switch
                    {
                        LogLevel.Debug => new SolidColorBrush(Color.FromArgb(0xff, 0xe9, 0xec, 0x6b)),
                        LogLevel.Info => new SolidColorBrush(Color.FromArgb(0xff, 0xe8, 0xcd, 0xcf)),
                        LogLevel.Warning => new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0x6d, 0x68)),
                        LogLevel.Error => new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0x6d, 0x68)),
                        LogLevel.Fatal => new SolidColorBrush(Color.FromArgb(0xff, 0xf9, 0x00, 0x46)),
                        _ => Application.Current.FindResource("AccentColor") as SolidColorBrush ?? Brushes.White
                    };
                }
            }
            public Brush TextForeground
            {
                get
                {
                    return Type switch
                    {
                        LogLevel.Debug => new SolidColorBrush(Color.FromArgb(0xff, 0xe9, 0xec, 0x6b)),
                        LogLevel.Warning => new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0x6d, 0x68)),
                        LogLevel.Error => new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0x6d, 0x68)),
                        LogLevel.Fatal => new SolidColorBrush(Color.FromArgb(0xff, 0xf9, 0x00, 0x46)),
                        _ => Application.Current.FindResource("AccentColor") as SolidColorBrush ?? Brushes.White
                    };
                }
            }
        }
        public class PacketModel : LogObject
        {
            public PacketModel() {}
            public PacketModel(DbPacket packet)
            {
                Date = packet.CreatedAt;
                Bytes = packet.Payload.ToArray();
                Length = packet.Payload.Length;
                Source = $"{packet.SrcAddr}:{packet.SrcPort}";
                Target = $"{packet.DstAddr}:{packet.DstPort}";
                Type = packet.IsSent;
                Direction = packet.IsInbound;

                if (packet.DstPort == 3074 || packet.SrcPort == 3074 &&
                    packet.Payload.Length > 2 && packet.Payload.Take(2).All(x => x == 0x00))
                {
                    PvePacket = Visibility.Visible;
                    PveId = packet.GetPveId().ToString();
                }
                else if (packet.Flags is not null)
                {
                    PvePacket = Visibility.Visible;
                    PveId = packet.Flags;
                }
            }
            public byte[] Bytes { get; set; }
            public int Length { get; set; }

            public string Source { get; set; }
            public string Target { get; set; }
            public string RemoteAddr =>  Direction ? Source : Target;

            public bool Direction { get; set; }
            public bool Type { get; set; }

            public Visibility PvePacket { get; set; } = Visibility.Collapsed;
            public string PveId { get; set; } = "0";

            public Brush TypeForeground => Type ? new SolidColorBrush(Color.FromArgb(0xff, 0xA9, 0xEC, 0x6B)) : new SolidColorBrush(Color.FromArgb(0xff, 0xDA, 0x29, 0x48));
            public Brush DirForeground => Direction ? new SolidColorBrush(Color.FromArgb(0xff, 0xE7, 0xFB, 0xBE)) : new SolidColorBrush(Color.FromArgb(0xff, 0xDE, 0xBA, 0xCE));
        }
    }
}
