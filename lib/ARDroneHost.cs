/*
 * Created by SharpDevelop.
 * User: woeishi
 * Date: 31.01.2013
 * Time: 16:19
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;

namespace VVVV.Nodes.ARDrone
{
	/// <summary>
	/// Description of ARDroneHost.
	/// </summary>
	public class ARDroneHost : IDisposable
	{
		public string debug;
		
		private IPAddress localIP;
		private string macAddress;
		private bool validNetwork;
		
		private UdpServer navDataServer;
		private UdpServer commandServer;
		
		private List<ARDrone> drones;
		public ARDrone this[int i]
		{ 
			get { return drones[i]; }
			set { RemoveAt(i); InsertAt(i, value); }
		}
		public int Count { get { return drones.Count; } }
		
		private bool disposed = false;
		
		public ARDroneHost()
		{
			debug = string.Empty;
			
			validNetwork = GetWifiConfig();
			NetworkChange.NetworkAddressChanged += new NetworkAddressChangedEventHandler(NetworkChange_NetworkAddressChanged);
			
			drones = new List<ARDrone>();
			navDataServer = new UdpServer(5554, true);
			navDataServer.SendError += delegate(object sender, IssueSendEventArgs e) { debug += e.Destination.Address.ToString()+": "+BitConverter.ToInt32(e.Data,0).ToString(); };
			navDataServer.SetLocalIP(localIP, validNetwork);
			
			commandServer = new UdpServer(5556);
			commandServer.SetLocalIP(localIP, validNetwork);
			
			//ndServer.DataReceived += new EventHandler<DataReceivedEventArgs>(ndServer_DataReceived);
		}

		private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
		{
			validNetwork = GetWifiConfig();
			navDataServer.SetLocalIP(localIP, validNetwork);
			commandServer.SetLocalIP(localIP, validNetwork);
			foreach (ARDrone drone in drones)
			{
				drone.Status = DroneStatus.NotConnected;
				drone.Configurator.Disconnect();
			}
		}

		private void navDataReceived(object sender, DataReceivedEventArgs e)
		{
			for (int i=0; i<drones.Count; i++)
				drones[i].NavData.DataReceived(sender, e);
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
                	navDataServer.Dispose();
                	commandServer.Dispose();
                	foreach(ARDrone d in drones)
                		d.Dispose();
                }
                disposed = true;
            }
        }

        ~ARDroneHost()
        {
            Dispose(false);
        }
        #endregion

		public void RemoveAt(int i)
		{
			if (i < drones.Count)
			{
				navDataServer.RemoveRemotes(drones[i].IPAddress);
				commandServer.RemoveRemotes(drones[i].IPAddress);
				drones[i].Dispose();
				drones.RemoveAt(i);
			}
		}
		public void InsertAt(int i, ARDrone drone)
		{
			if (i<0)
				drones.Add(drone);
			else
				drones.Insert(i, drone);
			
			navDataServer.AddRemotes(drone.IPAddress);
			navDataServer.DataReceived += new EventHandler<DataReceivedEventArgs>(drone.NavData.DataReceived);
			commandServer.AddRemotes(drone.IPAddress);
			drone.NavData.IssueInit += new EventHandler<IssueSendEventArgs>(navDataServer.IssueMessage);
			drone.Commander.IssueCommand += new EventHandler<IssueSendEventArgs>(commandServer.IssueMessage);
		}
		
		private bool GetWifiConfig()
		{
			bool newProperties = false;
			foreach (NetworkInterface net in NetworkInterface.GetAllNetworkInterfaces())
			{
				if ((int)net.OperationalStatus == (int)OperationalStatus.Up && net.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
				{
					foreach (var addr in net.GetIPProperties().UnicastAddresses)
					{
						if (addr.IsDnsEligible)
						{
							localIP = addr.Address;
							
							byte[] macBytes = net.GetPhysicalAddress().GetAddressBytes();
							macAddress = "";
							for (int i=0; i<macBytes.Length; i++)
							{
								if (i>0)
									macAddress+=":";
								macAddress+=macBytes[i].ToString("X2");
							}
							newProperties = true;
						}
					}
				}
			}
			return newProperties;
		}
	}
}
