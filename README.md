该项目基于[Sam Xiao](http://home.cnblogs.com/u/xcj26/)的[性能秒杀log4net的NLogger日志组件(附测试代码与NLogger源码)](http://www.cnblogs.com/xcj26/p/6391853.html)),将项目迁移至Net Core 平台，添加了注释，并修改部分逻辑和使用新技术代替旧的技术实现,，同时修改了测试代码。

`NLogger`是一款轻量级的、使用默认配置(目前只能通过修改源码来修改配置)的、高性能日志组件。测试中开启10线程，共写入10 * 100000 条日志， 用时低于10秒, 表现远远优于log4net。