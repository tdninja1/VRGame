using NaughtyAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Autohand {
    [HelpURL("https://earnestrobot.notion.site/Hand-967e36c2ab2945b2b0f75cea84624b2f")]
    public class Hand : HandBase {


        [AutoToggleHeader("Enable Highlight", 0, 0, tooltip = "Raycasting for grabbables to highlight is expensive, you can disable it here if you aren't using it")]
        public bool usingHighlight = true;

        [EnableIf("usingHighlight")]
        [Tooltip("The layers to highlight and use look assist on --- Nothing will default on start")]
        public LayerMask highlightLayers;

        [EnableIf("usingHighlight")]
        [Tooltip("Leave empty for none - used as a default option for all grabbables with empty highlight material")]
        public Material defaultHighlight;


        [AutoToggleHeader("Show Advanced")]
        public bool showAdvanced = false;


        [ShowIf("showAdvanced")]
        [Tooltip("Whether the hand should go to the object and come back on grab, or the object to float to the hand on grab. Will default to HandToGrabbable for objects that have \"parentOnGrab\" disabled")]
        public GrabType grabType = GrabType.HandToGrabbable;

        [ShowIf("showAdvanced")]
        [Tooltip("The animation curve based on the grab time 0-1"), Min(0)]
        public AnimationCurve grabCurve;

        [ShowIf("showAdvanced")]
        [Tooltip("Whether or not the hands return on grab should only move when the controller follow moves")]
        public bool useSmoothReturn = false;
        [Tooltip("The hand with return X:1 based on controller movement")]
        public float smoothReturnSpeed = 1;

        [ShowIf("showAdvanced")]
        [Tooltip("This is used in conjunction with custom poses. For a custom pose to work it must has the same PoseIndex as the hand. Used for when your game has multiple hands")]
        public int poseIndex = 0;

        [AutoLine]
        public bool ignoreMe1;




#if UNITY_EDITOR
        bool editorSelected = false;
#endif


        public static string[] grabbableLayers = { "Grabbable", "Grabbing" };

        //The layer is used and applied to all grabbables in if the hands layer is set to default
        public static string grabbableLayerNameDefault = "Grabbable";
        //This helps the auto grab distinguish between what item is being grabbaed and the items around it
        public static string grabbingLayerName = "Grabbing";

        //This was added by request just in case you want to add different layers for left/right hand
        public static string rightHandLayerName = "Hand";
        public static string leftHandLayerName = "Hand";



        ///Events for all my programmers out there :)/// 
        /// <summary>Called when the grab event is triggered, event if nothing is being held</summary>
        public event HandGrabEvent OnTriggerGrab;
        /// <summary>Called at the very start of a grab before anything else</summary>
        public event HandGrabEvent OnBeforeGrabbed;
        /// <summary>Called when the hand grab connection is made (the frame the hand touches the grabbable)</summary>
	    public event HandGrabEvent OnGrabbed;

        /// <summary>Called when the release event is triggered, event if nothing is being held</summary>
        public event HandGrabEvent OnTriggerRelease;
        public event HandGrabEvent OnBeforeReleased;
        /// <summary>Called at the end the release</summary>
        public event HandGrabEvent OnReleased;

        /// <summary>Called when the squeeze button is pressed, regardless of whether an object is held or not (grab returns null)</summary>
        public event HandGrabEvent OnSqueezed;
        /// <summary>Called when the squeeze button is released, regardless of whether an object is held or not (grab returns null)</summary>
        public event HandGrabEvent OnUnsqueezed;

        /// <summary>Called when highlighting starts</summary>
        public event HandGrabEvent OnHighlight;
        /// <summary>Called when highlighting ends</summary>
        public event HandGrabEvent OnStopHighlight;

        /// <summary>Called whenever joint breaks or force release event is called</summary>
        public event HandGrabEvent OnForcedRelease;
        /// <summary>Called when the physics joint between the hand and the grabbable is broken by force</summary>
        public event HandGrabEvent OnGrabJointBreak;

        /// <summary>Legacy Event - same as OnRelease</summary>
        public event HandGrabEvent OnHeldConnectionBreak;

        public event HandGameObjectEvent OnHandCollisionStart;
        public event HandGameObjectEvent OnHandCollisionStop;
        public event HandGameObjectEvent OnHandTriggerStart;
        public event HandGameObjectEvent OnHandTriggerStop;

        List<HandTriggerAreaEvents> triggerEventAreas = new List<HandTriggerAreaEvents>();

        Coroutine tryGrab;
        Coroutine highlightRoutine;

        Coroutine _grabRoutine;
        Coroutine grabRoutine {
            get { return _grabRoutine; }
            set {
                if(value != null && _grabRoutine != null) {
                    StopCoroutine(_grabRoutine);
                    if(holdingObj != null) {
                        holdingObj.body.velocity = Vector3.zero;
                        holdingObj.body.angularVelocity = Vector3.zero;
                        holdingObj.beingGrabbed = false;
                    }
                    BreakGrabConnection();
                    grabbing = false;
                }
                _grabRoutine = value;
            }
        }


        protected override void Awake() {
            if(highlightLayers == 0)
                highlightLayers = LayerMask.GetMask(grabbableLayerNameDefault);

            handLayers = LayerMask.GetMask(rightHandLayerName, leftHandLayerName);

            base.Awake();

            if(enableMovement) {
                body.drag = 10;
                body.angularDrag = 35;
                body.useGravity = false;
            }


#if UNITY_EDITOR
            if(Selection.activeGameObject == gameObject) {
                Selection.activeGameObject = null;
                Debug.Log("Auto Hand: Selecting the grabbable can cause lag and quality reduction at runtime. (Automatically deselecting at runtime) Remove this code at any time.", this);
                editorSelected = true;
            }

            Application.quitting += () => { if(editorSelected) Selection.activeGameObject = gameObject; };
#endif
        }

        protected override void Start() {
            base.Start();
            SetLayer();
            highlightRoutine = StartCoroutine(HighlightUpdate(Time.fixedUnscaledDeltaTime * 4f));
        }

        protected override void OnEnable() {
            base.OnEnable();
            collisionTracker.OnCollisionFirstEnter += OnCollisionFirstEnter;
            collisionTracker.OnCollisionLastExit += OnCollisionLastExit;
            collisionTracker.OnTriggerFirstEnter += OnTriggerFirstEnter;
            collisionTracker.OnTriggeLastExit += OnTriggerLastExit;

            collisionTracker.OnCollisionFirstEnter += (collision) => { OnHandCollisionStart?.Invoke(this, collision); };
            collisionTracker.OnCollisionLastExit += (collision) => { OnHandCollisionStop?.Invoke(this, collision); };
            collisionTracker.OnTriggerFirstEnter += (collision) => { OnHandTriggerStart?.Invoke(this, collision); };
            collisionTracker.OnTriggeLastExit += (collision) => { OnHandTriggerStop?.Invoke(this, collision); };
        }

        protected override void OnDisable() {
            foreach(var trigger in triggerEventAreas)
                trigger.Exit(this);

            if(tryGrab != null)
                StopCoroutine(tryGrab);
            if(highlightRoutine != null)
                StopCoroutine(highlightRoutine);
            base.OnDisable();
        }


        protected virtual void Update() {

            if(holdingObj && !holdingObj.maintainGrabOffset) {
                if(useSmoothReturn) {
                    var deltaDist = Vector3.Distance(follow.position, lastFrameFollowPos);
                    var deltaRot = Quaternion.Angle(follow.rotation, lastFrameFollowRot);
                    grabPositionOffset = Vector3.MoveTowards(grabPositionOffset, Vector3.zero, deltaDist * smoothReturnSpeed);
                    grabRotationOffset = Quaternion.RotateTowards(grabRotationOffset, Quaternion.identity, deltaRot + deltaDist * 90);
                }
                else {
                    grabPositionOffset = Vector3.zero;
                    grabRotationOffset = Quaternion.identity;

                }
            }


            lastFrameFollowPos = follow.position;
            lastFrameFollowRot = follow.rotation;
        }


        //================== CORE INTERACTION FUNCTIONS ===================
        //================================================================
        //================================================================


        /// <summary>Function for controller trigger fully pressed -> Grabs whatever is directly in front of and closest to the hands palm</summary>
        public virtual void Grab() {
            var grabType = this.grabType;
            Grab(grabType);
        }


        /// <summary>Function for controller trigger fully pressed -> Grabs whatever is directly in front of and closest to the hands palm</summary>
        public virtual void Grab(GrabType grabType) {
            OnTriggerGrab?.Invoke(this, null);
            foreach(var triggerArea in triggerEventAreas) {
                triggerArea.Grab(this);
            }

            if(!grabbing && holdingObj == null) {
                if(HandClosestHit(out RaycastHit closestHit, out Grabbable grabbable, reachDistance, ~handLayers) != Vector3.zero && grabbable != null) {
                    var newGrabType = this.grabType;
                    if(grabbable.grabType != HandGrabType.Default)
                        newGrabType = grabbable.grabType == HandGrabType.GrabbableToHand ? GrabType.GrabbableToHand : GrabType.HandToGrabbable;
                    if(grabbable != null)
                        grabRoutine = StartCoroutine(GrabObject(closestHit, grabbable, newGrabType));
                }
            }
            else if(holdingObj != null && holdingObj.CanGetComponent(out GrabLock grabLock)) {
                grabLock.OnGrabPressed?.Invoke();
            }
        }

        /// <summary>Grabs based on raycast and grab input data</summary>
        public virtual void Grab(RaycastHit hit, Grabbable grab, GrabType grabType = GrabType.InstantGrab) {
            bool objectFree = grab.body.isKinematic != true && grab.body.constraints == RigidbodyConstraints.None && grab.parentOnGrab;
            if(!grabbing && holdingObj == null && this.CanGrab(grab) && objectFree) {

                var estimatedRadius = Vector3.Distance(hit.point, hit.transform.position);
                var difference = (grab.transform.position - hit.point) + (palmTransform.forward * estimatedRadius * 2f);
                var startPos = grab.transform.position;
                grab.transform.position = palmTransform.position + difference;
                grab.body.position = grab.transform.position;

                if(HandClosestHit(out RaycastHit closestHit, out Grabbable grabbable, estimatedRadius * 3f, LayerMask.GetMask(LayerMask.LayerToName(grab.gameObject.layer)), grab) != Vector3.zero) {
                    grabRoutine = StartCoroutine(GrabObject(closestHit, grabbable, grabType));
                }
                else if(grab != null) {
                    grab.transform.position = startPos;
                    grab.body.position = grab.transform.position;
                    grabRoutine = StartCoroutine(GrabObject(hit, grab, grabType));
                }
            }
        }

        /// <summary>Attempts grab on given grabbable</summary>
        public virtual void TryGrab(Grabbable grab) {
            if(!grabbing && holdingObj == null && this.CanGrab(grab)) {
                if(tryGrab != null)
                    StopCoroutine(tryGrab);
                tryGrab = StartCoroutine(TryGrab());
            }

            IEnumerator TryGrab() {
                RaycastHit closestHit;
                for(int i = 0; i < 5; i++) {
                    bool grabbed = false;
                    var distance = Vector3.Distance(palmTransform.position, grab.transform.position);
                    var mask = LayerMask.GetMask(LayerMask.LayerToName(grab.gameObject.layer));
                    if(HandClosestHit(out closestHit, out _, distance, mask, grab) != Vector3.zero) {
                        grabRoutine = StartCoroutine(GrabObject(closestHit, grab, GrabType.InstantGrab));
                        grabbed = true;
                        break;
                    }
                    else {
                        RaycastHit[] hits = Physics.RaycastAll(palmTransform.position, grab.transform.position - palmTransform.position, distance * 2, mask);
                        if(hits.Length > 0) {
                            foreach(var hit in hits) {
                                if(!grabbed && hit.transform.gameObject == grab.gameObject) {
                                    grabRoutine = StartCoroutine(GrabObject(hit, grab, GrabType.InstantGrab));
                                    grabbed = true;
                                    break;
                                }
                            }
                        }
                    }

                    if(grabbed) break;
                    yield return new WaitForSeconds(Time.fixedUnscaledDeltaTime * 2);
                }

                tryGrab = null;
            }
        }

        /// <summary>Attempts grab on given grabbable</summary>
        public virtual void TryGrab(Grabbable grab, bool imforbackwardscompatability) {
            TryGrab(grab);
        } 


        /// <summary>Function for controller trigger unpressed</summary>
        public virtual void Release() {
            OnTriggerRelease?.Invoke(this, null);
            foreach(var triggerArea in triggerEventAreas) {
                triggerArea.Release(this);
            }

            if(holdingObj && !holdingObj.wasForceReleased && holdingObj.CanGetComponent<GrabLock>(out _))
                return;

            if(holdingObj != null) {
                OnBeforeReleased?.Invoke(this, holdingObj);
                //Do the holding object calls and sets
                holdingObj?.OnRelease(this);
                OnHeldConnectionBreak?.Invoke(this, holdingObj);
                OnReleased?.Invoke(this, holdingObj);
            }

            BreakGrabConnection();
        }

        /// <summary>This will force release the hand without throwing or calling OnRelease\n like losing grip on something instead of throwing</summary>
        public virtual void ForceReleaseGrab() {
            if(holdingObj != null) {
                OnForcedRelease?.Invoke(this, holdingObj);
                holdingObj?.ForceHandRelease(this);
            }
        }

        /// <summary>Old function left for backward compatability -> Will release grablocks, recommend using ForceReleaseGrab() instead</summary>
        public virtual void ReleaseGrabLock() {
            ForceReleaseGrab();
        }

        /// <summary>Event for controller grip</summary>
        public virtual void Squeeze() {
            OnSqueezed?.Invoke(this, holdingObj);
            holdingObj?.OnSqueeze(this);

            foreach(var triggerArea in triggerEventAreas) {
                triggerArea.Squeeze(this);
            }
            squeezing = true;
        }

        /// <summary>Event for controller ungrip</summary>
        public virtual void Unsqueeze() {
            squeezing = false;
            OnUnsqueezed?.Invoke(this, holdingObj);
            holdingObj?.OnUnsqueeze(this);

            foreach(var triggerArea in triggerEventAreas) {
                triggerArea.Unsqueeze(this);
            }
        }

        /// <summary>Breaks the grab event</summary>
        public virtual void BreakGrabConnection(bool callEvent = true) {

            if(holdingObj != null) {
                if(squeezing)
                    holdingObj.OnUnsqueeze(this);

                if(grabbing) {
                    holdingObj.body.velocity = Vector3.zero;
                    holdingObj.body.angularVelocity = Vector3.zero;
                }

                foreach(var finger in fingers) {
                    finger.SetFingerBend(finger.GetLastHitBend());
                }

                holdingObj.BreakHandConnection(this);
                holdingObj = null;
            }

            grabbed = false;
            grabPose = null;
            lookingAtObj = null;
            grabPositionOffset = Vector3.zero;
            grabRotationOffset = Quaternion.identity;
            grabRoutine = null;

            if(heldJoint != null) {
                Destroy(heldJoint);
                heldJoint = null;
            }
        }

        /// <summary>Creates the grab connection</summary>
        public virtual void CreateGrabConnection(Grabbable grab, Vector3 handPos, Quaternion handRot, Vector3 grabPos, Quaternion grabRot, bool executeGrabEvents = false) {

            if(executeGrabEvents) {
                OnBeforeGrabbed?.Invoke(this, grab);
                grab.OnBeforeGrab(this);
            }

            transform.position = handPos;
            body.position = handPos;
            transform.rotation = handRot;
            body.rotation = handRot;

            grab.transform.position = grabPos;
            grab.body.position = grabPos;
            grab.transform.rotation = grabRot;
            grab.body.rotation = grabRot;

            grabPoint.parent = grab.transform;
            grabPoint.transform.position = handPos;
            grabPoint.transform.rotation = handRot;

            holdingObj = grab;
            if(!(holdingObj.grabType == HandGrabType.GrabbableToHand) && !(grabType == GrabType.GrabbableToHand)) {
                grabPositionOffset = transform.position - follow.transform.position;
                grabRotationOffset = Quaternion.Inverse(follow.transform.rotation) * transform.rotation;
            }


            //If it's a predetermined Pose
            GrabbablePose tempPose = null;
            if(holdingObj.CanGetComponent(out GrabbablePoseCombiner poseCombiner)) {
                if(poseCombiner.CanSetPose(this)) {
                    grabPose = poseCombiner.GetClosestPose(this, holdingObj);
                    grabPose.SetHandPose(this);
                }
            }
            else if(holdingObj.CanGetComponent(out tempPose)) {
                if(tempPose.CanSetPose(this)) {
                    grabPose = tempPose;
                    grabPose.SetHandPose(this);
                }
            }

            if(executeGrabEvents) {
                OnGrabbed?.Invoke(this, holdingObj);
                holdingObj.OnGrab(this);
            }

            CreateJoint(holdingObj, holdingObj.jointBreakForce * ((1f / Time.fixedUnscaledDeltaTime) / 60f), float.PositiveInfinity);
        }

        public virtual void OnJointBreak(float breakForce) {
            if(heldJoint != null) {
                Destroy(heldJoint);
                heldJoint = null;
            }
            if(holdingObj != null) {
                holdingObj.body.velocity /= 100f;
                holdingObj.body.angularVelocity /= 100f;
                OnGrabJointBreak?.Invoke(this, holdingObj);
                holdingObj?.OnHandJointBreak(this);
            }
        }


        //=============== HIGHLIGHT AND LOOK ASSIST ===================
        //=============================================================
        //=============================================================

        /// <summary>Manages the highlighting for grabbables</summary>
        protected virtual void UpdateHighlight() {
            if(usingHighlight && highlightLayers != 0 && holdingObj == null) {
                Vector3 dir = Vector3.zero;
                Grabbable newLookingAtObj = null;

                if(!holdingObj)
                    dir = HandClosestHit(out highlightHit, out newLookingAtObj, reachDistance, ~handLayers);

                //Zero means it didn't hit
                if(dir != Vector3.zero && (newLookingAtObj != null && newLookingAtObj.CanGrab(this))) {
                    //Changes look target
                    if(newLookingAtObj != lookingAtObj) {
                        //Unhighlights current target if found
                        if(lookingAtObj != null) {
                            OnStopHighlight?.Invoke(this, lookingAtObj);
                            lookingAtObj.Unhighlight(this);
                        }

                        lookingAtObj = newLookingAtObj;

                        //Highlights new target if found
                        OnHighlight?.Invoke(this, lookingAtObj);
                        lookingAtObj.Highlight(this);
                    }
                }
                //If it was looking at something but now it's not there anymore
                else if(newLookingAtObj == null && lookingAtObj != null) {
                    //Just in case the object your hand is looking at is destroyed
                    OnStopHighlight?.Invoke(this, lookingAtObj);
                    lookingAtObj.Unhighlight(this);

                    lookingAtObj = null;
                }
            }
        }

        /// <summary>Returns the closest raycast hit from the hand's highlighting system, if no highlight, returns blank raycasthit</summary>
        public RaycastHit GetHighlightHit() { return highlightHit; }




        //======================== GETTERS AND SETTERS ====================
        //=================================================================
        //=================================================================

        /// <summary>Takes a raycasthit and grabbable and automatically poses the hand</summary>
        public void AutoPose(RaycastHit hit, Grabbable grabbable) {
            var grabbableLayer = grabbable.gameObject.layer;
            var grabbingLayer = LayerMask.NameToLayer(Hand.grabbingLayerName);
            grabbable.SetLayerRecursive(grabbable.body.transform, grabbingLayer);

            Vector3 palmLocalPos = palmTransform.localPosition;
            Quaternion palmLocalRot = palmTransform.localRotation;

            palmChild.position = transform.position;
            palmChild.rotation = transform.rotation;

            palmTransform.LookAt(hit.point, palmTransform.up);

            transform.position = palmChild.position;
            transform.rotation = palmChild.rotation;

            palmTransform.localPosition = palmLocalPos;
            palmTransform.localRotation = palmLocalRot;

            transform.RotateAround(palmTransform.position, -palmTransform.right, Vector3.Angle(palmTransform.forward, hit.point - palmTransform.position));
            transform.RotateAround(palmTransform.position, palmTransform.up, Vector3.Angle(palmTransform.forward, hit.point - palmTransform.position));
            transform.position += hit.point - palmTransform.position;
            if(Physics.ComputePenetration(hit.collider, hit.collider.transform.position, hit.collider.transform.rotation,
                palmCollider, palmTransform.transform.position, palmTransform.transform.rotation, out var dir, out var dist))
                transform.position -= dir * dist / 2f;

            foreach(var finger in fingers)
                finger.BendFingerUntilHit(fingerBendSteps, LayerMask.GetMask(Hand.grabbingLayerName));

            grabbable.SetLayerRecursive(grabbable.body.transform, grabbableLayer);
        }

        /// <summary>Returns the current hand pose, ignoring what is being held - (IF SAVING A HELD POSE USE GetHeldPose())</summary>
        public HandPoseData GetHandPose() {
            return new HandPoseData(this);
        }

        /// <summary>Returns the hand pose relative to what it's holding</summary>
        public HandPoseData GetHeldPose() {
            if(holdingObj)
                return new HandPoseData(this, holdingObj);
            return new HandPoseData(this);
        }

        /// <summary>Sets the hand pose and connects the grabbable</summary>
        public virtual void SetHeldPose(HandPoseData pose, Grabbable grabbable, bool createJoint = true) {
            //Set Pose
            pose.SetPose(this, grabbable.transform);

            if(createJoint) {
                holdingObj = grabbable;
                OnBeforeGrabbed?.Invoke(this, holdingObj);
                holdingObj.body.transform.position = transform.position;

                CreateJoint(holdingObj, holdingObj.jointBreakForce * ((1f / Time.fixedUnscaledDeltaTime) / 60f), float.PositiveInfinity);

                grabPoint.parent = holdingObj.transform;
                grabPoint.transform.position = transform.position;
                grabPoint.transform.rotation = transform.rotation;

                OnGrabbed?.Invoke(this, holdingObj);
                holdingObj.OnGrab(this);

                SetHandLocation(moveTo.position, moveTo.rotation);

                grabbed = true;
            }

        }

        /// <summary>Sets the hand pose</summary>
        public void SetHandPose(HandPoseData pose) {
            pose.SetPose(this, null);
        }

        /// <summary>Sets the hand pose</summary>
        public void SetHandPose(GrabbablePose pose) {
            pose.GetHandPoseData(this).SetPose(this, null);
        }

        /// <summary>Takes a new pose and an amount of time and poses the hand</summary>
        public void UpdatePose(HandPoseData pose, float time) {
            if(handAnimateRoutine != null)
                StopCoroutine(handAnimateRoutine);
            if(gameObject.activeInHierarchy)
                handAnimateRoutine = StartCoroutine(LerpHandPose(GetHandPose(), pose, time));
        }

        /// <summary>If the held grabbable has a GrabbablePose, this will return it. Null if none</summary>
        public bool GetGrabPose(Transform from, Grabbable grabbable, out GrabbablePose grabPose, out Transform relativeTo) {
            if(grabbable.GetSavedPose(grabbable.transform, out var poseCombiner) && poseCombiner.CanSetPose(this)) {
                grabPose = poseCombiner.GetClosestPose(this, grabbable);
                relativeTo = grabbable.transform;
                return true;
            }
            if(grabbable.GetSavedPose(from, out var poseCombiner1) && poseCombiner1.CanSetPose(this)) {
                grabPose = poseCombiner1.GetClosestPose(this, grabbable);
                relativeTo = from;
                return true;
            }

            grabPose = null;
            relativeTo = from;
            return false;
        }


        /// <summary>Returns the current held object - null if empty (Same as GetHeld())</summary>
        public Grabbable GetHeldGrabbable() {
            return holdingObj;
        }

        /// <summary>Returns the current held object - null if empty (Same as GetHeldGrabbable())</summary>
        public Grabbable GetHeld() {
            return holdingObj;
        }

        /// <summary>Returns true if squeezing has been triggered</summary>
        public bool IsSqueezing() {
            return squeezing;
        }



        //========================= HELPER FUNCTIONS ======================
        //=================================================================
        //=================================================================


        /// <summary>Whether or not this hand can grab the grabbbale based on hand and grabbable settings</summary>
        public bool CanGrab(Grabbable grab) {
            var cantHandSwap = (grab.IsHeld() && grab.singleHandOnly && !grab.allowHeldSwapping);
            return (grab.CanGrab(this) && !grabbing && !cantHandSwap);
        }

        /// <summary>Sets the hands grip 0 is open 1 is closed</summary>
        public void SetGrip(float grip) {
            triggerPoint = grip;
        }


        [ContextMenu("Set Pose - Relax Hand")]
        public void RelaxHand() {
            foreach(var finger in fingers)
                finger.SetFingerBend(gripOffset);
        }

        [ContextMenu("Set Pose - Open Hand")]
        public void OpenHand() {
            foreach(var finger in fingers)
                finger.SetFingerBend(0);
        }

        [ContextMenu("Set Pose - Close Hand")]
        public void CloseHand() {
            foreach(var finger in fingers)
                finger.SetFingerBend(1);
        }

        [ContextMenu("Bend Fingers Until Hit")]
        /// <summary>Bends each finger until they hit</summary>
        public void ProceduralFingerBend() {
            ProceduralFingerBend(~LayerMask.GetMask(rightHandLayerName, leftHandLayerName));
        }

        /// <summary>Bends each finger until they hit</summary>
        public void ProceduralFingerBend(int layermask) {
            foreach(var finger in fingers) {
                finger.BendFingerUntilHit(fingerBendSteps, layermask);
            }
        }

        /// <summary>Bends each finger until they hit</summary>
        public void ProceduralFingerBend(RaycastHit hit) {
            foreach(var finger in fingers) {
                finger.BendFingerUntilHit(fingerBendSteps, hit.transform.gameObject.layer);
            }
        }

        /// <summary>Plays haptic vibration on the hand controller if supported by controller link</summary>
        public void PlayHapticVibration() {
            PlayHapticVibration(grabTime, 0.5f);
        }

        /// <summary>Plays haptic vibration on the hand controller if supported by controller link</summary>
        public void PlayHapticVibration(float duration) {
            PlayHapticVibration(duration, 0.5f);
        }

        /// <summary>Plays haptic vibration on the hand controller if supported by controller link</summary>
        public void PlayHapticVibration(float duration, float amp = 0.5f) {
            if(left)
                HandControllerLink.handLeft.TryHapticImpulse(duration, amp);
            else
                HandControllerLink.handRight.TryHapticImpulse(duration, amp);
        }


        //========================= SAVING FUNCTIONS ======================
        //=================================================================
        //=================================================================

        [Button("Save Open Pose"), ContextMenu("SAVE OPEN")]
        public void SaveOpenPose() {
            foreach(var finger in fingers) {
#if UNITY_EDITOR
                EditorUtility.SetDirty(finger);
#endif
                finger.SetMinPose();
            }
            Debug.Log("Auto Hand: Saved Open Hand Pose!");
        }

        [Button("Save Closed Pose"), ContextMenu("SAVE CLOSED")]
        public void SaveClosedPose() {
            foreach(var finger in fingers) {
#if UNITY_EDITOR
                EditorUtility.SetDirty(finger);
#endif
                finger.SetMaxPose();
            }
            Debug.Log("Auto Hand: Saved Closed Hand Pose!");
        }



        #region INTERNAL FUNCTIONS

        //======================= INTERNAL FUNCTIONS ======================
        //=================================================================
        //=================================================================



        protected virtual void OnCollisionFirstEnter(GameObject collision) {
            if(collision.CanGetComponent(out HandTouchEvent touchEvent))
                touchEvent.Touch(this);
        }

        protected virtual void OnCollisionLastExit(GameObject collision) {
            if(collision.CanGetComponent(out HandTouchEvent touchEvent))
                touchEvent.Untouch(this);
        }

        protected virtual void OnTriggerFirstEnter(GameObject other) {
            CheckEnterPoseArea(other);
            if(other.CanGetComponent(out HandTriggerAreaEvents area)) {
                triggerEventAreas.Add(area);
                area.Enter(this);
            }
        }

        protected virtual void OnTriggerLastExit(GameObject other) {
            CheckExitPoseArea(other);
            if(other.CanGetComponent(out HandTriggerAreaEvents area)) {
                triggerEventAreas.Remove(area);
                area.Exit(this);
            }
        }

        //Highlighting doesn't need to be called every update, it can be called every 4th update without causing any noticable differrences 
        IEnumerator HighlightUpdate(float timestep) {
            //This will smooth out the highlight calls to help prevent occasional lag spike
            if(left)
                yield return new WaitForSecondsRealtime(timestep / 2);

            while(true) {
                if(usingHighlight) {
                    UpdateHighlight();
                }
                yield return new WaitForSecondsRealtime(timestep);
            }
        }


        /// <summary>Takes a hit from a grabbable object and moves the hand towards that point, then calculates ideal hand shape</summary>
        protected IEnumerator GrabObject(RaycastHit hit, Grabbable grab, GrabType grabType) {
            //Checks if the grabbable script is enabled
            if(!this.CanGrab(grab))
                yield break;

            while(grab.beingGrabbed)
                yield return new WaitForEndOfFrame();


            CancelPose();
            ClearPoseArea();

            grabPose = null;
            grabbing = true;
            holdingObj = grab;
            lookingAtObj = null;
            var instantGrab = holdingObj.instantGrab || grabType == GrabType.InstantGrab;
            var startHoldingObj = holdingObj;

            foreach(var collider in holdingObj.heldIgnoreColliders)
                HandIgnoreCollider(collider, true);


            //Hand Swap - One Handed Items
            if(grab.singleHandOnly && grab.HeldCount() > 0 && grabType == GrabType.GrabbableToHand) {
                grab.ForceHandRelease(grab.GetHeldBy()[0]);
                yield return new WaitForFixedUpdate();
            }

            holdingObj.OnBeforeGrab(this);
            if(holdingObj == null) {
                CancelGrab();
                yield break;
            }

            OnBeforeGrabbed?.Invoke(this, holdingObj);
            if(holdingObj == null) {
                CancelGrab();
                yield break;
            }



            //SETS GRAB POINT
            grabPoint.parent = hit.transform;
            grabPoint.position = hit.point;
            //Sets Pose
            HandPoseData startGrabPose;
            Transform poseRelativeTo;
            startGrabPose = new HandPoseData(this, holdingObj);
            float startGrabDist = Vector3.Distance(palmTransform.position, grabPoint.position);
            startGrabDist = Mathf.Clamp(startGrabDist, reachDistance / 3f, reachDistance);

            var startGrabbablePosition = holdingObj.transform.position;
            var startGrabbableRotation = holdingObj.transform.rotation;

            if(GetGrabPose(hit.collider.transform, holdingObj, out grabPose, out poseRelativeTo)) {
                if(holdingObj.transform != poseRelativeTo.transform)
                    startGrabPose = new HandPoseData(this, holdingObj);
            }
            else {
                startGrabPose = new HandPoseData(this, grabPoint);
                transform.position -= palmTransform.forward * 0.1f;
                AutoPose(hit, holdingObj);
            }

            //Smooth Grabbing
            if(grabTime > 0 && !instantGrab) {
                Transform grabTarget = grabPose != null ? poseRelativeTo : grabPoint;
                HandPoseData postGrabPose = grabPose == null ? new HandPoseData(this, grabPoint) : grabPose.GetHandPoseData(this);
                var adjustedGrabTime = grabTime * (startGrabDist / reachDistance);

                if(grabType == GrabType.HandToGrabbable || (grabType == GrabType.GrabbableToHand && !holdingObj.parentOnGrab)) {
                    for(float i = 0; i < adjustedGrabTime; i += Time.deltaTime) {
                        if(holdingObj != null) {
                            var point = i / adjustedGrabTime;
                            HandPoseData.LerpPose(startGrabPose, postGrabPose, grabCurve.Evaluate(point)).SetPose(this, grabTarget);

                            holdingObj.body.angularVelocity *= 1 - (Time.deltaTime * 10);
                            holdingObj.body.velocity = Vector3.zero;
                            body.velocity = Vector3.zero;
                            body.angularVelocity = Vector3.zero;
                            yield return new WaitForEndOfFrame();
                        }
                    }
                }
                else if(grabType == GrabType.GrabbableToHand) {
                    bool useGravity = holdingObj.body.useGravity;
                    holdingObj.body.useGravity = false;
                    SetMoveTo();
                    SetHandLocation(moveTo.position, moveTo.rotation);
                    postGrabPose.SetPose(this, grabTarget);

                    for(float i = 0; i < adjustedGrabTime; i += Time.deltaTime) {
                        if(holdingObj != null) {
                            var point = i / adjustedGrabTime;
                            holdingObj.body.velocity = Vector3.zero;
                            holdingObj.body.angularVelocity = Vector3.zero;

                            HandPoseData.LerpPose(startGrabPose, postGrabPose, grabCurve.Evaluate(point)).SetPose(this, grabTarget);

                            SetMoveTo();
                            SetHandLocation(moveTo.position, moveTo.rotation);
                            holdingObj.transform.position = Vector3.Lerp(startGrabbablePosition, holdingObj.transform.position, grabCurve.Evaluate(point));
                            holdingObj.transform.rotation = Quaternion.Lerp(startGrabbableRotation, holdingObj.transform.rotation, grabCurve.Evaluate(point));
                            yield return new WaitForEndOfFrame();
                        }
                    }

                    if(holdingObj != null)
                        holdingObj.body.useGravity = useGravity;
                    else
                        startHoldingObj.body.useGravity = useGravity;
                }

                if(holdingObj != null)
                    postGrabPose.SetPose(this, grabTarget);
            }
            else if(grabPose != null) {
                holdingObj.body.velocity = Vector3.zero;
                holdingObj.body.angularVelocity = Vector3.zero;
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                grabPose.SetHandPose(this);
            }

            //Hand Swap - One Handed Items
            if(grab.singleHandOnly && grab.HeldCount() > 0) {
                grab.ForceHandRelease(grab.GetHeldBy()[0]);
                yield return new WaitForFixedUpdate();
            }

            if(holdingObj == null) {
                CancelGrab();
                yield break;
            }

            if(!instantGrab && !(holdingObj.grabType == HandGrabType.GrabbableToHand) && !(grabType == GrabType.GrabbableToHand)) {
                grabPositionOffset = transform.position - follow.transform.position;
                grabRotationOffset = Quaternion.Inverse(follow.transform.rotation) * transform.rotation;
            }

            grabPoint.transform.position = transform.position;
            grabPoint.transform.rotation = transform.rotation;
            CreateJoint(holdingObj, holdingObj.jointBreakForce * ((1f / Time.fixedUnscaledDeltaTime) / 60f), float.PositiveInfinity);

            OnGrabbed?.Invoke(this, holdingObj);
            holdingObj.OnGrab(this);


            grabbed = true;
            /*
            bool objectFree = holdingObj.body.isKinematic == false && holdingObj.body.constraints == RigidbodyConstraints.None && holdingObj.parentOnGrab;

            //Returns hand based on grab return time
            if(grabType == GrabType.HandToGrabbable && objectFree && grabTime > 0 && !instantGrab) {
                for(float i = 0; i < grabTime * (startGrabDist / reachDistance); i += Time.fixedDeltaTime) {
                    SetMoveTo();
                    if(holdingObj != null) {
                        var point = i / (grabTime * (startGrabDist / reachDistance));
                        body.position = Vector3.Lerp(body.position, moveTo.position, point);
                        body.rotation = Quaternion.Lerp(body.rotation, moveTo.rotation, point);
                    }
                    else
                        break;
                    yield return new WaitForFixedUpdate();
                }
            }
            else */
            
            if(instantGrab || grabTime == 0) {
                SetMoveTo();
                SetHandLocation(moveTo.position, moveTo.rotation);
            }


            if(holdingObj == null) {
                CancelGrab();
                yield break;
            }



            void CancelGrab() {
                BreakGrabConnection();
                if(startHoldingObj) {
                    startHoldingObj.body.velocity = Vector3.zero;
                    startHoldingObj.body.angularVelocity = Vector3.zero;
                    startHoldingObj.beingGrabbed = false;
                }
                grabbing = false;
                grabRoutine = null;
            }

            //Reset Values
            grabbing = false;
            startHoldingObj.beingGrabbed = false;
            grabRoutine = null;
        }

        /// <summary>Ensures any pose being made is canceled</summary>
        protected void CancelPose() {
            if(handAnimateRoutine != null)
                StopCoroutine(handAnimateRoutine);
            handAnimateRoutine = null;
            grabPose = null;
        }

        /// <summary>Not exactly lerped, uses non-linear sqrt function because it looked better -- planning animation curves options soon</summary>
        protected virtual IEnumerator LerpHandPose(HandPoseData fromPose, HandPoseData toPose, float totalTime) {
            float timePassed = 0;
            while(timePassed < totalTime) {
                SetHandPose(HandPoseData.LerpPose(fromPose, toPose, Mathf.Pow(timePassed / totalTime, 0.5f)));
                yield return new WaitForEndOfFrame();
                timePassed += Time.deltaTime;
            }
            SetHandPose(HandPoseData.LerpPose(fromPose, toPose, 1));
            handAnimateRoutine = null;
        }

        /// <summary>Checks and manages if any of the hands colliders enter a pose area</summary>
        protected virtual void CheckEnterPoseArea(GameObject other) {
            if(holdingObj || !usingPoseAreas || !other.activeInHierarchy)
                return;

            if(other && other.CanGetComponent(out HandPoseArea tempPose)) {
                for(int i = 0; i < tempPose.poseAreas.Length; i++) {
                    if(tempPose.poseIndex == poseIndex) {
                        if(tempPose.HasPose(left) && (handPoseArea == null || handPoseArea != tempPose)) {
                            if(handPoseArea == null)
                                preHandPoseAreaPose = GetHandPose();

                            else if(handPoseArea != null)
                                TryRemoveHandPoseArea(handPoseArea);

                            handPoseArea = tempPose;
                            handPoseArea?.OnHandEnter?.Invoke(this);
                            if(holdingObj == null)
                                UpdatePose(handPoseArea.GetHandPoseData(left), handPoseArea.transitionTime);
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>Checks if manages any of the hands colliders exit a pose area</summary>
        protected virtual void CheckExitPoseArea(GameObject other) {
            if(!usingPoseAreas || !other.gameObject.activeInHierarchy)
                return;

            if(other.CanGetComponent(out HandPoseArea poseArea))
                TryRemoveHandPoseArea(poseArea);
        }

        internal void TryRemoveHandPoseArea(HandPoseArea poseArea) {
            if(handPoseArea != null && handPoseArea.gameObject.Equals(poseArea.gameObject)) {
                if(holdingObj == null) {
                    if(handPoseArea != null)
                        UpdatePose(preHandPoseAreaPose, handPoseArea.transitionTime);
                    handPoseArea?.OnHandExit?.Invoke(this);
                    handPoseArea = null;
                }
                else if(holdingObj != null) {
                    handPoseArea?.OnHandExit?.Invoke(this);
                    handPoseArea = null;
                }
            }
        }

        private void ClearPoseArea() {
            if(handPoseArea != null)
                handPoseArea.OnHandExit?.Invoke(this);
            handPoseArea = null;
        }

        internal virtual void RemoveHandTriggerArea(HandTriggerAreaEvents handTrigger) {
            handTrigger.Exit(this);
            triggerEventAreas.Remove(handTrigger);
        }

        #endregion
    }
}