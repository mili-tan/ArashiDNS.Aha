using System.Net;
using System.Security.Cryptography;
using System.Text;
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
                              $"Copyright (c) {DateTime.Now.Year} Milkey Tan. Code released under the MPL License"
            };
            cmd.HelpOption("-?|-h|--help");
            var accountIDArgument = cmd.Argument("AccountID", "Account ID 为阿里云官网中云解析-公共DNS控制台中的 Account ID，而非阿里云账号 ID");
            var accessKeySecretArgument = cmd.Argument("AccessKeySecret", "AccessKey Secret 为阿里云官网中云解析-公共 DNS 控制台创建密钥中创建的 AccessKey 的 Secret");
            var accessKeyIDArgument = cmd.Argument("AccessKeyID", "AccessKey ID 为阿里云官网中云解析-公共 DNS 控制台创建密钥中创建的 AccessKey 的 ID");
            var wOption = cmd.Option<int>("-w <timeout>", "等待回复的超时时间(毫秒)。", CommandOptionType.SingleValue);
            var sOption = cmd.Option<int>("-s <name>", "设置的服务器的地址。", CommandOptionType.SingleValue);
            var ipOption = cmd.Option<string>("-l|--listen <IPEndPoint>", "监听的地址与端口。", CommandOptionType.SingleValue);

            cmd.OnExecute(() =>
            {
                AccountID = accountIDArgument.Value ?? "";
                AccessKeySecret = accessKeySecretArgument.Value ?? "";
                AccessKeyID = accessKeyIDArgument.Value ?? "";

                if (wOption.HasValue()) Timeout = TimeSpan.FromMilliseconds(wOption.ParsedValue);
                if (sOption.HasValue()) Server = sOption.Value()!;
                if (ipOption.HasValue()) ListenerEndPoint = IPEndPoint.Parse(ipOption.Value()!);

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
                    return;
                }

                var quest = query.Questions.First();

                if (quest.RecordType is RecordType.A or RecordType.Aaaa or RecordType.CName or RecordType.Ns or RecordType.Txt)
                {
                    var res = await GetRes(quest.Name.ToString(), quest.RecordType.ToString());
                    if (res != null && res.Any())
                    {
                        foreach (var item in res)
                            if (item != null && !string.IsNullOrWhiteSpace(item))
                                msg.AnswerRecords.Add(GetRecord(quest, item));
                    }
                    else
                        msg.ReturnCode = ReturnCode.ServerFailure;
                }
                else
                {
                    e.Response =
                        await new DnsClient(
                            IPAddress.TryParse(Server, out var svr) ? svr : IPAddress.Parse("223.6.6.6"),
                            Timeout.Milliseconds).SendMessageAsync(query);
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

        public static DnsRecordBase GetRecord(DnsQuestion quest, string item, int ttl = 600)
        {
            return quest.RecordType switch
            {
                RecordType.A => new ARecord(quest.Name, 600, IPAddress.Parse(item)),
                RecordType.Aaaa => new AaaaRecord(quest.Name, 600, IPAddress.Parse(item)),
                RecordType.CName => new CNameRecord(quest.Name, 600, DomainName.Parse(item)),
                RecordType.Ns => new NsRecord(quest.Name, 600, DomainName.Parse(item)),
                RecordType.Txt => new TxtRecord(quest.Name, 600, item),
                _ => new TxtRecord(quest.Name, 600, item)
            };
        }

        public static async Task<string?[]?> GetRes(string name, string type)
        {
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var key = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(AccountID + AccessKeySecret + ts + name + AccessKeyID)))
                .ToLower();
            var url =
                $"http://{Server}/resolve?name={name}&type={type}&uid={AccountID}&ak={AccessKeyID}&key={key}&ts={ts}&short=1";

            var client = ClientFactory!.CreateClient();
            client.Timeout = Timeout;

            return JsonNode.Parse(await client.GetStringAsync(url))
                ?.AsArray()
                .Select(x => x?.ToString()).ToArray();
        }
    }
}
