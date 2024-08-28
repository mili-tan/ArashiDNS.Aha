<p align="center">
  <img src='https://mili.one/pics/arashiaha.png' width="70%" height="70%"/>
</p>

----------
阿里云递归解析（公共DNS）HTTP DNS 客户端

> 请确保已经 [安装 .NET SDK](https://learn.microsoft.com/zh-cn/dotnet/core/install/linux) 运行环境
```
git clone https://github.com/mili-tan/ArashiDNS.Aha
cd ArashiDNS.Aha
dotnet run <AccountID> <AccessKey Secret> <AccessKey ID>
```
或者使用 Docker (很快就来)。
```
ArashiDNS.Aha - 阿里云递归（公共）HTTP DNS 客户端
Copyright (c) 2024 Milkey Tan. Code released under the MIT License

Usage: ArashiDNS.Aha [options] <AccountID> <AccessKey Secret> <AccessKey ID>

Arguments:
  AccountID                 为云解析-公共 DNS 控制台的 Account ID，而非阿里云账号 ID
  AccessKey Secret          为云解析-公共 DNS 控制台创建密钥中的 AccessKey 的 Secret
  AccessKey ID              为云解析-公共 DNS 控制台创建密钥中的 AccessKey 的 ID

Options:
  -?|-h|--help              显示帮助信息。
  -w <Timeout>              等待回复的超时时间(毫秒)。
  -s <IPAddress>            设置的服务器的地址。
  -l|--listen <IPEndPoint>  监听的地址与端口。
```

## License

Copyright (c) 2024 Milkey Tan. Code released under the [MIT License](https://github.com/mili-tan/ArashiDNS.Aha/blob/main/LICENSE). 

<sup>ArashiDNS™ is a trademark of Milkey Tan.</sup>
