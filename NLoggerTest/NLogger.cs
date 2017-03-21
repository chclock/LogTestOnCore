using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NLoggerTest
{
	public class NLogger
	{
		/// <summary>
		/// 读写队列单元为字符数组，包含3个字符串
		/// 文件夹, 文件名, 日志
		/// </summary>
		private readonly ConcurrentQueue<String[]> WriteQueue = new ConcurrentQueue<String[]>();
		private readonly ConcurrentQueue<String[]> ReadQueue = new ConcurrentQueue<String[]>();
		// AutoResetEvent 与 ManualResetEvent 区别在于它释放锁后, IsRelease 自动为false, 并且只随机解放一个线程
		private readonly AutoResetEvent Pause = new AutoResetEvent(false);
		private int totalCount = 0;

		private static readonly object Lock = new object();
		// 单例模式, 但对外依旧只提供方法
		private static NLogger _instance;
		private static NLogger Instance
		{
			get
			{
				if (_instance == null)
				{
					lock (Lock)
					{
						if(_instance == null)
						{
							_instance = new NLogger();
						}
					}
				}
				return _instance;
			}
		}

		private NLogger()
		{
			// 后台线程持续方法, 读写队列双缓冲
			Action writeTask = () =>
			{
				Console.WriteLine("日志记录线程启动");
				// 一直执行
				while (true)
				{
					// 此处修改原来代码逻辑
					// 原此处代码只有 Pause.WaitOne();
					// 由于读写延迟, 并且AutoResetEvent释放后自动把 IsRelease 设为false
					// 会造成读写队列还有数据未写入日志时候, 无法获取信号, 造成线程堵塞
					if (WriteQueue.Count == 0 && ReadQueue.Count ==0)
					{
						Pause.WaitOne();
					}
					string[] tmp;
					// 使用线程安全队列， 所以不用lock
					while (WriteQueue.TryDequeue(out tmp))
					{
						ReadQueue.Enqueue(tmp);
					}
					List<string[]> tempQueue = new List<string[]>();
					while (ReadQueue.TryDequeue(out tmp))
					{
						totalCount += 1;
					    // 判断是否有同文件日志
						var tmpItem = tempQueue.FirstOrDefault(d => d[0] == tmp[0] && d[1] == tmp[1]);
						// 日志不合并, 直接添加进缓冲队列
						// tempQueue.Add(tmp);
						if (tmpItem == null)
						{
							tempQueue.Add(tmp);
						}
						else
						{
							// 一次循环中, 写入同一文件合并, 减少文件IO操作
							tmpItem[2] = string.Concat(tmpItem[2], Environment.NewLine, tmp[2]);
							if (tmpItem[2].Length > 64 * 1024)
							{
								// 限制单次写入文件日志信息大小, 超过大小, 跳出循环，读队列内容下次再读取
								break;
							}
						}
					}
					Console.WriteLine("已读取数据: " + totalCount);
					Console.WriteLine("写队列剩余数据: " + WriteQueue.Count);
					Console.WriteLine("读队列剩余数据: " + ReadQueue.Count);
					// 写日志
					// 所有写日志都在此线程中, 不用担心多线程读写文件问题
					foreach (var item in tempQueue)
					{
						string logPath = GetLogPath(item[0], item[1]);
						string infoData = item[2] + Environment.NewLine + "----------------------------------------------------------------------------------------" + Environment.NewLine;
						WriteText(logPath, infoData);
					}
				}
			};
			// 启动新线程
		    // 一直运行
			// 新建线程，不从线程池获取
			Task.Factory.StartNew(writeTask, TaskCreationOptions.LongRunning);
		}

		/// <summary>
		/// 对外开放写日志方法
		/// </summary>
		/// <param name="preFile">文件名</param>
		/// <param name="infoData">日志</param>
		public static void WriteLog(string preFile, string infoData)
		{
			WriteLog(string.Empty, preFile, infoData);
		}

		/// <summary>
		/// 对外开放写日志方法
		/// </summary>
		/// <param name="customDirectory">日志目录地址</param>
		/// <param name="preFile">文件名</param>
		/// <param name="infoData">日志</param>
		public static void WriteLog(string customDirectory, string preFile, string infoData)
		{
			// 线程安全队列
			// 不需lock
			// 自定义日志信息格式
			string logInfo = $"[线程号: {Thread.CurrentThread.ManagedThreadId}] {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")} {infoData}";
			NLogger.Instance.WriteQueue.Enqueue(new[] { customDirectory, preFile, logInfo });
			// 释放锁，允许一个或多个线程继续
			// 由于只在静态构造函数时建立的线程持有信号量， 所以就一个线程开始执行
			// 使用 AutoResetEvent, 释放后又自动持有锁，所以理论上，一个线程调用写日志函数，读写队列只会进入一个日志信息， 多个进程，可能进入多个
			// 释放后, 日志线程收到通知, 开始读写
			NLogger.Instance.Pause.Set();
		}

		/// <summary>
		/// 获取日志文件路径
		/// </summary>
		/// <param name="customDirectory">日志文件夹</param>
		/// <param name="preFile">日志文件名</param>
		/// <returns></returns>
		private String GetLogPath(string customDirectory, string preFile)
		{
			String newFilePath = String.Empty;
			String logDir = String.IsNullOrEmpty(customDirectory) ? Path.Combine(Directory.GetCurrentDirectory(), "Logs") : customDirectory;
			if (!Directory.Exists(logDir))
			{
				Directory.CreateDirectory(logDir);
			}
			String extendsion = ".log";
			String fileNameNotExt = String.Concat(preFile, DateTime.Now.ToString("yyyyMMdd"));
			String fileName = String.Concat(fileNameNotExt, extendsion);
			// 正则, 匹配当天日志
			String fileNamePattern = String.Concat(fileNameNotExt, "(*)", extendsion);
			String noPattern = @"(?is)(?<=\()(.*)(?=\))";
			// 只获取当前目录匹配文件
			List<String> filePaths = new List<string>(Directory.GetFiles(logDir, fileNamePattern, SearchOption.TopDirectoryOnly));
			List<String> correctFilePaths = new List<string>();
			if (filePaths.Count > 0)
			{
				foreach(String fPath in filePaths)
				{
					String no = new Regex(noPattern).Match(Path.GetFileName(fPath)).Value;
					if (int.TryParse(no, out int tmpNo))
					{
						correctFilePaths.Add(fPath);
					}
				}
			}

			if (correctFilePaths.Count > 0)
			{
				correctFilePaths.Sort((x, y) => x.CompareTo(y));
				int fileMaxLen = 0;
				for(int i = 0; i < correctFilePaths.Count; i++)
				{
					int itemLength = correctFilePaths[i].Length;
					fileMaxLen = itemLength > fileMaxLen ? itemLength : fileMaxLen;
				}
				String lastFilePath = correctFilePaths.LastOrDefault(d => d.Length == fileMaxLen);
				long actualSize = new FileInfo(lastFilePath).Length;
				long masSize = 10 * 1024 * 1024;
				if (actualSize < masSize)
				{
					newFilePath = lastFilePath;
				}
				else
				{
					String no = new Regex(noPattern).Match(Path.GetFileName(lastFilePath)).Value;
					bool parse = int.TryParse(no, out int tempNo);
					tempNo = parse ? tempNo + 1 : tempNo;
					String formatNo = $"({tempNo})";
					String newFileName = String.Concat(fileNameNotExt, formatNo, extendsion);
					newFilePath = Path.Combine(logDir, newFileName);
				}
			}
			else
			{
				String newFileName = String.Concat(fileNameNotExt, "(0)", extendsion);
				newFilePath = Path.Combine(logDir, newFileName);
			}
			return newFilePath;
		}

		/// <summary>
		/// 真正的将日志写入文件操作
		/// </summary>
		/// <param name="logPath">日志文件路径</param>
		/// <param name="logContent">日志</param>
		private void WriteText(string logPath, string logContent)
		{
			try
			{
				using(StreamWriter sw = File.AppendText(logPath))
				{
					sw.Write(logContent);
					sw.Flush();
				}
			}
			catch(Exception ex)
			{
				throw ex;
			}
			finally
			{

			}
		}
	}
}
