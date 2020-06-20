﻿/*
  Box2D.NetStandard Copyright © 2020 Ben Ukhanov & Hugh Phoenix-Hulme https://github.com/benzuk/box2d-netstandard
  Box2DX Copyright (c) 2009 Ihar Kalasouski http://code.google.com/p/box2dx
  
// MIT License

// Copyright (c) 2019 Erin Catto

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
*/


using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Box2D.NetStandard.Common;
using Box2D.NetStandard.Dynamics.Bodies;

namespace Box2D.NetStandard.Dynamics.Joints
{
	public enum JointType
	{
		UnknownJoint,
		RevoluteJoint,
		PrismaticJoint,
		DistanceJoint,
		PulleyJoint,
		MouseJoint,
		GearJoint,
		LineJoint,
		WheelJoint
	}

	public enum LimitState
	{
		InactiveLimit,
		AtLowerLimit,
		AtUpperLimit,
		EqualLimits
	}

	public struct Jacobian
	{
		public Vector2 Linear1;
		public float Angular1;
		public Vector2 Linear2;
		public float Angular2;

		public void SetZero()
		{
			Linear1=Vector2.Zero; Angular1 = 0.0f;
			Linear2=Vector2.Zero; Angular2 = 0.0f;
		}

		public void Set(Vector2 x1, float a1, Vector2 x2, float a2)
		{
			Linear1 = x1; Angular1 = a1;
			Linear2 = x2; Angular2 = a2;
		}

		public float Compute(Vector2 x1, float a1, Vector2 x2, float a2)
		{
			return Vector2.Dot(Linear1, x1) + Angular1 * a1 + Vector2.Dot(Linear2, x2) + Angular2 * a2;
		}
	}

#warning "CAS"
	/// <summary>
	/// A joint edge is used to connect bodies and joints together
	/// in a joint graph where each body is a node and each joint
	/// is an edge. A joint edge belongs to a doubly linked list
	/// maintained in each attached body. Each joint has two joint
	/// nodes, one for each attached body.
	/// </summary>
	public class JointEdge
	{
		/// <summary>
		/// Provides quick access to the other body attached.
		/// </summary>
		public Body other;

		/// <summary>
		/// The joint.
		/// </summary>
		public Joint joint;

		/// <summary>
		/// The previous joint edge in the body's joint list.
		/// </summary>
		public JointEdge Prev;

		/// <summary>
		/// The next joint edge in the body's joint list.
		/// </summary>
		public JointEdge next;
	}

#warning "CAS"
	/// <summary>
	/// Joint definitions are used to construct joints.
	/// </summary>
	public class JointDef
	{
		public JointDef()
		{
			Type = JointType.UnknownJoint;
			UserData = null;
			BodyA = null;
			BodyB = null;
			CollideConnected = false;
		}

		/// <summary>
		/// The joint type is set automatically for concrete joint types.
		/// </summary>
		public JointType Type;

		/// <summary>
		/// Use this to attach application specific data to your joints.
		/// </summary>
		public object UserData;

		/// <summary>
		/// The first attached body.
		/// </summary>
		public Body BodyA;

		/// <summary>
		/// The second attached body.
		/// </summary>
		public Body BodyB;

		/// <summary>
		/// Set this flag to true if the attached bodies should collide.
		/// </summary>
		public bool CollideConnected;
	}

	/// <summary>
	/// The base joint class. Joints are used to constraint two bodies together in
	/// various fashions. Some joints also feature limits and motors.
	/// </summary>
	public abstract class Joint
	{
		protected JointType _type;
		internal Joint _prev;
		internal Joint _next;
		internal JointEdge _edgeA = new JointEdge();
		internal JointEdge _edgeB = new JointEdge();
		internal Body _bodyA;
		internal Body _bodyB;

		internal bool _islandFlag;
		internal bool _collideConnected;

		protected object _userData;

		// Cache here per time step to reduce cache misses.
		protected Vector2 _localCenter1, _localCenter2;
		protected float _invMass1, _invI1;
		protected float _invMass2, _invI2;

		/// <summary>
		/// Get the type of the concrete joint.
		/// </summary>
		public JointType Type => _type;
		

		/// <summary>
		/// Get the first body attached to this joint.
		/// </summary>
		/// <returns></returns>
		public Body GetBodyA()
		{
			return _bodyA;
		}

		/// <summary>
		/// Get the second body attached to this joint.
		/// </summary>
		/// <returns></returns>
		public Body GetBodyB()
		{
			return _bodyB;
		}

		/// <summary>
		/// Get the anchor point on body1 in world coordinates.
		/// </summary>
		/// <returns></returns>
		public abstract Vector2 Anchor1 { get; }

		/// <summary>
		/// Get the anchor point on body2 in world coordinates.
		/// </summary>
		/// <returns></returns>
		public abstract Vector2 Anchor2 { get; }

		/// <summary>
		/// Get the reaction force on body2 at the joint anchor.
		/// </summary>		
		public abstract Vector2 GetReactionForce(float inv_dt);

		/// <summary>
		/// Get the reaction torque on body2.
		/// </summary>		
		public abstract float GetReactionTorque(float inv_dt);

		/// <summary>
		/// Get the next joint the world joint list.
		/// </summary>
		/// <returns></returns>
		public Joint GetNext()
		{
			return _next;
		}

		/// <summary>
		/// Get/Set the user data pointer.
		/// </summary>
		/// <returns></returns>
		public object UserData
		{
			get { return _userData; }
			set { _userData = value; }
		}

		protected Joint(JointDef def)
		{
			_type = def.Type;
			_prev = null;
			_next = null;
			_bodyA = def.BodyA;
			_bodyB = def.BodyB;
			_collideConnected = def.CollideConnected;
			_islandFlag = false;
			_userData = def.UserData;
		}

		internal static Joint Create(JointDef def)
		{
			Joint joint = null;

			switch (def.Type) {
				case JointType.DistanceJoint:
					joint = new DistanceJoint((DistanceJointDef) def);
					break;
				case JointType.MouseJoint:
					joint = new MouseJoint((MouseJointDef) def);
					break;
				case JointType.PrismaticJoint:
					joint = new PrismaticJoint((PrismaticJointDef) def);
					break;
				case JointType.RevoluteJoint:
					joint = new RevoluteJoint((RevoluteJointDef) def);
					break;
				case JointType.PulleyJoint:
					joint = new PulleyJoint((PulleyJointDef) def);
					break;
				case JointType.GearJoint:
					joint = new GearJoint((GearJointDef) def);
					break;
				// case JointType.LineJoint: {
				// 	joint = new LineJoint((LineJointDef) def);
				// }
				// 	break;
				case JointType.WheelJoint:
					joint = new WheelJoint((WheelJointDef) def);
					break;

				default:
					Debug.Assert(false);
					break;
			}

			return joint;
		}

		internal static void Destroy(Joint joint)
		{
			joint = null;
		}

		internal abstract void InitVelocityConstraints(in SolverData data);
		internal abstract void SolveVelocityConstraints(in SolverData data);

		// This returns true if the position errors are within tolerance.
		internal abstract bool SolvePositionConstraints(in SolverData data);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void ComputeXForm(ref Transform xf, Vector2 center, Vector2 localCenter, float angle)
		{
			xf.q = Matrix3x2.CreateRotation(angle); // .Set(angle);
			xf.p = center - Math.Mul(xf.q, localCenter);
		}
	}
}
