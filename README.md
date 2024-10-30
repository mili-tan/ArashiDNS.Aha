<p align="center">
  <img src='https://mili.one/pics/arashiaha.png' width="70%" height="70%"/>
</p>

----------
阿里云递归解析（公共DNS）HTTP DNS 客户端

> 请确保已经 [安装 .NET SDK](https://learn.microsoft.com/zh-cn/dotnet/core/install/linux) 运行环境
```
git clone https://github.com/mili-tan/ArashiDNS.Aha
cd ArashiDNS.Aha
dotnet run -c Release <AccountID> <AccessKey Secret> <AccessKey ID>
```
或者使用 Docker（南大镜像）：
```
docker run -d -p 127.0.0.1:16883:16883 -p 127.0.0.1:16883:16883/udp ghcr.nju.edu.cn/mili-tan/arashidns.aha <AccountID> <AccessKey Secret> <AccessKey ID> -l 0.0.0.0:16883
```
GHCR：
```
docker run -d -p 127.0.0.1:16883:16883 -p 127.0.0.1:16883:16883/udp ghcr.io/mili-tan/arashidns.aha <AccountID> <AccessKey Secret> <AccessKey ID> -l 0.0.0.0:16883
```
--------

```
ArashiDNS.Aha - 阿里云递归（公共）HTTP DNS 客户端
Copyright (c) 2024 Milkey Tan. Code released under the MIT License

Usage: ArashiDNS.Aha [options] <AccountID> <AccessKey Secret> <AccessKey ID>

Arguments:
  AccountID                  为云解析-公共 DNS 控制台的 Account ID，而非阿里云账号 ID
  AccessKey Secret           为云解析-公共 DNS 控制台创建密钥中的 AccessKey 的 Secret
  AccessKey ID               为云解析-公共 DNS 控制台创建密钥中的 AccessKey 的 ID

Options:
  -?|-h|--help               Show help information.
  -n|--no-cache              禁用内置缓存。
  -w <timeout>               等待回复的超时时间（毫秒）。
  -s <name>                  设置的服务器的地址。
  -e <method>                设置 ECS 处理模式。
                             （0=按原样、-1=停用ECS、1=无ECS添加本地IP、2=无ECS添加请求IP、3=全部覆盖）
  --ecs-address <IPNetwork>  覆盖设置本地 ECS 地址。(CIDR 形式，0.0.0.0/0)
  -l|--listen <IPEndPoint>   监听的地址与端口。
```

## See also

- [xireiki/Aha-Go](https://github.com/xireiki/Aha-Go)
- [honwen/dnspod-http-dns-libev](https://github.com/honwen/dnspod-http-dns-libev)

## License

Copyright (c) 2024 Milkey Tan. Code released under the [MIT License](https://github.com/mili-tan/ArashiDNS.Aha/blob/main/LICENSE). 

<sup>ArashiDNS™ is a trademark of Milkey Tan.</sup>
