/*
 * Created by SharpDevelop.
 * User: woeishi
 * Date: 23.01.2013
 * Time: 18:52
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;

namespace VVVV.Nodes.ARDrone
{
	/// <summary>
	/// Description of Pinger.
	/// </summary>
	public class Pinger : IDisposable
	{
		private ARDrone host;
		private Thread thread;
		private volatile bool work;
		private volatile bool checkPing;
		public bool Working { get { return checkPing; } }
		
		private bool disposed = false;
		
		public Pinger(ARDrone Host)
		{
			this.host = Host;
			this.host.StatusChanged += new EventHandler<DroneStatusChangedEventArgs>(Host_StatusChanged);
			work = true;
		}

		private void Host_StatusChanged(object sender, DroneStatusChangedEventArgs e)
		{
			if ((int)host.Status == (int)DroneStatus.NotConnected)
				StartPinging();
		}
		
		public void StartPinging()
		{
			checkPing = true;
			if (thread == null)
			{
				thread = new Thread(DoPings);
				thread.Start();
			}
		}
		
		private void DoPings()
		{
			while (work)
			{
				Thread.Sleep(250);
				while (checkPing)
				{
					try
					{
						Ping ping = new Ping();
						PingReply reply = ping.Send(host.IPAddress);
						
						if (reply.Status == IPStatus.Success)
						{
							checkPing = false;
							if ((int)host.Status <= (int)DroneStatus.NotConnected)
									host.Status = DroneStatus.Available;
						}
						ping.Dispose();
					}
					catch
					{
						/* ignore not pingable
						if ((int)host.Status == (int)DroneStatus.NotConnected)
							host.Status = DroneStatus.Invalid; */
					}
					Thread.Sleep(250);
				}
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
                	checkPing = false;
                	work = false;
//                	thread.Join(500);
                	thread.Abort();
                	thread = null;
                }
                disposed = true;
            }
        }

        ~Pinger()
        {
            Dispose(false);
        }
        #endregion
		
	}
}
