using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using NaughtyAttributes;
using UnityEditor;
using UnityEngine.Serialization;

namespace Autohand {
    public enum HandGrabType{
        Default,
        HandToGrabbable,
        GrabbableToHand
    }

    [HelpURL("https://earnestrobot.notion.site/Grabbables-9308c564e60848a882eb23e9778ee2b6")]
    public class Grabbable : GrabbableBase {


        [Tooltip("This will copy the given grabbables settings to this grabbable when applied"), OnValueChanged("EditorCopyGrabbable")]
        public Grabbable CopySettings;

        [Header("Grab Settings")]

        [Tooltip("Whether or not this can be grabbed with more than one hand")]
        public bool singleHandOnly = false;

        [ShowIf("singleHandOnly")]
        [Tooltip("if false single handed items cannot be passes back and forth on grab")]
        public bool allowHeldSwapping = true;

        [Tooltip("Will the item automatically return the hand on grab - good for saved poses, bad for heavy things")]
        public bool instantGrab = false;

        [Tooltip("Experimental - ignores weight of held object while held")]
        public bool ignoreWeight = false;

        [Tooltip("Creates an offset an grab so the hand will not return to the hand on grab - Good for statically jointed grabbable objects")]
        public bool maintainGrabOffset = false;

        [Tooltip("This will NOT parent the object under the hands on grab. This will parent the object to the parents of the hand, which allow you to move the hand parent object smoothly while holding an item, but will also allow you to move items that are very heavy - recommended for all objects that aren't very heavy or jointed to other rigidbodies")]
        public bool parentOnGrab = true;


        [Header("Release Settings")]
        [Tooltip("How much to multiply throw by for this grabbable when releasing - 0-1 for no or reduced throw strength")]
        [FormerlySerializedAs("throwMultiplyer")]
        public float throwPower = 1;

        [Tooltip("The required force to break the fixedJoint\n " +
                 "Turn this to \"infinity\" to disable (Might cause jitter)\n" +
                "Ideal value depends on hand mass and velocity settings")]
        public float jointBreakForce = 3500;



        [AutoToggleHeader("Show Advanced")]
        public bool showAdvancedSettings = false;

        [ShowIf("showAdvancedSettings")]
        [Tooltip("Which hand this can be held by")]
        public HandGrabType grabType;

        [ShowIf("showAdvancedSettings")]
        [Tooltip("Which hand this can be held by")]
        public HandType handType = HandType.both;

        [ShowIf("showAdvancedSettings")]
        [Tooltip("Adds and links a GrabbableChild to each child with a collider on start - So the hand can grab them")]
        public bool makeChildrenGrabbable = true;

        [ShowIf("showAdvancedSettings")]
        [Tooltip("Whether or not the break call made only when holding with multiple hands - if this is false the break event can be called by forcing an object into a static collider")]
        public bool pullApartBreakOnly = true;


        [Space]
        [ShowIf("showAdvancedSettings")]
        [Min(0), Tooltip("The joint that connects the hand and the grabbable. Defaults to the joint in AutoHand/Resources/DefaultJoint.prefab if empty")]
        public ConfigurableJoint customGrabJoint;

        [Tooltip("The number of seconds that the hand collision should ignore the released object\n (Good for increased placement precision and resolves clipping errors)"), Min(0)]
        [ShowIf("showAdvancedSettings")]
        public float ignoreReleaseTime = 0.5f;


        [Min(0), Tooltip("I.E. Grab Prioirty - BIGGER IS BETTER - divides highlight distance by this when calculating which object to grab. Hands always grab closest object to palm")]
        [ShowIf("showAdvancedSettings")]
        public float grabPriorityWeight = 1;

        [Tooltip("Offsets the grabbable by this much when being held")]
        [ShowIf("showAdvancedSettings")]
        public Vector3 heldPositionOffset;

        [Tooltip("Offsets the grabbable by this many degrees when being held")]
        [ShowIf("showAdvancedSettings")]
        public Vector3 heldRotationOffset;

        [Space]
        [ShowIf("showAdvancedSettings")]
        [Tooltip("For the special use case of having grabbable objects with physics jointed peices move properly while being held")]
        public List<Rigidbody> jointedBodies = new List<Rigidbody>();

