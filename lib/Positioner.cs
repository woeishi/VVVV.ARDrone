/*
 * Created by SharpDevelop.
 * User: woeishi
 * Date: 28.01.2013
 * Time: 19:47
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using SlimDX;

namespace VVVV.Nodes.ARDrone
{
	/// <summary>
	/// Description of Positioner.
	/// </summary>
	public class Positioner
	{
		private ARDrone drone;
		
		private int queueCount;
		private Vector3 levelPos;
		private Queue<Vector3> posQueue;
		private Vector3 meanPos;
		public Vector3 PositionBySensor { get { return meanPos; } }
		
		private Vector3 inertialPos;
		public Vector3 InertialPosition { get { return inertialPos; } }
		
		private Matrix levelRot;
		public Matrix Rotation
		{
			get { return this.drone.NavData.Rotation * Matrix.Invert(levelRot); }
		}
		
		public Positioner(ARDrone Drone)
		{
			this.drone = Drone;
			queueCount = 10;
			posQueue = new Queue<Vector3>(queueCount);
			meanPos = Vector3.Zero;
			levelPos = new Vector3();
			levelRot = new Matrix();
			
			this.drone.NavData.NavDataReceived += new EventHandler(Positioner_NavDataReceived);
		}

		void Positioner_NavDataReceived(object sender, EventArgs e)
		{
			//averaging positions reported by sensor
			if (posQueue.Count >= queueCount)
				posQueue.Dequeue();
			posQueue.Enqueue(drone.NavData.Position - levelPos);
			
			Vector3 ret = Vector3.Zero;
			foreach (Vector3 v in posQueue)
				ret += v;
			meanPos = ret/posQueue.Count; 
			
			//integrating inertial position
			inertialPos += (drone.NavData.Body.Velocity/1000.0f)*(drone.NavData.DeltaTime/1000.0f);
		}
		
		public void SetPositionBySensor(Vector3 Position, Vector3 Rotation)
		{
			posQueue.Clear();
			levelPos = this.drone.NavData.Position-Position;
			Matrix inRot = new Matrix();
			Matrix.RotationYawPitchRoll(Rotation.Y, Rotation.X, Rotation.Z, out inRot);
			levelRot = this.drone.NavData.Rotation * Matrix.Invert(inRot);
		}
		
		public void SetInertialPosition(Vector3 Position)
		{
			inertialPos = Position;
		}
	}
}
