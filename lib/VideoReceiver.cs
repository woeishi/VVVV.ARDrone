using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

using Wilke.Interactive.Drone.Control;


namespace VVVV.Nodes.ARDrone
{	
	/// <summary>
	/// Description of VideoReceiver.
	/// </summary>
	/// 
	public class VideoReceiver
	{
		private const int port = 5555;
		
		private ARDrone drone;
		private TcpClient client;
		
		private VideoImage videoImage;
		
		private Thread worker;
		private volatile bool connected;
		
		public event EventHandler<DataReceivedEventArgs> DataReceived;
		protected virtual void OnDataReceived(DataReceivedEventArgs e)
		{
			EventHandler<DataReceivedEventArgs> handler = DataReceived;
			if (handler != null)
            	handler(this, e);
		}
		
		public event EventHandler<ImageCompleteEventArgs> ImageComplete;
		
		public VideoReceiver(ARDrone Drone)
		{
			this.drone = Drone;
			this.drone.StatusChanged += new EventHandler<DroneStatusChangedEventArgs>(DroneStatusChanged);
			
			videoImage = new VideoImage();
			videoImage.ImageComplete += new EventHandler<ImageCompleteEventArgs>(videoImage_ImageComplete);
			connected = false;
			this.DataReceived += new EventHandler<DataReceivedEventArgs>(VideoReceiver_DataReceived);
		}

		void VideoReceiver_DataReceived(object sender, DataReceivedEventArgs e)
		{
//			try
//			{
			videoImage.AddImageStream(e.Data);
//			}
//			catch (Exception ex)
//			{
//				drone.debug += ex.Message;
//			}
		}

		private void videoImage_ImageComplete(object sender, ImageCompleteEventArgs e)
		{
			 if (ImageComplete != null)
            {
                ImageComplete(this, e);
            }
		}

		private void DroneStatusChanged(object sender, DroneStatusChangedEventArgs e)
		{
			if((int)e.FormerStatus < (int)DroneStatus.Available)
			{
				if ((int)drone.Status >= (int)DroneStatus.Available)
					Initialize();
			}
		}
		
		private void Initialize()
		{
			this.Connect();
		}
		
		public void Connect()
		{
			if (!connected)
			{
				connected = true;
				client = new TcpClient();
				client.ExclusiveAddressUse = false;
				client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				client.Connect(drone.IPAddress, port);
				
				worker = new Thread(ReceiveWorker);
				worker.Start();
			}
		}
		
		private void ReceiveWorker()
		{
			while (this.connected)
			{
				if (client.Available >0)
				{
					byte[] buffer = new byte[client.Available];
					client.GetStream().Read(buffer, 0, buffer.Length);
					OnDataReceived(new DataReceivedEventArgs(new IPEndPoint(IPAddress.Any,port), buffer));
//					DataReceived.BeginInvoke(this, new DataReceivedEventArgs(new IPEndPoint(IPAddress.Any,port), buffer), ar => { var del = (EventHandler<DataReceivedEventArgs>)ar.AsyncState; del.EndInvoke(ar);}, DataReceived);
					//videoImage.AddImageStream(buffer);
				}
			}
		}
		
		public void Disconnect()
		{
			if (connected)
			{
				connected = false;
				
				worker.Join(10);
		        worker.Abort();
		        worker = null;
		        
				client.Client.Close();
			}
		}
	}
}