        [ShowIf("showAdvancedSettings")]
        [Tooltip("For the special use case of having things connected to the grabbable that the hand should ignore while being held (good for doors and drawers) -> for always active use the [GrabbableIgnoreHands] Component")]
        public List<Collider> heldIgnoreColliders = new List<Collider>();

        [AutoToggleHeader("Show Events")]
        public bool showEvents = true;
        [Space]
        [ShowIf("showEvents")]
        public UnityHandGrabEvent onGrab;
        [ShowIf("showEvents")]
        public UnityHandGrabEvent onRelease;

        [AutoToggleHeader("Show Advanced Events")]
        public bool showAdvancedEvents = false;
        [ShowIf("showAdvancedEvents")]
        [Space, Space]
        public UnityHandGrabEvent onSqueeze;
        [ShowIf("showAdvancedEvents")]
        public UnityHandGrabEvent onUnsqueeze;

        [Space, Space]
        [ShowIf("showAdvancedEvents")]
        public UnityHandGrabEvent onHighlight;
        [ShowIf("showAdvancedEvents")]
        public UnityHandGrabEvent onUnhighlight;
        [Space, Space]

        [ShowIf("showAdvancedEvents")]
        public UnityHandGrabEvent OnJointBreak;


        //Advanced Hidden Settings
        [ShowIf("showAdvancedSettings")]
        [HideInInspector, Tooltip("Lock hand in place on grab (This is a legacy setting, set hand kinematic on grab/release instead)")]
        public bool lockHandOnGrab = false;

        
        /// <summary>Legacy value for Throw Power</summary>
        public float throwMultiplyer{
            get { return throwPower; }
            set { throwPower = value; }
        }

        //For programmers <3
        public HandGrabEvent OnBeforeGrabEvent;
        public HandGrabEvent OnGrabEvent;
        public HandGrabEvent OnReleaseEvent;
        public HandGrabEvent OnJointBreakEvent;

        public HandGrabEvent OnSqueezeEvent;
        public HandGrabEvent OnUnsqueezeEvent;

        public HandGrabEvent OnHighlightEvent;
        public HandGrabEvent OnUnhighlightEvent;

        /// <summary>Whether or not this object was force released (dropped) when last released (as opposed to being intentionally released)</summary>
        public bool wasForceReleased { get; internal set;} = false;
        public Hand lastHeldBy { get; protected set; } = null;


#if UNITY_EDITOR
        void EditorCopyGrabbable() {
            if(CopySettings != null)
                EditorUtility.CopySerialized(CopySettings, this);
        }
#endif


        protected new virtual void Awake(){

            if (makeChildrenGrabbable)
                MakeChildrenGrabbable();

            base.Awake();

            for (int i = 0; i < jointedBodies.Count; i++)
                jointedParents.Add(jointedBodies[i].transform.parent ?? null);
        }

        protected new virtual void FixedUpdate(){
            base.FixedUpdate();
            if(wasIsGrabbable && !(isGrabbable || enabled)) {
                ForceHandsRelease();
            }
            wasIsGrabbable = isGrabbable || enabled;
        }
        
        public virtual void OnTriggerEnter(Collider other) {
            if(other.CanGetComponent(out PlacePoint otherPoint)) {
                if (placePoint == null && otherPoint.CanPlace(this)){
                    otherPoint.Highlight(this);
                }
            }
        }
        
        public virtual void OnTriggerExit(Collider other){
            if(other.CanGetComponent(out PlacePoint otherPoint)) {
                if (placePoint != null && otherPoint != null && placePoint.Equals(otherPoint) && placePoint.placedObject != this) {
                    placePoint.StopHighlight(this);
                }
            }
        }


        /// <summary>Called when the hand starts aiming at this item for pickup</summary>
        internal virtual void Highlight(Hand hand, Material customMat = null) {
            if(!hightlighting){
                hightlighting = true;
                onHighlight?.Invoke(hand, this);
                OnHighlightEvent?.Invoke(hand, this);
                var highlightMat = customMat != null ? customMat : hightlightMaterial;
                highlightMat = highlightMat != null ? highlightMat : hand.defaultHighlight;
                if(highlightMat != null){
                    MeshRenderer meshRenderer;
                    if(gameObject.CanGetComponent(out meshRenderer)) {
                        //Creates a slightly larger copy of the mesh and sets its material to highlight material
                        highlightObj = new GameObject();
                        highlightObj.transform.parent = transform;
                        highlightObj.transform.localPosition = Vector3.zero;
                        highlightObj.transform.localRotation = Quaternion.identity;
                        highlightObj.transform.localScale = Vector3.one * 1.001f;
                        highlightObj.AddComponent<MeshFilter>().sharedMesh = GetComponent<MeshFilter>().sharedMesh;
                        var highlightRenderer = highlightObj.AddComponent<MeshRenderer>();
                        var mats = new Material[meshRenderer.materials.Length];
                        for(int i = 0; i < mats.Length; i++)
                            mats[i] = highlightMat;
                        highlightRenderer.materials = mats;
                    }
                }
            }
        }

