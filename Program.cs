using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;

namespace ArashiDNS.Aha
{
    internal class Program
    {
        public static IServiceProvider ServiceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
        public static IHttpClientFactory? ClientFactory = ServiceProvider.GetService<IHttpClientFactory>();
        public static string Server = "223.5.5.5";
        public static string AccountID = "";
        public static string AccessKeySecret = "";
        public static string AccessKeyID = "";
        public static IPEndPoint ListenerEndPoint = new(IPAddress.Loopback, 16883);
        public static TimeSpan Timeout = TimeSpan.FromMilliseconds(3000);

        static void Main(string[] args)
        {
            var cmd = new CommandLineApplication
            {
                Name = "ArashiDNS.Aha",
                Description = "ArashiDNS.Aha - 阿里云递归（公共）HTTP DNS 客户端" +
                              Environment.NewLine +
                              $"Copyright (c) {DateTime.Now.Year} Milkey Tan. Code released under the MIT License"
            };
            cmd.HelpOption("-?|-h|--help");
            var accountIDArgument = cmd.Argument("AccountID", "为云解析-公共 DNS 控制台的 Account ID，而非阿里云账号 ID");
            var accessKeySecretArgument = cmd.Argument("AccessKey Secret", "为云解析-公共 DNS 控制台创建密钥中的 AccessKey 的 Secret");
            var accessKeyIDArgument = cmd.Argument("AccessKey ID", "为云解析-公共 DNS 控制台创建密钥中的 AccessKey 的 ID");
            var wOption = cmd.Option<int>("-w <timeout>", "等待回复的超时时间(毫秒)。", CommandOptionType.SingleValue);
            var sOption = cmd.Option<int>("-s <name>", "设置的服务器的地址。", CommandOptionType.SingleValue);
            var ipOption = cmd.Option<string>("-l|--listen <IPEndPoint>", "监听的地址与端口。", CommandOptionType.SingleValue);

            cmd.OnExecute(() =>
            {
                if (!args.Any() && args.Length < 3)
                {
                    Console.WriteLine("缺少需要的参数。");
                    cmd.ShowHelp();
                    return;
                }

                AccountID = accountIDArgument.Value ?? "";
                AccessKeySecret = accessKeySecretArgument.Value ?? "";
                AccessKeyID = accessKeyIDArgument.Value ?? "";

                if (wOption.HasValue()) Timeout = TimeSpan.FromMilliseconds(wOption.ParsedValue);
                if (sOption.HasValue()) Server = sOption.Value()!;
                if (ipOption.HasValue()) ListenerEndPoint = IPEndPoint.Parse(ipOption.Value()!);
                if (ListenerEndPoint.Port == 0) ListenerEndPoint.Port = 16883;

                var dnsServer = new DnsServer(new UdpServerTransport(ListenerEndPoint),
                    new TcpServerTransport(ListenerEndPoint));
                dnsServer.QueryReceived += DnsServerOnQueryReceived;
                dnsServer.Start();

                Console.WriteLine("Now listening on: " + ListenerEndPoint);
                Console.WriteLine("Application started. Press Ctrl+C / q to shut down.");
                if (!Console.IsInputRedirected && Console.KeyAvailable)
                {
                    while (true)
                        if (Console.ReadKey().KeyChar == 'q')
                            Environment.Exit(0);
                }

                EventWaitHandle wait = new AutoResetEvent(false);
                while (true) wait.WaitOne();
            });

            cmd.Execute(args);
        }

        private static async Task DnsServerOnQueryReceived(object sender, QueryReceivedEventArgs e)
        {
            if (e.Query is not DnsMessage query) return;
            try
            {
                var msg = query.CreateResponseInstance();

                if (!query.Questions.Any())
                {
                    msg.ReturnCode = ReturnCode.ServerFailure;
                    e.Response = msg;
                    return;
                }

                var quest = query.Questions.First();

                if (quest.RecordType is RecordType.A or RecordType.Aaaa or RecordType.CName or RecordType.Ns
                    or RecordType.Txt)
                {
                    var dnsEntity = await GetRes(quest.Name.ToString(), quest.RecordType.ToString(),
                        TryGetEcs(query, out var ip) ? ip.ToString() : null);
                    if (dnsEntity!=null)
                    {
                        msg.ReturnCode = (ReturnCode)dnsEntity.Status!;
                        if (dnsEntity.Answer != null && dnsEntity.Answer.Any())
                            foreach (var item in dnsEntity.Answer) msg.AnswerRecords.Add(GetRecord(item));
                    }
                    else
                        msg.ReturnCode = ReturnCode.ServerFailure;

                    e.Response = msg;
                }
                else
                {
                    e.Response =
                        await new DnsClient(
                                IPAddress.TryParse(Server, out var svr) ? svr : IPAddress.Parse("223.6.6.6"),
                                (int) Timeout.TotalMilliseconds)
                            .SendMessageAsync(query);
                }
            }
            catch (Exception exception)
            {
                Console.Write(exception);

                var response = query.CreateResponseInstance();
                response.ReturnCode = ReturnCode.ServerFailure;
                e.Response = response;
            }
        }

        public static DnsRecordBase GetRecord(Answer answer)
        {
            var type = (RecordType) answer.Type;
            var name = DomainName.Parse(answer.Name);
            var ttl = answer.TTL;
            return type switch
            {
                RecordType.A => new ARecord(name, ttl, IPAddress.Parse(answer.Data)),
                RecordType.Aaaa => new AaaaRecord(name, ttl, IPAddress.Parse(answer.Data)),
                RecordType.CName => new CNameRecord(name, ttl, DomainName.Parse(answer.Data)),
                RecordType.Ns => new NsRecord(name, ttl, DomainName.Parse(answer.Data)),
                RecordType.Txt => new TxtRecord(name, ttl, answer.Data.Trim('\"')),
                _ => new TxtRecord(name, ttl, answer.Data.Trim('\"'))
            };
        }

        public static async Task<DNSEntity?> GetRes(string name, string type, string? ecs = null)
        {
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var key = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(AccountID + AccessKeySecret + ts + name + AccessKeyID)))
                .ToLower();
            var url =
                $"http://{Server}/resolve?name={name}&type={type}&uid={AccountID}&ak={AccessKeyID}&key={key}&ts={ts}";

            if (ecs != null && !string.IsNullOrWhiteSpace(ecs)) url += $"&edns_client_subnet={ecs}";
            var client = ClientFactory!.CreateClient();
            client.Timeout = Timeout;
            client.DefaultRequestHeaders.Add("User-Agent", "ArashiDNS.Aha/0.1");

            var res = await client.GetStringAsync(url);
            return JsonSerializer.Deserialize<DNSEntity>(res);
        }


        public static bool TryGetEcs(DnsMessage dnsMsg, out IPNetwork ipNetwork)
        {
            try
            {
                ipNetwork = new IPNetwork(IPAddress.Any, 0);

                if (!dnsMsg.IsEDnsEnabled) return false;
                foreach (var eDnsOptionBase in dnsMsg.EDnsOptions?.Options.ToArray()!)
                {
                    if (eDnsOptionBase is not ClientSubnetOption option) continue;
                    ipNetwork = new IPNetwork(option.Address, option.SourceNetmask);
                    return !Equals(option.Address, IPAddress.Any) && !Equals(option.Address, IPAddress.IPv6Any) &&
                           !IPAddress.IsLoopback(option.Address);
                }

                return false;
            }
            catch (Exception)
            {
                ipNetwork = new IPNetwork(IPAddress.Any, 0);
                return false;
            }
        }
    }
}
