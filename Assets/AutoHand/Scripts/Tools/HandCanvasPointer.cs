using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Autohand
{
    public class HandCanvasPointer : MonoBehaviour
    {
        [Header("References")]
        public GameObject hitPointMarker;
        private LineRenderer lineRenderer;


        [Header("Ray settings")]
        public float raycastLength = 8.0f;
        public bool autoShowTarget = true;
        public LayerMask UILayer;


        [Header("Events")]
        public UnityEvent StartSelect;
        public UnityEvent StopSelect;
        public UnityEvent StartPoint;
        public UnityEvent StopPoint;

        // Internal variables
        private bool hover = false;
        AutoInputModule inputModule;


        static Camera cam;

        int pointerIndex;

        void OnEnable()
        {
            if (cam == null)
            {
                cam = new GameObject("Camera Canvas Pointer (I AM CREATED AT RUNTIME FOR UI CANVAS INTERACTION, I AM NOT RENDERING ANYTHING, I AM NOT CREATING ADDITIONAL OVERHEAD, THANK YOU FOR READING MY STORY)").AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.Nothing;
                cam.stereoTargetEye = StereoTargetEyeMask.None;
                cam.orthographic = true;
                cam.orthographicSize = 0.001f;
                cam.cullingMask = 0;
                cam.nearClipPlane = 0.01f;
                cam.depth = 0f;
                cam.allowHDR = false;
                cam.enabled = false;
                cam.fieldOfView = 0.00001f;

                foreach (var canvas in FindObjectsOfType<Canvas>(true))
                {
                    canvas.worldCamera = cam;
                }
            }

            if (inputModule.Instance != null)
                pointerIndex = inputModule.Instance.AddPointer(this);
        }

        void OnDisable()
        {
            inputModule.Instance?.RemovePointer(this);
        }

        public void SetIndex(int index)
        {
            pointerIndex = index;
        }

        internal void Preprocess()
        {
            cam.transform.position = transform.position;
            cam.transform.forward = transform.forward;
        }

        public void Press()
        {
            // Handle the UI events
            inputModule.ProcessPress(pointerIndex);

            // Show the ray when they attemp to press
            if (!autoShowTarget && hover) ShowRay(true);

            // Fire the Unity event
            StartSelect?.Invoke();
        }

        public void Release()
        {
            // Handle the UI events
            inputModule.ProcessRelease(pointerIndex);

            // Fire the Unity event
            StopSelect?.Invoke();
        }

        private void Awake()
        {
            if (lineRenderer == null)
                gameObject.CanGetComponent(out lineRenderer);

            if (inputModule == null)
            {
                if (gameObject.CanGetComponent<AutoInputModule>(out var inputMod))
                {
                    inputModule = inputMod;
                }
                else if (!(inputModule = FindObjectOfType<AutoInputModule>()))
                {
                    EventSystem system;
                    if(!(system = FindObjectOfType<EventSystem>())) {
                        system.name = "UI Input Event System";
                        system = new GameObject().AddComponent<EventSystem>();
                    }
                    inputModule = system.gameObject.AddComponent<AutoInputModule>();
                }
            }
        }

        private void Update()
        {
            UpdateLine();
        }

        private void UpdateLine()
        {
            PointerEventData data = inputModule.GetData(pointerIndex);
            float targetLength = data.pointerCurrentRaycast.distance == 0 ? raycastLength : data.pointerCurrentRaycast.distance;

            if (data.pointerCurrentRaycast.distance != 0 && !hover)
            {
                // Fire the Unity event
                StartPoint?.Invoke();

                // Show the ray if autoShowTarget is on when they enter the canvas
                if (autoShowTarget) ShowRay(true);

                hover = true;
            }
            else if (data.pointerCurrentRaycast.distance == 0 && hover)
            {
                // Fire the Unity event
                StopPoint?.Invoke();

                // Hide the ray when they leave the canvas
                ShowRay(false);

                hover = false;
            }

            RaycastHit hit = CreateRaycast(targetLength);

            Vector3 endPosition = transform.position + (transform.forward * targetLength);

            if (hit.collider) endPosition = hit.point;

            //Handle the hitmarker
            hitPointMarker.transform.position = endPosition;

            //Handle the line renderer
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, endPosition);
        }

        private RaycastHit CreateRaycast(float dist)
        {
            RaycastHit hit;
            Ray ray = new Ray(transform.position, transform.forward);
            Physics.Raycast(ray, out hit, dist, UILayer);

            return hit;
        }

        private void ShowRay(bool show)
        {
            hitPointMarker.SetActive(show);
            lineRenderer.enabled = show;
        }

    }
}