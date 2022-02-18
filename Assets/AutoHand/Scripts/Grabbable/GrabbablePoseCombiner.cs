using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Autohand{
    public class GrabbablePoseCombiner : MonoBehaviour{
        public float positionWeight = 1;
        public float rotationWeight = 1;
        public GrabbablePose[] poses;

        HandPoseData pose;

        public void Start() {
            poses = GetComponents<GrabbablePose>();
        }

        public bool CanSetPose(Hand hand) {
            foreach(var pose in poses) {
                if(pose.CanSetPose(hand))
                    return true;
            }
            return false;
        }

        public GrabbablePose GetClosestPose(Hand hand, Grabbable grab){
            if(this.poses.Length == 0)
                Debug.LogError("AUTO HAND: No poses connected to multi pose", gameObject);

            List<GrabbablePose> poses = new List<GrabbablePose>();
            foreach(var handPose in this.poses)
                if(handPose.CanSetPose(hand))
                    poses.Add(handPose);
            
            float closestValue = float.MaxValue;
            int closestIndex = 0;

            var pregrabPos = hand.transform.position;
            var pregrabRot = hand.transform.rotation;

            var tempContainer = AutoHandExtensions.transformRuler;
            tempContainer.rotation = Quaternion.identity;
            tempContainer.position = grab.transform.position;
            tempContainer.localScale = grab.transform.lossyScale;

            var handMatch = AutoHandExtensions.transformRulerChild;
            handMatch.position = hand.transform.position;
            handMatch.rotation = hand.transform.rotation;

            for (int i = 0; i < poses.Count; i++){
                pose = poses[i].GetHandPoseData(hand);

                handMatch.localPosition = pose.handOffset;
                handMatch.localRotation = pose.localQuaternionOffset;

                var distance = Vector3.Distance(handMatch.position, pregrabPos);
                var angleDistance = Quaternion.Angle(handMatch.rotation, pregrabRot) / 90f;

                var closenessValue = distance * positionWeight + angleDistance * rotationWeight;
                if(closenessValue < closestValue) {
                    closestIndex = i;
                    closestValue = closenessValue;
                }

                hand.transform.position = pregrabPos;
                hand.transform.rotation = pregrabRot;
            }

            return poses[closestIndex];
        }
    }
}
