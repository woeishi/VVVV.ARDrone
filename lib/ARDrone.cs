using System;
using SlimDX;

namespace VVVV.Nodes.ARDrone
{
	public enum DroneStatus 
	{
		Invalid,
		NotConnected,
		Available,
		Connected,
		BatteryLow,
		Emergency,
		Ready,
		Flying
	}
	
	public class DroneStatusChangedEventArgs : EventArgs
	{
		public DroneStatusChangedEventArgs(DroneStatus CurrentStatus, DroneStatus FormerStatus)
		{
			this.Status = CurrentStatus;
			this.FormerStatus = FormerStatus;
		}
		public DroneStatus Status { get; private set;}
		public DroneStatus FormerStatus { get; private set;}
	}
	
	/// <summary>
	/// wrapper class for node connection handling in vvvv
	/// </summary>
	public class FlyCommand
	{
		private ARDrone drone;
		public FlyCommand(ARDrone Drone)
		{
			drone = Drone;
		}
		public DroneStatus DroneStatus { get { return drone.Status; } }
		public Commander Commander { get { return drone.Commander; } }
		public Positioner Positioner { get { return drone.Positioner; } }
		public Target Targeter { get { return drone.Targeter; } }
	}
	
	/// <summary>
	/// Description of ARDrone.
	/// </summary>
	public class ARDrone : IDisposable
	{
		public string debug = "";
		
		private System.Net.IPAddress ip;
		public System.Net.IPAddress IPAddress { get { return ip; } }
		public string IP { get { return ip.ToString(); } }
		
		private DroneStatus status;
		public DroneStatus Status
		{
			get { return status; }
			set 
			{ 
				DroneStatus formerStatus = status;
				status = value;
				if ((int)formerStatus != (int)status)
					OnStatusChanged(new DroneStatusChangedEventArgs(status, formerStatus));
			}
		}
		public event EventHandler<DroneStatusChangedEventArgs> StatusChanged;
		protected virtual void OnStatusChanged(DroneStatusChangedEventArgs e)
		{
			EventHandler<DroneStatusChangedEventArgs> handler = StatusChanged;
			if (handler != null)
            	handler(this, e);
		}

		
		private Pinger ping;
		
		private NavData navData;
		public NavData NavData { get { return navData; } }
		
		private Commander commander;
		public Commander Commander { get { return commander; } }
		
		private Configurator configurator;
		public Configurator Configurator { get { return configurator; } }
		
		private Positioner positioner;
		public Positioner Positioner { get { return positioner; } }
		
		private Target targeter;
		public Target Targeter { get { return targeter; } }
		
		private FlyCommand flyCommand;
		public FlyCommand FlyCommands { get { return flyCommand; } }
			
		private bool disposed = false;
		
		public ARDrone(string IP)
		{
			status = DroneStatus.Invalid;
			if (System.Net.IPAddress.TryParse(IP, out ip))
			{
				ping = new Pinger(this);
				this.Status = DroneStatus.NotConnected;
				
				navData = new NavData(this);
				configurator = new Configurator(this);
				
				commander = new Commander(this);
				positioner = new Positioner(this);
				targeter = new Target(this);
				
				flyCommand = new FlyCommand(this);
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
                	if (ping != null)
                		ping.Dispose();
//					if (navData != null)
//                		navData.Disconnect();
//					if (commander != null)
//                		commander.Disconnect();
					if (configurator != null)
						configurator.Disconnect();
                }
                disposed = true;
            }
        }

        ~ARDrone()
        {
            Dispose(false);
        }
        #endregion
	}
}