        /// <summary>Called when the hand stops aiming at this item</summary>
        internal virtual void Unhighlight(Hand hand) {
            if(hightlighting){
                onUnhighlight?.Invoke(hand, this);
                OnUnhighlightEvent?.Invoke(hand, this);
                hightlighting = false;
                if(highlightObj != null)
                    Destroy(highlightObj);
            }
        }



        /// <summary>Called by the hands Squeeze() function is called and this item is being held</summary>
        internal virtual void OnSqueeze(Hand hand) {
            OnSqueezeEvent?.Invoke(hand, this);
            onSqueeze?.Invoke(hand, this);
        }

        /// <summary>Called by the hands Unsqueeze() function is called and this item is being held</summary>
        internal virtual void OnUnsqueeze(Hand hand) {
            OnUnsqueezeEvent?.Invoke(hand, this);
            onUnsqueeze?.Invoke(hand, this);
        }

        /// <summary>Called by the hand when this item is started being grabbed</summary>
        internal virtual void OnBeforeGrab(Hand hand) {
            OnBeforeGrabEvent?.Invoke(hand, this);
            Unhighlight(hand);
            beingGrabbed = true;
            if (resetLayerRoutine != null){
                StopCoroutine(resetLayerRoutine);
                resetLayerRoutine = null;
            }
            resetLayerRoutine = StartCoroutine(IgnoreHandCollision(hand.grabTime, hand));
        }

        /// <summary>Called by the hand whenever this item is grabbed</summary>
        internal virtual void OnGrab(Hand hand) {
            if(lockHandOnGrab)
                hand.body.isKinematic = true;
            
            if(!body.isKinematic)
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            else
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            if (!beingDestroyed) {
                if (resetLayerRoutine != null){
                    StopCoroutine(resetLayerRoutine);
                    resetLayerRoutine = null;
                }
            }

            if (parentOnGrab) {
                body.transform.parent = hand.transform.parent;
                foreach (var jointedBody in jointedBodies){
                    jointedBody.transform.parent = hand.transform.parent;
                    if(jointedBody.gameObject.HasGrabbable(out var grab))
                        grab.heldBodyJointed = true;
                }
            }

            
            if (ignoreWeight) {
                WeightlessFollower heldFollower = null;
                if (!gameObject.CanGetComponent(out heldFollower) || singleHandOnly){
                    heldFollower = gameObject.AddComponent<WeightlessFollower>();
                }

                heldFollower?.Set(hand, this);
            }

            collisionTracker.enabled = true;

            placePoint?.Remove(this);
            heldBy?.Add(hand);
            onGrab?.Invoke(hand, this);
            OnGrabEvent?.Invoke(hand, this);

            wasForceReleased = false;
            beingGrabbed = false;
        }

        /// <summary>Whether or not the hand can grab this grabbable</summary>
        public virtual bool CanGrab(Hand hand){
            return enabled && isGrabbable && (handType == HandType.both || (handType == HandType.left && hand.left) || (handType == HandType.right && !hand.left));
        }

        /// <summary>Called by the hand whenever this item is release</summary>
        internal virtual void OnRelease(Hand hand) {
            if(heldBy.Contains(hand)) {
                if (placePoint != null && placePoint.CanPlace(this))
                    placePoint.Place(this);

                BreakHandConnection(hand);

                if (body != null && heldBy.Count == 0) {
                    body.velocity = hand.ThrowVelocity() * throwMultiplyer;
                    var throwAngularVel = hand.ThrowAngularVelocity();
                    if(!float.IsNaN(throwAngularVel.x) && !float.IsNaN(throwAngularVel.y) && !float.IsNaN(throwAngularVel.z))
                        body.angularVelocity = throwAngularVel;
                }

                OnReleaseEvent?.Invoke(hand, this);
                onRelease?.Invoke(hand, this);
                
                Unhighlight(hand);
            }
        }

