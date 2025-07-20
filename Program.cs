﻿using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
        public static int EcsMethod = 0;
        public static bool NoCache = false;
        public static bool UseHttps = false;
        public static IPNetwork2 EcsAddress = new(IPAddress.Any, 0);
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
            var accountIDArgument = cmd.Argument("AccountID", "为云解析-解析配置-移动解析 HTTPDNS 控制台的 Account ID，而非阿里云账号 ID");
            var accessKeySecretArgument = cmd.Argument("AccessKey Secret", "为云解析-解析配置-移动解析 HTTPDNS 控制台创建密钥中的 AccessKey 的 Secret");
            var accessKeyIDArgument = cmd.Argument("AccessKey ID", "为云解析-解析配置-移动解析 HTTPDNS 控制台创建密钥中的 AccessKey 的 ID");
            var noCacheOption = cmd.Option("-n|--no-cache", "禁用内置缓存。", CommandOptionType.NoValue);
            var wOption = cmd.Option<int>("-w <timeout>", "等待回复的超时时间（毫秒）。", CommandOptionType.SingleValue);
            var sOption = cmd.Option<string>("-s <name>", "设置的服务器的地址。", CommandOptionType.SingleValue);
            var eOption = cmd.Option<int>("-e <method>",
                $"设置 ECS 处理模式。{Environment.NewLine}（0=按原样、-1=停用ECS、1=无ECS添加本地IP、2=无ECS添加请求IP、3=全部覆盖）",
                CommandOptionType.SingleValue);
            var ecsIpOption = cmd.Option<string>("--ecs-address <IPNetwork>", "覆盖设置本地 ECS 地址。(CIDR 形式，0.0.0.0/0)",
                CommandOptionType.SingleValue);
            var ipOption = cmd.Option<string>("-l|--listen <IPEndPoint>", "监听的地址与端口。", CommandOptionType.SingleValue);
            var httpsOption = cmd.Option("--https", "使用 HTTPS 传输。（请注意：HTTPS 按照 DoH 五倍计费，将导致配额消耗增加）",
                CommandOptionType.NoValue);

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
                if (eOption.HasValue()) EcsMethod = eOption.ParsedValue;
                if (ipOption.HasValue()) ListenerEndPoint = IPEndPoint.Parse(ipOption.Value()!);
                if (ListenerEndPoint.Port == 0) ListenerEndPoint.Port = 16883;
                UseHttps = httpsOption.HasValue();
                NoCache = noCacheOption.HasValue();

                if (EcsMethod != 0)
                {
                    if (ecsIpOption.HasValue())
                        EcsAddress = IPNetwork2.Parse(ecsIpOption.Value()!);
                    else
                    {
                        IPAddress originalIp;
                        using var httpClient = new HttpClient();
                        httpClient.DefaultRequestHeaders.Add("User-Agent", "ArashiDNS.Aha/0.1");
                        try
                        {
                            originalIp = IPAddress.Parse(httpClient
                                .GetStringAsync("https://www.cloudflare-cn.com/cdn-cgi/trace")
                                .Result.Split('\n').First(i => i.StartsWith("ip=")).Split("=").LastOrDefault()
                                ?.Trim() ?? string.Empty);
                        }
                        catch (Exception)
                        {
                            originalIp =
                                IPAddress.Parse(httpClient.GetStringAsync("http://whatismyip.akamai.com/").Result);
                        }

                        EcsAddress = IPNetwork2.Parse(originalIp + "/24");
                    }
                }

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
                var response = query.CreateResponseInstance();

                if (!query.Questions.Any())
                {
                    response.ReturnCode = ReturnCode.ServerFailure;
                    e.Response = response;
                    return;
                }

                if (!NoCache && DnsCache.TryGet(query,out var cache))
                {
                    e.Response = cache;
                    return;
                }

                if (query.Questions.First().Name.IsEqualOrSubDomainOf(DomainName.Parse("use-application-dns.net")))
                {
                    var msg = query.CreateResponseInstance();
                    msg.IsRecursionAllowed = true;
                    msg.IsRecursionDesired = true;
                    msg.ReturnCode = ReturnCode.NoError;
                    e.Response = msg;
                    return;
                }

                if (query.Questions.First().RecordClass == RecordClass.Chaos && query.Questions.First().RecordType == RecordType.Txt &&
                    query.Questions.First().Name.IsEqualOrSubDomainOf(DomainName.Parse("version.bind")))
                {
                    var msg = query.CreateResponseInstance();
                    msg.IsRecursionAllowed = true;
                    msg.IsRecursionDesired = true;
                    msg.AnswerRecords.Add(
                        new TxtRecord(query.Questions.First().Name, 3600, "ArashiDNS.Aha"));
                    e.Response = msg;
                    return;
                }

                var quest = query.Questions.First();

                if (quest.RecordType is RecordType.A or RecordType.Aaaa or RecordType.CName or RecordType.Ns
                    or RecordType.Txt)
                {
                    var ecs = EcsMethod switch
                    {
                        1 => TryGetEcs(query, out var ip) ? ip.ToString() : EcsAddress.ToString(),
                        2 => TryGetEcs(query, out var ip) && !LocalNetworks.Any(x => x.Contains(e.RemoteEndpoint.Address))
                            ? ip.ToString()
                            : string.Join(".", e.RemoteEndpoint.Address.ToString().Split('.').Take(3).Concat(["0/24"])),
                        3 => EcsAddress.ToString(),
                        -1 => null,
                        _ => TryGetEcs(query, out var ip) ? ip.ToString() : null
                    };
                    var dnsEntity = await GetDnsEntity(quest.Name.ToString(), quest.RecordType.ToString(), ecs);
                    if (dnsEntity != null)
                    {
                        response.ReturnCode = (ReturnCode) dnsEntity.Status!;
                        if (dnsEntity.Answer != null && dnsEntity.Answer.Any())
                            foreach (var item in dnsEntity.Answer)
                                response.AnswerRecords.Add(GetRecord(item));
                    }
                    else
                        response.ReturnCode = ReturnCode.ServerFailure;

                    if (!NoCache) DnsCache.Add(query, response);

                    e.Response = response;
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
                _ => new TxtRecord(name, ttl, answer.Data.Trim('\"'))
            };
        }

        public static async Task<DNSEntity?> GetDnsEntity(string name, string type, string? ecs = null)
        {
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var key = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(AccountID + AccessKeySecret + ts + name + AccessKeyID)))
                .ToLower();
            var url =
                $"{(UseHttps ? "https" : "http")}://{Server}/resolve?name={name}&type={type}&uid={AccountID}&ak={AccessKeyID}&key={key}&ts={ts}";

            if (ecs != null && !string.IsNullOrWhiteSpace(ecs)) url += $"&edns_client_subnet={ecs}";
            var client = ClientFactory!.CreateClient();
            client.Timeout = Timeout;
            client.DefaultRequestHeaders.Add("User-Agent", "ArashiDNS.Aha/0.1");

            return JsonSerializer.Deserialize(await client.GetStringAsync(url),
                SourceGenerationContext.Default.DNSEntity);
        }

        public static bool TryGetEcs(DnsMessage dnsMsg, out IPNetwork2 ipNetwork)
        {
            ipNetwork = new IPNetwork2(IPAddress.Any, 0);
            try
            {
                if (!dnsMsg.IsEDnsEnabled) return false;
                foreach (var eDnsOptionBase in dnsMsg.EDnsOptions?.Options.ToArray()!)
                {
                    if (eDnsOptionBase is not ClientSubnetOption option) continue;
                    ipNetwork = new IPNetwork2(option.Address, option.SourceNetmask);
                    return !Equals(option.Address, IPAddress.Any) && !Equals(option.Address, IPAddress.IPv6Any) &&
                           !IPAddress.IsLoopback(option.Address);
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static HashSet<IPNetwork2> LocalNetworks = new()
        {
            IPNetwork2.Parse("10.0.0.0/8"),
            IPNetwork2.Parse("100.64.0.0/10"),
            IPNetwork2.Parse("127.0.0.0/8"),
            IPNetwork2.Parse("169.254.0.0/16"),
            IPNetwork2.Parse("172.16.0.0/12"),
            IPNetwork2.Parse("192.0.0.0/24"),
            IPNetwork2.Parse("192.0.2.0/24"),
            IPNetwork2.Parse("192.88.99.0/24"),
            IPNetwork2.Parse("192.168.0.0/16"),
            IPNetwork2.Parse("198.18.0.0/15"),
            IPNetwork2.Parse("198.18.0.0/15"),
            IPNetwork2.Parse("198.51.100.0/24"),
            IPNetwork2.Parse("203.0.113.0/24"),
            IPNetwork2.Parse("224.0.0.0/4"),
            IPNetwork2.Parse("233.252.0.0/24"),
            IPNetwork2.Parse("240.0.0.0/4"),
            IPNetwork2.Parse("255.255.255.255/32")
        };
    }
}
