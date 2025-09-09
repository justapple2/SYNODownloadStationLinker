# SYNODownloadStationLinker

SYNODownloadStationLinker 是一个因个人需求和练习目的，基于Avalonia搭建的利用群晖DownloadStation Web Api进行手动/自动下载的桌面客户端。

[![](https://img.shields.io/badge/License-MIT-blue.svg)](\src\LICENSE.txt)

## 功能

1.手动添加下载链接通过API发送给Download Station下载；

2.监控剪切板，验证链接合法后自动向Download Station推送任务下载

## 注意

1.必须先登录成功才可进行下载，暂时仅在DMS218j的DSM 7.1.1-42962 Update 9版本下测试过；

2.首次打开时会随机生成AES加密密钥以json存储在文档中，nas登录用户名与密码以AES加密为base64字符串保存至本地json文件中；

3.只建议在本地局域网环境中使用本软件，因不当使用造成nas用户密码泄露或其他非预期请自行承担后果。


