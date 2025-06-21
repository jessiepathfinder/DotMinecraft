using System.Net.Sockets;

namespace DotMinecraft.Server
{
	internal static class Program
	{
		static void Main(string[] args)
		{
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