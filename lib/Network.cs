using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace VVVV.Nodes.ARDrone
{
	public class UDPInterfacer : Networker
	{
		private UdpClient client;
		public UdpClient Client
		{
			get { return client; }
		}
		
		public UDPInterfacer(IPAddress IP, int Port) : base(IP, Port)
		{
			client = new UdpClient();
			client.ExclusiveAddressUse = false;
			client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
		}
		
		protected override void Connecting()
		{
			if (!this.connected)
				client.Client.Bind(this.localEP);
		}
		
		protected override void Disconnecting()
		{
			client.Client.Close();
			client = null;
		}
		
		protected override void ProcessWorker() {}
		
		public int SendMessage(int message)
        {
            return SendMessage(BitConverter.GetBytes(message));
        }

        public int SendMessage(String message)
        {
            byte[] buffer = System.Text.Encoding.ASCII.GetBytes(message);
            return SendMessage(buffer);
        }

        public int SendMessage(byte[] message)
        {
            return client.Send(message, message.Length, this.remoteEP);
        }
	}
	
	/// <summary>
	/// Description of Network.
	/// </summary>
	public abstract class Networker
	{
		protected IPEndPoint remoteEP;
		protected IPEndPoint localEP;
		protected int port;
		protected volatile bool connected;
		protected Thread worker;
		protected bool working;
		
		public Networker(IPAddress IP, int Port)
		{
			port = Port;
            remoteEP = new IPEndPoint(IP, port);

            GetHostIP();
			connected = false;
            working = false;
		}
		
		private void GetHostIP ()
		{
			IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
			foreach (IPAddress ip in host.AddressList)
			{
			    if (ip.AddressFamily == AddressFamily.InterNetwork)
			    {
			    	try
			    	{
			    		localEP = new IPEndPoint(ip, port);
			    	}
			    	catch
			    	{
			    		Disconnect();
			    		GetHostIP();
			    	}
			    }
			}
		}
		
		public void Connect()
		{
			if (!connected)
			{
				Connecting();
				connected = true;
				StartWorker();
			}
		}
		protected abstract void Connecting();
		
		public void Disconnect()
		{
			if (connected)
			{
				connected = false;
				EndWorker();
				Disconnecting();
			}
		}
		protected abstract void Disconnecting();
		
		#region thread stuff
		protected void StartWorker()
		{
			worker = new Thread(ProcessWorker);
			worker.Start();
			working = true;
		}
		
		protected abstract void ProcessWorker();
		
		protected void EndWorker()
        {
            worker.Join(500);
            worker.Abort();
            worker = null;
            working = false;
        }
		#endregion
	}
}
