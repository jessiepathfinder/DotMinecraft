using System.IO.Pipes;
using System.Net.Sockets;

namespace MCSniffer
{
	internal static class Program
	{
		static void Main(string[] args)
		{
			if(args.Length != 2){
				Console.WriteLine("USAGE: MCSniffer [server address] [listening port]");
				Console.WriteLine("WARNING: This tool does not work with encryption-enabled servers!");
				return;
			}
			string underlyingServer = args[0];
			TcpListener tcpListener = new TcpListener(System.Net.IPAddress.Loopback, Convert.ToInt32(args[1]));
			tcpListener.Start();
			ushort port;
			int ps = underlyingServer.LastIndexOf(':');
            if (ps > -1)
            {
				port = Convert.ToUInt16(underlyingServer.Substring(ps + 1));
				underlyingServer = underlyingServer.Substring(0, ps);
            } else{
				port = 25565;
			}
            while (true){
				Socket socket = tcpListener.AcceptSocket();
				Thread clientHandlerThread = new Thread(() => ClientToServerCopier(socket, underlyingServer, port));
				clientHandlerThread.IsBackground = true;
				clientHandlerThread.Name = "MCSniffer client-to-server data copier thread";
				clientHandlerThread.Start();
			}
		}
		private const int SEGMENT_BITS = 0x7F;
		private const int CONTINUE_BIT = 0x80;
		private static (int,int) ReadVarInt(Stream underlying, Span<byte> copy)
		{
			int value = 0;
			int position = 0;
			int p1 = 0;

			while (true)
			{
				int currentByte = underlying.ReadByte();
				if (currentByte < 0){
					if (position == 0) return (0, 0);
					throw new Exception("Unexpected end of stream");
				}
				copy[p1++] = (byte)currentByte;
				value |= (currentByte & SEGMENT_BITS) << position;

				if ((currentByte & CONTINUE_BIT) == 0) break;

				position += 7;

				if (position >= 32) throw new Exception("VarInt is too big");
			}

			return (value,p1);
		}
		private static void ClientToServerCopier(Socket socket, string server, ushort port){
			TcpClient tcpClient = new TcpClient();
			tcpClient.Connect(server, port);
			NetworkStream serverStream = tcpClient.GetStream();

			BufferedStream serverInputStream = new BufferedStream(serverStream,65536);

			NetworkStream clientStream = new NetworkStream(socket, FileAccess.ReadWrite, true);

			Thread thread = new Thread(() => Mirror(serverInputStream,clientStream, "server -> client: "));
			thread.Name = "server-to-client copier thread";
			thread.IsBackground = true;
			thread.Start();
			Mirror(new BufferedStream(clientStream, 65536), serverStream, "client -> server: ");
		}
		
		private static readonly object consoleLocker = new();
		private static void Mirror(Stream source, Stream destination, string prefix){
			Span<byte> bounce = stackalloc byte[65536];
			try{
				while (true)
				{
					(int siz, int siz2) = ReadVarInt(source, bounce);
					if (siz2 == 0)
					{
						destination.Dispose();
						return;
					}
					siz += siz2;

					//HACK: Allow us to conditionally use our built-in scratchpad
					Span<byte> scratchpad = bounce;
					if (siz > 65536)
					{
						scratchpad = new byte[siz];
						bounce.Slice(0, siz2).CopyTo(scratchpad);
					}
					else
					{
						scratchpad = bounce.Slice(0, siz);
					}
					source.ReadExactly(scratchpad.Slice(siz2));
					destination.Write(scratchpad);
					string f = prefix + Convert.ToHexString(scratchpad);
					lock (consoleLocker)
					{
						Console.WriteLine(f);
					}
				}
			} catch(Exception e){
				Console.Error.WriteLine("Exception in copy thread: " + e);	
			} finally{
				source.Dispose();
			}
		}
	}
}