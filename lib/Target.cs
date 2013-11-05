/*
 * Created by SharpDevelop.
 * User: woeishi
 * Date: 28.01.2013
 * Time: 22:57
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Threading;
using SlimDX;

namespace VVVV.Nodes.ARDrone
{
	/// <summary>
	/// Description of Target.
	/// </summary>
	public class Target
	{
		private ARDrone drone;
		
		private Thread worker;
		private volatile bool work;
		private volatile bool follow;
		private volatile bool useInertial;
		public bool UseInertialPosition 
		{
			get { return useInertial; }
			set { useInertial = value; }
		}
		private Quaternion dstRotQ;
		private Vector3 dstPos;
		private float speed;
		
		public Target(ARDrone Drone)
		{
			this.drone = Drone;
			work = false;
			follow = false;
			dstRotQ = Quaternion.Identity;
			dstPos = Vector3.Zero;
			speed = 0.5f;
			
			worker = new Thread(FollowTarget);
			work = true;
			worker.Start();
		}
		
		public void ReleaseTarget()
		{
			follow = false;
		}
		
		public void SetTarget(Vector3 Position, Vector3 Rotation, float Speed)
		{
			dstRotQ = Quaternion.RotationYawPitchRoll(Rotation.Y,0,0);
			dstPos = Position;
			speed = Speed;
			follow = true;
		}
		
		private void FollowTarget()
		{
			while (work)
			{
				while (follow)
				{
					Vector3 sc = new Vector3();
					Vector3 tr = new Vector3();
					Quaternion srcRotQ = new Quaternion();
					drone.Positioner.Rotation.Decompose(out sc, out srcRotQ, out tr);
					Quaternion dRotQ = Quaternion.Multiply(Quaternion.Invert(srcRotQ),dstRotQ);
					float dY = Math.Sign(dRotQ.Axis.Y)*dRotQ.Angle;
					Vector3 srcPos = drone.Positioner.PositionBySensor;
					if (useInertial)
						srcPos = drone.Positioner.InertialPosition;
					Vector3 dPos = dstPos - srcPos;
					//do some damping near target
					dPos /= 1000.0f;
					dPos.X *= dPos.X*Math.Sign(dPos.X);
					dPos.Y *= dPos.Y*Math.Sign(dPos.Y);
					dPos.Z *= dPos.Z*Math.Sign(dPos.Z);
					Vector3 dVal = Vector3.Clamp(dPos*speed, new Vector3(-1), new Vector3(1));
//					host.Commander.MoveSend(dVal.X, dVal.Y, dVal.Z, Math.Max(-amount,Math.Min(amount,dY)));
					drone.Commander.MoveSend(dVal.X, dVal.Y, -dVal.Z, dY*speed);
				}
				Thread.Sleep(drone.NavData.DeltaTime);
			}
		}
	}
}
