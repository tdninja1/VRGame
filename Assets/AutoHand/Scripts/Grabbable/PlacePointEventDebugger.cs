using Autohand;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlacePoint))]
public class PlacePointEventDebugger : MonoBehaviour
{
    PlacePoint placePoint;

    void OnEnable()
    {
        placePoint = GetComponent<PlacePoint>();
        placePoint.OnPlaceEvent += (PlacePoint point, Grabbable grabbable) => { Debug.Log("On Place"); };
        placePoint.OnRemoveEvent += (PlacePoint point, Grabbable grabbable) => { Debug.Log("On Remove"); };
        placePoint.OnHighlightEvent += (PlacePoint point, Grabbable grabbable) => { Debug.Log("On Highlight"); };
        placePoint.OnStopHighlightEvent += (PlacePoint point, Grabbable grabbable) => { Debug.Log("On Stop Highlight"); };
    }


    void OnDisable()
    {
        placePoint = GetComponent<PlacePoint>();
        placePoint.OnPlaceEvent -= (PlacePoint point, Grabbable grabbable) => { Debug.Log("On Place"); };
        placePoint.OnRemoveEvent -= (PlacePoint point, Grabbable grabbable) => { Debug.Log("On Remove"); };
        placePoint.OnHighlightEvent -= (PlacePoint point, Grabbable grabbable) => { Debug.Log("On Highlight"); };
        placePoint.OnStopHighlightEvent -= (PlacePoint point, Grabbable grabbable) => { Debug.Log("On Stop Highlight"); };
    }
}