        internal virtual void BreakHandConnection(Hand hand){
            if (!heldBy.Remove(hand))
                return;

            if (lockHandOnGrab)
                hand.body.isKinematic = false;

            if(gameObject.activeInHierarchy) {
                if(resetLayerRoutine != null) {
                    StopCoroutine(resetLayerRoutine);
                    resetLayerRoutine = null;
                }
                resetLayerRoutine = StartCoroutine(IgnoreHandCollision(ignoreReleaseTime, hand));
            }

            foreach (var collider in heldIgnoreColliders)
                hand.HandIgnoreCollider(collider, false);

            if (heldBy.Count == 0){
                beingGrabbed = false;
                ResetGrabbableAfterRlease();
            }

            collisionTracker.enabled = false;
            lastHeldBy = hand;
        }
        
        /// <summary>Tells each hand holding this object to release</summary>
        public virtual void HandsRelease() {
            for(int i = heldBy.Count - 1; i >= 0; i--)
                heldBy[i].Release();
        }

        /// <summary>Tells each hand holding this object to release</summary>
        public virtual void HandRelease(Hand hand) {
            if (heldBy.Contains(hand)) {
                hand.Release();
            }
        }

        /// <summary>Forces all the hands on this object to relese without applying throw force or calling OnRelease event</summary>
        public virtual void ForceHandsRelease() {
            for(int i = heldBy.Count - 1; i >= 0; i--) {
                wasForceReleased = true;
                ForceHandRelease(heldBy[i]);
            }

        }

        /// <summary>Forces all the hands on this object to relese without applying throw force</summary>
        public virtual void ForceHandRelease(Hand hand) {
            if (heldBy.Contains(hand)) {
                var throwMult = throwPower;
                throwPower = 0;
                wasForceReleased = true;
                hand.Release();
                throwPower = throwMult;
            }
        }
        

        /// <summary>Called when the joint between the hand and this item is broken\n - Works to simulate pulling item apart event</summary>
        public virtual void OnHandJointBreak(Hand hand) {
            if (heldBy.Contains(hand)) {
                body.WakeUp();

                body.velocity *= 0;
                body.angularVelocity *= 0;

                if(!pullApartBreakOnly){
                    OnJointBreakEvent?.Invoke(hand, this);
                    OnJointBreak?.Invoke(hand, this);
                }
                if (pullApartBreakOnly && heldBy.Count > 1){
                    OnJointBreakEvent?.Invoke(hand, this);
                    OnJointBreak?.Invoke(hand, this);
                }

                ForceHandRelease(hand);

                if(heldBy.Count > 0)
                    heldBy[0].SetHandLocation(heldBy[0].moveTo.position, heldBy[0].transform.rotation);
            }
        }
        
        //============================ GETTERS ============================
        //=================================================================
        //=================================================================


        public Vector3 GetVelocity(){
            return lastCenterOfMassPos - body.position;
        }
        
        public Vector3 GetAngularVelocity(){
            Quaternion deltaRotation = body.rotation * Quaternion.Inverse(lastCenterOfMassRot);
            deltaRotation.ToAngleAxis(out var angle, out var axis);
            angle *= Mathf.Deg2Rad;
            return (1.0f / Time.fixedDeltaTime) * angle/1.2f * axis ;
        }

        public List<Hand> GetHeldBy() {
            return heldBy;
        }
        public int HeldCount() {
            return heldBy.Count;
        }

        public bool IsHeld() {
            return heldBy.Count > 0;
        }

        /// <summary>Returns true during hand grabbing coroutine</summary>
        public bool BeingGrabbed() {
            return beingGrabbed;
        }

        /// <summary>Plays haptic on each hand holding this grabbable</summary>
        public void PlayHapticVibration() {
            foreach(var hand in heldBy) {
                hand.PlayHapticVibration();
            }
        }

        /// <summary>Plays haptic on each hand holding this grabbable</summary>
        public void PlayHapticVibration(float duration = 0.1f) {
            foreach(var hand in heldBy) {
                hand.PlayHapticVibration(duration);
            }
        }

