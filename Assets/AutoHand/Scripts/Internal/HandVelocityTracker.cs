using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Autohand {
    public class HandVelocityTracker {
        HandBase hand = null;
        float minThrowVelocity = 0.25f;

        ///<summary> A list of all acceleration values from the time the throwing motion was detected til now.</summary>
        protected List<VelocityTimePair> m_ThrowVelocityList = new List<VelocityTimePair>();
        protected List<VelocityTimePair> m_ThrowFrameVelocityList = new List<VelocityTimePair>();
        protected List<VelocityTimePair> m_ThrowAngleVelocityList = new List<VelocityTimePair>();

        public void ClearThrow() {
            m_ThrowVelocityList.Clear();
            m_ThrowFrameVelocityList.Clear();
            m_ThrowAngleVelocityList.Clear();
        }

        public HandVelocityTracker(HandBase hand) {
            this.hand = hand;
        }

        public void UpdateFrameThrowing(Vector3 vel) {
            // Add current hand velocity to throw velocity list.
            m_ThrowFrameVelocityList.Add(new VelocityTimePair() { time = Time.time, velocity = vel });

            // Remove old entries from m_ThrowVelocityList.
            for(int i = m_ThrowFrameVelocityList.Count - 1; i >= 0; --i) {
                if(Time.time - m_ThrowFrameVelocityList[i].time >= hand.throwVelocityExpireTime) {
                    // Remove expired entry.
                    m_ThrowFrameVelocityList.RemoveAt(i);
                }
            }
        }

        public void UpdateThrowing() {
            if(hand.holdingObj == null || hand.IsGrabbing()) {
                if(m_ThrowVelocityList.Count > 0) {
                    m_ThrowVelocityList.Clear();
                    m_ThrowAngleVelocityList.Clear();
                }

                return;
            }

            // Add current hand velocity to throw velocity list.
            m_ThrowVelocityList.Add(new VelocityTimePair() { time = Time.time, velocity = hand.body.velocity });

            // Remove old entries from m_ThrowVelocityList.
            for(int i = m_ThrowVelocityList.Count - 1; i >= 0; --i) {
                if(Time.time - m_ThrowVelocityList[i].time >= hand.throwVelocityExpireTime) {
                    // Remove expired entry.
                    m_ThrowVelocityList.RemoveAt(i);
                }
            }

            // Add current hand velocity to throw velocity list.
            m_ThrowAngleVelocityList.Add(new VelocityTimePair() { time = Time.time, velocity = hand.holdingObj.body.angularVelocity });

            // Remove old entries from m_ThrowVelocityList.
            for(int i = m_ThrowAngleVelocityList.Count - 1; i >= 0; --i) {
                if(Time.time - m_ThrowAngleVelocityList[i].time >= hand.throwVelocityExpireTime) {
                    // Remove expired entry.
                    m_ThrowAngleVelocityList.RemoveAt(i);
                }
            }
        }

        /// <summary>Returns the hands velocity times its strength</summary>
        public Vector3 ThrowVelocity() {
            if(hand.IsGrabbing())
                return Vector3.zero;

            // Calculate the average hand velocity over the course of the throw.
            Vector3 averageVelocity = Vector3.zero;
            if(m_ThrowVelocityList.Count > 0) {
                foreach(VelocityTimePair pair in m_ThrowVelocityList) {
                    averageVelocity += pair.velocity;
                }
                averageVelocity /= m_ThrowVelocityList.Count;
            }
            else { averageVelocity = hand.body.velocity; }

            var vel = averageVelocity * hand.throwPower;

            averageVelocity = Vector3.zero;
            if(m_ThrowFrameVelocityList.Count > 0) {
                foreach(VelocityTimePair pair in m_ThrowFrameVelocityList) {
                    averageVelocity += pair.velocity;
                }
                averageVelocity /= m_ThrowFrameVelocityList.Count;
            }

            vel += averageVelocity * hand.throwPower;

            return vel.magnitude > minThrowVelocity ? vel : Vector3.zero;
        }

        /// <summary>Returns the hands velocity times its strength</summary>
        public Vector3 ThrowAngularVelocity() {
            if(hand.IsGrabbing())
                return Vector3.zero;

            // Calculate the average hand velocity over the course of the throw.
            Vector3 averageVelocity = Vector3.zero;
            if(m_ThrowAngleVelocityList.Count > 0) {
                foreach(VelocityTimePair pair in m_ThrowAngleVelocityList) {
                    averageVelocity += pair.velocity;
                }
                averageVelocity /= m_ThrowAngleVelocityList.Count;
            }

            averageVelocity *= Mathf.Sqrt(hand.throwPower) / 2f;

            return averageVelocity.magnitude > minThrowVelocity ? averageVelocity : Vector3.zero; ;
        }
    }
}