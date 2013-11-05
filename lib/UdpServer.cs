/*
 * Created by SharpDevelop.
 * User: woeishi
 * Date: 31.01.2013
 * Time: 17:04
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Linq;
using System.Collections.Concurrent;

namespace VVVV.Nodes.ARDrone
{
	public class DataReceivedEventArgs : EventArgs  
	{
		public DataReceivedEventArgs(IPEndPoint source, byte[] data)
		{
			this.Source = source;
			this.Data = data;
		}
		public IPEndPoint Source { get; private set;}
		public byte[] Data { get; private set;}
	}
	
	public class IssueSendEventArgs : EventArgs  
	{
		public IssueSendEventArgs(IPEndPoint destination, byte[] data)
		{
			this.Destination = destination;
			this.Data = data;
		}
		public IPEndPoint Destination { get; private set;}
		public byte[] Data { get; private set;}
	}
	
	/// <summary>
	/// Description of NavDataServer.
	/// </summary>
	public class UdpServer : IDisposable
	{
		private IPEndPoint localEP;
		private int port;
		private UdpClient client;
		private volatile bool connected;
		private volatile bool receive;
		private Thread worker;
		private ConcurrentDictionary<IPEndPoint, IPEndPoint> remotes;
		
		private bool disposed = false;
		
		public event EventHandler<DataReceivedEventArgs> DataReceived;
		protected virtual void OnDataReceived(DataReceivedEventArgs e)
		{
			EventHandler<DataReceivedEventArgs> handler = DataReceived;
			if (handler != null)
            	handler(this, e);
		}
		
		public event EventHandler<IssueSendEventArgs> SendError;
		protected virtual void OnSendError(IssueSendEventArgs e)
		{
			EventHandler<IssueSendEventArgs> handler = SendError;
			if (handler != null)
            	handler(this, e);
		}	
		
		public UdpServer(int Port, bool doReceive=false)
		{
			port = Port;

			connected = false;
			receive = doReceive;
			
			remotes = new ConcurrentDictionary<IPEndPoint,IPEndPoint>();
		}
		
		public void SetLocalIP(IPAddress ip, bool doConnect)
		{
			Disconnect();
			localEP = null;
			if (doConnect)
			{
				try
				{
					localEP = new IPEndPoint(ip, port);
				}
				catch {}
				Connect();
			}
		}
		
		public void AddRemotes(IPAddress IP)
		{
			IPEndPoint ep = new IPEndPoint(IP, port);
			if (!remotes.Keys.Contains(ep))
				remotes.TryAdd(ep,ep);
		}
		
		public void RemoveRemotes(IPAddress IP)
		{
			IPEndPoint ep = new IPEndPoint(IP, port);
			IPEndPoint ret;
			if (remotes.Keys.Contains(ep))
				remotes.TryRemove(ep, out ret);
		}
		
		public void IssueMessage(object sender, IssueSendEventArgs e)
		{
			int bytesSent = SendMessage(e.Data, e.Destination.Address);
			if (bytesSent != -1)
				OnSendError(new IssueSendEventArgs(e.Destination, BitConverter.GetBytes(bytesSent)));
		}
		
        public int SendMessage(byte[] message, IPAddress IP)
        {
        	IPEndPoint ep = new IPEndPoint(IP, port);
        	int bytesSent = client.Send(message, message.Length, ep);
        	if (bytesSent != message.Length)
            	return bytesSent;
        	else
        		return -1;
        }
		
		public void Connect()
		{
			if (!connected)
			{
				connected = true;
				client = new UdpClient();
				client.ExclusiveAddressUse = false;
				client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				client.Client.Bind(this.localEP);
				if (receive)
				{
					worker = new Thread(ReceiveWorker);
					worker.Start();
				}
			}
		}
		
		private void ReceiveWorker()
		{
			while (connected)
			{
					IPEndPoint any = new IPEndPoint(IPAddress.Any, port);
					byte[] buffer = client.Receive(ref any);
					if (buffer.Length > 0)
					{
						string test ="";
						
						if (remotes.Keys.Contains(any))
						{
							if (any.Address.ToString() == "192.168.10.30")
								test = System.Text.ASCIIEncoding.ASCII.GetString(buffer);
							OnDataReceived(new DataReceivedEventArgs(new IPEndPoint(any.Address,any.Port), buffer));
	//						DataReceived.BeginInvoke(this, new DataReceivedEventArgs(new IPEndPoint(any.Address,any.Port), buffer), ar => { var del = (EventHandler<DataReceivedEventArgs>)ar.AsyncState; del.EndInvoke(ar);}, DataReceived);
	//						DataReceived.BeginInvoke(this, new DataReceivedEventArgs(new IPEndPoint(any.Address,any.Port), buffer), null, null);
						}
					}
			}
		}
		
		public void Disconnect()
		{
			if (connected)
			{
				connected = false;
				if (receive)
				{
					worker.Join(10);
		            worker.Abort();
		            worker = null;
				}
				client.Client.Close();
			}
		}
		
		#region dispose
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if(!this.disposed)
            {
                if(disposing)
                {
                	Disconnect();
                }
                disposed = true;
            }
        }

        ~UdpServer()
        {
            Dispose(false);
        }
        #endregion
	}
}
