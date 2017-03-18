using log4net;
using log4net.Config;
using log4net.Repository;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Log4netTest
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.OutputEncoding = System.Text.Encoding.UTF8;
			Console.WriteLine("测试log4net开启10线程, 记录10*100000条记录用时");
			ILoggerRepository repository = LogManager.CreateRepository("Log4netTest");
			XmlConfigurator.Configure(repository, new FileInfo("log4net.config"));
			Action<int> task = i =>
			{
				ILog log = LogManager.GetLogger(repository.Name, "Log4netTest");
				Console.WriteLine($"开启线程{i}");
				Stopwatch sw = new Stopwatch();
				sw.Start();
				for (int j = 0; j < 100000; j++)
				{
					log.Info($"日志测试数据, 序号： {j}");
				}
				sw.Stop();
				Console.WriteLine($"线程{i}写入日志结束, 共用时{sw.ElapsedMilliseconds}毫秒");
			};
			Parallel.For(0, 10, task);
			Console.ReadKey();
		}
	}
}