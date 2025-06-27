using System.IO.Compression;
using System.Net.Sockets;

namespace DotMinecraft.Server
{
	internal static class Program
	{
		static void Main(string[] args)
		{
			/*
			MemoryStream ms = new MemoryStream();
			using(DeflateStream ds = new DeflateStream(ms, CompressionLevel.SmallestSize, true)){
				ds.Write(new byte[256]);
			}
			Console.WriteLine(Convert.ToHexString(ms.ToArray()));
			*/
			TcpListener tcpListener = new TcpListener(System.Net.IPAddress.Any,12345);
			tcpListener.Start();
			MinecraftListener mcl = new MinecraftListener(tcpListener);
			try{
				
			} finally{
				while (Console.ReadLine() != "stop")
				{

				}
			}

		}
	}
}