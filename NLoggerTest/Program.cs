using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace NLoggerTest
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.OutputEncoding = System.Text.Encoding.UTF8;
			Console.WriteLine("测试NLogger开启10线程, 记录10*100000条记录用时");
			Action<int> task = i =>
			{
				Console.WriteLine($"开启线程{i}");
				Stopwatch sw = new Stopwatch();
				sw.Start();
				for (int j = 0; j < 100000; j++)
				{
					NLogger.WriteLog("test_", $"日志测试数据, 序号： {j}");
				}
				sw.Stop();
				Console.WriteLine($"线程{i}写入日志结束, 共用时{sw.ElapsedMilliseconds}毫秒");
			};
			Parallel.For(0, 10, task);
			//Console.WriteLine("日志写入结束, 意味所有日志写入读写队列, 并未全部写入文件");
			//Console.WriteLine("日志写入文件正在后台线程执行, 如果此时结束当前线程, 文件写入也会停止");
			Console.ReadKey();
		}
	}
}