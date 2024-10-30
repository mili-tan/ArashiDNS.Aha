using System.Runtime.Caching;
using ArashiDNS.Aha;
using ARSoft.Tools.Net.Dns;

namespace ArashiDNS
{
    public class DnsCache
    {
        public static void Add(DnsMessage qMessage, DnsMessage aMessage)
        {
            if (aMessage.ReturnCode == ReturnCode.ServerFailure) return;
            var question = qMessage.Questions;
            var answerRecords = aMessage.AnswerRecords ?? new List<DnsRecordBase>();
            var ttl = answerRecords.Any() ? answerRecords.Min(x => x.TimeToLive) : 15;
            Add(
                new CacheItem(
                    question.First().ToString() +
                    (Program.TryGetEcs(qMessage, out var network) ? network : string.Empty),
                    (answerRecords, aMessage.ReturnCode)), ttl);
        }

        public static void Add(CacheItem cacheItem, int ttl)
        {
            if (!MemoryCache.Default.Contains(cacheItem.Key))
                MemoryCache.Default.Add(cacheItem,
                    new CacheItemPolicy
                    {
                        AbsoluteExpiration =
                            DateTimeOffset.Now + TimeSpan.FromSeconds(ttl)
                    });
        }

        public static bool Contains(DnsMessage qMessage)
        {
            return MemoryCache.Default.Contains(qMessage.Questions.First().ToString() +
                                                (Program.TryGetEcs(qMessage, out var network) ? network : string.Empty));
        }

        public static (List<DnsRecordBase>, ReturnCode) Get(DnsMessage qMessage)
        {
            return ((List<DnsRecordBase>, ReturnCode))MemoryCache.Default.Get(qMessage.Questions.First().ToString() +
                                                                               (Program.TryGetEcs(qMessage, out var network) ? network : string.Empty));
        } 

        public static bool TryGet(DnsMessage query, out DnsMessage message)
        {
            var contains = Contains(query);
            message = query.CreateResponseInstance();
            if (!contains) return contains;
            var get = Get(query);
            message.ReturnCode = get.Item2;
            message.AnswerRecords.AddRange(get.Item1);
            message.IsRecursionDesired = true;
            message.IsRecursionDesired = true;
            return contains;
        }
    }
}
