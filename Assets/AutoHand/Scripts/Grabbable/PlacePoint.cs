using NaughtyAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Autohand {
    public delegate void PlacePointEvent(PlacePoint point, Grabbable grabbable);
    [RequireComponent(typeof(SphereCollider)), HelpURL("https://earnestrobot.notion.site/Place-Points-e6361a414928450dbb53d76fd653cf9a")]
    //You can override this by turning the radius to zero, and using any other trigger collider
    public class PlacePoint : MonoBehaviour{
        [AutoHeader("Place Point")]
        public bool ignoreMe;

        [Tooltip("If used, the place point will only accept this grabbable as a target")]
        public Grabbable matchTarget;
        [Tooltip("Will allow placement for any grabbable with a name containing this array of strings, leave blank for any grabbable allowed")]
        public string[] placeNames;
        [Tooltip("The radius of the place point")]
        public float placeRadius = 0.1f;
        [Tooltip("This will make the point place the object as soon as it enters the radius, instead of on release")]
        public bool forcePlace = false;
        [Tooltip("Whether or not the grabbable should be disabled on placement")]
        public bool disableGrabOnPlace = false;



        [AutoToggleHeader("Use Kinematic")]
        [Tooltip("Makes the object being placedObject kinematic")]
        public bool makePlacedKinematic = true;

        [Space][Tooltip("The rigidbody to attach the placed grabbable to - leave empty means no joint")]
        [HideIf("makePlacedKinematic")]
        public Rigidbody placedJointLink;
        [HideIf("makePlacedKinematic"), FormerlySerializedAs("placedJointBreakForce")]
        public float jointBreakForce = 1000;

        [AutoToggleHeader("Show Advanced")]
        public bool showAdvanced = false;
        [Tooltip("Snaps an object to the point at start, leave empty for no target")]
        [ShowIf("showAdvanced")]
        public Grabbable startPlaced;
        [Tooltip("The object will snap to this point instead of the place point on place")]
        [ShowIf("showAdvanced")]
        public Transform placedOffset;
        [Tooltip("This will make the point place the object as soon as it enters the radius, instead of on release")]
        [ShowIf("showAdvanced")] 
        public bool parentOnPlace = true;
        [Tooltip("Whether or not to only allow placement of an object while it's being held (or released)")]
        [ShowIf("showAdvanced")]
        public bool heldPlaceOnly = false;
        [Tooltip("Will prevent placement for any name containing this array of strings")]
        [ShowIf("showAdvanced")]
        public string[] blacklistNames;



        [AutoToggleHeader("Show Events")]
        public bool showEvents = true;
        [ShowIf("showEvents")]
        public UnityEvent OnPlace;
        [ShowIf("showEvents")]
        public UnityEvent OnRemove;
        [ShowIf("showEvents")]
        public UnityEvent OnHighlight;
        [ShowIf("showEvents")]
        public UnityEvent OnStopHighlight;
        
        //For the programmers
        public PlacePointEvent OnPlaceEvent;
        public PlacePointEvent OnRemoveEvent;
        public PlacePointEvent OnHighlightEvent;
        public PlacePointEvent OnStopHighlightEvent;

        public Grabbable highlightingObj { get; protected set; } = null;
        public Grabbable placedObject { get; protected set; } = null;
        public Grabbable lastPlacedObject { get; protected set; } = null;



        protected FixedJoint joint = null;

        //How far the placed object has to be moved to count to auto remove from point so something else can take its place
        protected float removalDistance = 0.05f;

        protected Vector3 placePosition;
        protected SphereCollider col;
        protected Transform originParent;
        protected bool placingFrame;
        protected CollisionDetectionMode placedObjDetectionMode;


        protected virtual void Start(){
            col = gameObject.GetComponent<SphereCollider>();
            col.radius = placeRadius;
            col.isTrigger = true;
            if (placedOffset == null)
                placedOffset = transform;
            SetStartPlaced();
            StartCoroutine(HighlighSafetyCheck());
        }

        IEnumerator HighlighSafetyCheck(){
            while (true){
                if (highlightingObj && placedObject == null){
                    if (!IsOverlapping(highlightingObj))
                        StopHighlight(highlightingObj);
                }

                yield return new WaitForSecondsRealtime(0.2f);
            }
        }

        protected virtual void FixedUpdate(){
        }

        public virtual bool CanPlace(Grabbable placeObj) {
            if(placedObject != null)
                return false;

            if (heldPlaceOnly && placeObj.HeldCount() == 0)
                return false;

            if (matchTarget != null && placeObj != matchTarget)
                return false;

            if (placeNames.Length == 0 && blacklistNames.Length == 0)
                return true;

            if (blacklistNames.Length > 0)
                foreach(var badName in blacklistNames) {
                    if(placeObj.name.Contains(badName)){
                        return false;
                    }
                }
            
            if(placeNames.Length > 0)
                foreach(var placeName in placeNames) {
                    if(placeObj.name.Contains(placeName)){
                        return true;
                    }
                }

            return false;
        }
        

        public virtual void Place(Grabbable placeObj) {
            if (placedObject != null)
                return;

            if(placeObj.placePoint != null && placeObj.placePoint != this)
                placeObj.placePoint.Remove(placeObj);


            placedObject = placeObj;
            placedObject.SetPlacePoint(this);


            if (placeObj.HeldCount() > 0)
                placeObj.ForceHandsRelease();

            placingFrame = true;
            originParent = placeObj.transform.parent;

            placeObj.transform.position = placedOffset.position;
            placeObj.transform.rotation = placedOffset.rotation;
            placeObj.body.position = placeObj.transform.position;
            placeObj.body.rotation = placeObj.transform.rotation;
            placedObjDetectionMode = placeObj.body.collisionDetectionMode;

            placeObj.OnGrabEvent += OnPlacedObjectGrabbed;
            placeObj.OnReleaseEvent += OnPlacedObjectReleased;

            if(makePlacedKinematic) {
                placeObj.body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                placeObj.body.isKinematic = makePlacedKinematic;
            }
            else if (placedJointLink != null){
                joint = placedJointLink.gameObject.AddComponent<FixedJoint>();
                joint.connectedBody = placeObj.body;
                joint.breakForce = jointBreakForce;
                joint.breakTorque = jointBreakForce;
                
                joint.connectedMassScale = 1;
                joint.massScale = 1;
                joint.enableCollision = false;
                joint.enablePreprocessing = false;
            }
            

            if (disableGrabOnPlace)
                placeObj.enabled = false;


            StopHighlight(placeObj);

            placePosition = placedObject.transform.position;
            OnPlaceEvent?.Invoke(this, placeObj);
            OnPlace?.Invoke();

            placeObj.body.velocity = Vector3.zero;
            placeObj.body.angularVelocity = Vector3.zero;

            if(parentOnPlace)
                placeObj.transform.parent = transform;
        }

        public void Remove() {
            if(placedObject != null)
                Remove(placedObject);
        }

        public virtual void Remove(Grabbable placeObj) {
            if (placedObject == null)
                return;

            if(makePlacedKinematic)
                placeObj.body.isKinematic = false;

            placeObj.body.collisionDetectionMode = placedObjDetectionMode;

            placeObj.OnGrabEvent -= OnPlacedObjectGrabbed;
            placeObj.OnReleaseEvent -= OnPlacedObjectReleased;

            if (parentOnPlace && !placeObj.BeingDestroyed())
                placeObj.transform.parent = originParent;

            OnRemoveEvent?.Invoke(this, placeObj);
            OnRemove?.Invoke();

            Highlight(placeObj);

            lastPlacedObject = placedObject;
            placedObject = null;

            if(joint != null){
                Destroy(joint);
                joint = null;
            }
        }


        internal virtual void Highlight(Grabbable from) {
            if(highlightingObj == null){
                from.SetPlacePoint(this);

                highlightingObj = from;
                OnHighlightEvent?.Invoke(this, from);
                OnHighlight?.Invoke();

                if(placedObject == null && forcePlace)
                    Place(from);
            }
        }


        bool IsOverlapping(Grabbable from){
            var sphereCheck = Physics.OverlapSphere(transform.position, placeRadius, LayerMask.GetMask(Hand.grabbableLayers));
            for (int i = 0; i < sphereCheck.Length; i++){
                if (sphereCheck[i].attachedRigidbody == highlightingObj.body) {
                    return true;
                }
            }
            return false;
        }


        internal virtual void StopHighlight(Grabbable from) {
            if(highlightingObj != null) {
                bool callStopHighlight = true;
                if(placedObject == null) {
                    callStopHighlight = !IsOverlapping(from);
                }

                if(callStopHighlight) {
                    highlightingObj = null;
                    OnStopHighlightEvent?.Invoke(this, from);
                    OnStopHighlight?.Invoke();
                    if (placedObject == null)
                        from.SetPlacePoint(null);
                }
            }
        }



        public virtual void SetStartPlaced() {
            if(startPlaced != null){
                startPlaced.SetPlacePoint(this);
                Place(startPlaced);
            }
        }
        
        public Grabbable GetPlacedObject() {
            return placedObject;
        }

        internal float Distance(Transform from) {
            return Vector3.Distance(from.position, transform.position+transform.InverseTransformPoint(placedOffset.position));
        }

        protected virtual void OnPlacedObjectGrabbed(Hand pHand, Grabbable pGrabbable)
        {
            // Unset kinematic status when the placed object is grabbed.
            if (makePlacedKinematic)
                pGrabbable.body.isKinematic = false;
        }

        protected virtual void OnPlacedObjectReleased(Hand pHand, Grabbable pGrabbable)
        {
            // Re-Place() grabbable when placed object is released before this has been unsubscribed to. (Before the object has left the bounds of the place points.)
            if (makePlacedKinematic)
                Place(pGrabbable);
        }

        protected virtual void OnJointBreak(float breakForce) {
            if(placedObject != null)
                Remove(placedObject);
        }

        void OnDrawGizmosSelected() {
            if(col == null)
                col = gameObject.GetComponent<SphereCollider>();

            if(col != null)
                col.radius = placeRadius;

            if (placedOffset == null)
                return;
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position + transform.InverseTransformPoint(placedOffset.position), 0.0025f);


        }

    }
}
