using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Autohand {
    [DefaultExecutionOrder(10)]
    public class HandProjector : MonoBehaviour
    {
        [Header("References")]
        public Hand hand;
        [Tooltip("This should be a copy of the hand with the desired visual setup for your projection hand")]
        public Hand handProjection;
        [Tooltip("If true everything in the handVisuals will be disabled/hidden when projection hand is showing")]
        public bool hideHand;
        [ShowIf("hideHand")]
        [Tooltip("The Object(s) under your Hand that contain the MeshRenderer Component(s)")]
        public Transform[] handVisuals;

        public float speed = 15f;

        [Header("Events")]
        public UnityHandGrabEvent OnStartProjection;
        public UnityHandGrabEvent OnEndProjection;

        const int REQUIRED_TARGET_FRAMES = 5;

        HandPoseData lastProjectionPose;
        HandPoseData newProjectionPose;
        bool lastFrameDidProjection = false;
        Vector3 lastProjectionPosition;
        Quaternion lastProjectionRotation;

        Grabbable target;
        Grabbable lastTarget;
        RaycastHit targetHit;
        RaycastHit currHit;
        int targetFrames;
        int newTargetFrames;

        void OnEnable(){
            handProjection.GetComponent<Rigidbody>().detectCollisions = false;
            handProjection.GetComponent<Rigidbody>().mass = 0;

            handProjection.followPositionStrength = 0;
            handProjection.followRotationStrength = 0;
            handProjection.swayStrength = 0;
            handProjection.disableIK = true;
            handProjection.usingHighlight = false;
            handProjection.usingPoseAreas = false;


            hand.OnBeforeGrabbed += OnGrab;
        }

        void OnDisable(){
            hand.OnBeforeGrabbed -= OnGrab;
        }

        void OnGrab(Hand hand, Grabbable grab){

            if (hideHand && handProjection.gameObject.activeInHierarchy){
                hand.SetHeldPose(handProjection.GetHandPose(), grab, false);
                hand.transform.position = handProjection.transform.position;
                hand.body.position = hand.transform.position;
                hand.transform.rotation = handProjection.transform.rotation;
                hand.body.rotation = hand.transform.rotation;
            }

            ShowProjection(false);
        }

        void LateUpdate(){
            SetTarget(hand.lookingAtObj);
            ShowProjection(IsProjectionActive());
        }


        void OnProjectionStart(Hand projectionHand, Grabbable lookingAtObj)
        {
            handProjection.transform.localPosition = hand.transform.localPosition;
            handProjection.transform.localRotation = hand.transform.localRotation;
            lastProjectionPosition = handProjection.transform.localPosition;
            handProjection.body.position = handProjection.transform.position;
            lastProjectionRotation = handProjection.transform.localRotation;
            handProjection.body.rotation = handProjection.transform.rotation;
            OnStartProjection?.Invoke(projectionHand, lookingAtObj); 
            currHit = hand.GetHighlightHit();
            targetHit = currHit;
            lastTarget = target;
        }


        void OnProjectionEnd(Hand projectionHand, Grabbable lookingAtObj)
        {
            lastFrameDidProjection = false;
            OnEndProjection?.Invoke(projectionHand, lookingAtObj);
        }


        void ShowProjection(bool show){
            //Found target
            if (show && target == null && !handProjection.gameObject.activeSelf){
                handProjection.SetHandLocation(hand.transform.position, hand.transform.rotation);
                OnProjectionStart(handProjection, target);
            }
            //New target 
            else if(show && target != null && lastTarget != target){
                OnProjectionEnd(handProjection, lastTarget);
                OnProjectionStart(handProjection, target);
            }
            //No target
            else if (!show && handProjection.gameObject.activeSelf){
                OnProjectionEnd(handProjection, lastTarget);
            }

            handProjection.gameObject.SetActive(show);

            if (hideHand){
                for (int i = 0; i < handVisuals.Length; i++)
                    handVisuals[i].gameObject.SetActive(!show);
            }

            handProjection.transform.localPosition = hand.transform.localPosition;
            handProjection.transform.localRotation = hand.transform.localRotation;

            if (show){

                if (!hand.CanGrab(target)){
                    ShowProjection(false);
                    return;
                }

                if (handProjection.GetGrabPose(targetHit.collider.transform, target, out var grabPose, out var relativeTo)){
                    grabPose.SetHandPose(handProjection);
                }
                else{
                    handProjection.transform.position -= handProjection.palmTransform.forward * 0.1f;
                    handProjection.AutoPose(currHit, target);
                }

                newProjectionPose = handProjection.GetHandPose();

                if (lastFrameDidProjection){
                    handProjection.SetHandPose(HandPoseData.LerpPose(lastProjectionPose, newProjectionPose, speed * Time.unscaledDeltaTime));
                    handProjection.transform.localPosition = Vector3.Lerp(lastProjectionPosition, handProjection.transform.localPosition, speed * Time.unscaledDeltaTime);
                    handProjection.transform.localRotation = Quaternion.Lerp(lastProjectionRotation, handProjection.transform.localRotation, speed * Time.unscaledDeltaTime);
                }

                lastProjectionPose = handProjection.GetHandPose();
                lastProjectionPosition = handProjection.transform.localPosition;
                lastProjectionRotation = handProjection.transform.localRotation;
                lastFrameDidProjection = true;
            }
            else{
                lastProjectionPosition = handProjection.transform.localPosition;
                lastProjectionRotation = handProjection.transform.localRotation;
                lastFrameDidProjection = false;
            }
        }


        void SetTarget(Grabbable newTarget){
            if(hand.holdingObj != null){
                target = null;
                targetFrames = 0;
                newTargetFrames = 0;
            }


            if (newTarget == null){
                targetFrames--;
                newTargetFrames--;
            }
            else if (target == null || target == newTarget){ 
                targetFrames++;
                newTargetFrames--;
            }
            else if (target != newTarget)
                newTargetFrames++;


            if (targetFrames == REQUIRED_TARGET_FRAMES){
                target = newTarget;
            }
            else if (targetFrames == 0) {
                target = null;
                lastFrameDidProjection = false;
            }


            if (newTargetFrames == REQUIRED_TARGET_FRAMES){
                target = newTarget;
            }

            if (target != null && hand.lookingAtObj != null)
            {
                targetHit = hand.GetHighlightHit();
                currHit.point = Vector3.Lerp(currHit.point, targetHit.point, speed * Time.unscaledDeltaTime);
                currHit.normal = Vector3.Lerp(currHit.normal, targetHit.normal, speed * Time.unscaledDeltaTime);
            }


            targetFrames = Mathf.Clamp(targetFrames, 0, REQUIRED_TARGET_FRAMES);
            newTargetFrames = Mathf.Clamp(newTargetFrames, 0, REQUIRED_TARGET_FRAMES);
        }


        bool IsProjectionActive(){
            return target != null && hand.holdingObj == null;
        }

    }
}