        /// <summary>Plays haptic on each hand holding this grabbable</summary>
        public void PlayHapticVibration(float duration, float amp = 0.5f) {
            foreach(var hand in heldBy) {
                hand.PlayHapticVibration(duration, amp);
            }
        }



        protected virtual void OnDestroy() {
            beingDestroyed = true;
            if(heldBy.Count != 0)
                ForceHandsRelease();
            MakeChildrenUngrabbable();
            if (resetLayerRoutine != null){
                StopCoroutine(resetLayerRoutine);
                resetLayerRoutine = null;
            }
            if(placePoint != null)
                placePoint.Remove(this);
        }



        public void AddJointedBody(Rigidbody body){
            Grabbable grab;
            jointedBodies.Add(body);

            if (body.gameObject.HasGrabbable(out grab))
                jointedParents.Add(grab.originalParent);
            else
                jointedParents.Add(body.transform.parent);

            if (transform.parent != originalParent){
                if(grab != null) {
                    if (grab.HeldCount() == 0)
                        grab.transform.parent = transform.parent;
                    grab.heldBodyJointed = true;
                }
                else
                    grab.transform.parent = transform.parent;
            }
        }

        public void RemoveJointedBody(Rigidbody body){
            var i = jointedBodies.IndexOf(body);
            if (jointedBodies[i].gameObject.HasGrabbable(out var grab)){
                if (grab.HeldCount() == 0)
                    grab.transform.parent = grab.originalParent;
                grab.heldBodyJointed = false;
            }
            else
                jointedBodies[i].transform.parent = jointedParents[i];

            jointedBodies.RemoveAt(i);
            jointedParents.RemoveAt(i);
        }

        public void DoDestroy(){
            Destroy(gameObject);
        }

        /// <summary>This flag is to help prevent jitter for specific types of objects</summary>
        public bool PhysicsMovementOnly()
        {
            return jointedBodies.Count != 0 || !parentOnGrab || collisionTracker.collisionCount > 0 || ignoreParent;
        }
        
        

        
        //Adds a reference script to child colliders so they can be grabbed
        void MakeChildrenGrabbable() {
            for(int i = 0; i < transform.childCount; i++) {
                AddChildGrabbableRecursive(transform.GetChild(i));
            }

            void AddChildGrabbableRecursive(Transform obj) {
                if(obj.CanGetComponent(out Collider col) && col.isTrigger == false && !obj.CanGetComponent<Grabbable>(out _) && !obj.CanGetComponent<GrabbableChild>(out _) && !obj.CanGetComponent<PlacePoint>(out _)){
                    var child = obj.gameObject.AddComponent<GrabbableChild>();
                    child.gameObject.layer = originalLayer;
                    child.grabParent = this;
                }
                for(int i = 0; i < obj.childCount; i++){
                    if(!obj.CanGetComponent<Grabbable>(out _))
                        AddChildGrabbableRecursive(obj.GetChild(i));
                }
            }
        }
        

        //Adds a reference script to child colliders so they can be grabbed
        void MakeChildrenUngrabbable() {
            for(int i = 0; i < transform.childCount; i++) {
                RemoveChildGrabbableRecursive(transform.GetChild(i));
            }

            void RemoveChildGrabbableRecursive(Transform obj) {
                if(obj.GetComponent<GrabbableChild>() && obj.GetComponent<GrabbableChild>().grabParent == this){
                    Destroy(obj.gameObject.GetComponent<GrabbableChild>());
                }
                for(int i = 0; i < obj.childCount; i++){
                    RemoveChildGrabbableRecursive(obj.GetChild(i));
                }
            }
        }

        /// <summary>INTERNAL - Sets the grabbables original layers</summary>
        internal void ResetGrabbableAfterRlease(){

            if (!beingDestroyed) {
                if (!heldBodyJointed)
                    body.transform.parent = originalParent;

                for (int i = 0; i < jointedBodies.Count; i++){
                    if (jointedBodies[i].gameObject.HasGrabbable(out var grab)){
                        if (grab.HeldCount() == 0)
                            grab.transform.parent = grab.originalParent;
                        grab.heldBodyJointed = false;
                    }
                    else if(!heldBodyJointed)
                        jointedBodies[i].transform.parent = jointedParents[i];
                }
            }
        }
    }
}
