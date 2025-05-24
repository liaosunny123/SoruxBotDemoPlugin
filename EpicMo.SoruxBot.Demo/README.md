# SoruxBot Demo Plugins
这是 SoruxBot 综合性质的聚合功能插件，目前具有的功能是：
1. Yiyan Controller：提供一句话生成的功能。
2. LookUp Controller：提供网页信息总结的功能。
3. ConversationController：提供 AI 对话的功能。

## 环境要求

本插件需要你使用 SoruxBot Wrapper (Chromium版本) 运行。你可以自己安装 Chromium 环境，或者使用 SoruxBot 官方的 SoruxBot For Chromium 的镜像。


## 插件构建

本插件基于 SoruxBot QQ SDK >= 1.0.6 构建（SoruxBot General SDK >= 1.1.1）。

## 命令

所有命令的前缀均为：#

### 私聊

ai set [token]
设置当前对话的 token。

saying <type>
输出一句话，type为类型，可选

### 群聊

ai start
开始 AI 对话

lookup [link]
输出一个 Link 对应的关键词和概要信息