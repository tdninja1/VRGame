using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PhysicsHand  : MonoBehaviour
{
    [Header("PID")]
    [SerializeField] float frequency = 50f, rotFrequency = 100f;
    [SerializeField] float damping = 1f, rotDamping = 0.9f;
    [SerializeField] Rigidbody playerRigidbody;
    [SerializeField] Transform target;
    
    [Space]
    [Header("Hooke's Law")]
    [SerializeField] float climbForce = 1000f;
    [SerializeField] float climbDrag = 200f;

    [Space]
    [Header("Grabbing")]
    [SerializeField] InputActionReference grabReference; //
    
    [SerializeField] float distance = 0.5f;
    
    Vector3 _previousPosition;
    Rigidbody _rigidbody;
    bool _isColliding, _isAttemptingGrab;
    Collision _collision;
    
    
    void Start()
    {
        //teleport hands to controllers
        transform.position = target.position;
        transform.rotation = target.rotation;

        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.maxAngularVelocity = float.PositiveInfinity;

        _previousPosition = transform.position;

        grabReference.action.started += OnGrab;
        grabReference.action.canceled += OnRelease;

    }


    void OnDestroy()
    { //Undo grab reference as soon as grab reference is destroyed
        grabReference.action.started -= OnGrab;
        grabReference.action.canceled -= OnRelease;
    }
    
    void FixedUpdate()
    {
        PIDMovement();
        PIDRotation();
        if (_isColliding) HookesLaw();
        DistanceCheck();
    }

    void DistanceCheck()
    {
        if (Math.Abs(Vector3.Distance(target.position, transform.position)) > distance)
        {
            transform.position = target.position;
            transform.rotation = target.rotation;
        }
    }

    void PIDMovement()
    {
        float kp = (6f * frequency) * (6f * frequency) * 0.25f;
        float kd = 4.5f * frequency * damping;
        float g = 1 / (1 + kd * Time.fixedDeltaTime + kp * Time.fixedDeltaTime * Time.fixedDeltaTime);
        float ksg = kp * g;
        float kdg = (kd + kp * Time.fixedDeltaTime) * g;
        Vector3 force = (target.position - transform.position) * ksg +
                        (playerRigidbody.velocity - _rigidbody.velocity) * kdg;
        _rigidbody.AddForce(force, ForceMode.Acceleration);
    }
    void PIDRotation()
    {
        float kp = (6f * rotFrequency) * (6f * rotFrequency) * 0.25f;
        float kd = 4.5f * rotFrequency * rotDamping;
        float g = 1 / (1 + kd * Time.fixedDeltaTime + kp * Time.fixedDeltaTime * Time.fixedDeltaTime);
        float ksg = kp * g;
        float kdg = (kd + kp * Time.fixedDeltaTime) * g;
        Quaternion q = target.rotation * Quaternion.Inverse(transform.rotation);

        if (q.w < 0)
        {
            q.x = -q.x;
            q.y = -q.y;
            q.z = -q.z;
            q.w = -q.w;
        }

        q.ToAngleAxis(out float angle, out Vector3 axis);
        axis.Normalize();
        axis *= Mathf.Deg2Rad;
        Vector3 torque = ksg * axis * angle + -_rigidbody.angularVelocity * kdg;
        _rigidbody.AddTorque(torque, ForceMode.Acceleration);
    }

    void HookesLaw()
    {
        Vector3 displacementFromResting = transform.position - target.position;
        Vector3 force = displacementFromResting * climbForce;
        float drag = GetDrag();

        playerRigidbody.AddForce(force, ForceMode.Acceleration);
        playerRigidbody.AddForce(drag * -playerRigidbody.velocity * climbDrag, ForceMode.Acceleration);
    }

    float GetDrag()
    {
        Vector3 handleVelocity = (target.localPosition - _previousPosition) / Time.fixedDeltaTime;
        float drag = 1 / (handleVelocity.magnitude + 0.01f);
        drag = drag > 1 ? 1 : drag; // ? means to check if drag is greater than 1, if true, drag is one. Otherwise, set to drag.
        drag = drag < 0.03f ? 0.03f : drag; // ? means to check if drag is less than 0.03f, if so, set it to 0.03f, otherwise set it to drag
        // drag = Mathf.Clamp(drag, 0.03f, 1f); //makes sure drag is within 1f to 0.03f range
        _previousPosition = transform.position;
        return drag;
    }

    void OnCollisionEnter(Collision collision) 
    {
        _isColliding = true; //if entering, collision is true
        _collision = collision; //save the collision
    }

    void OnCollisionExit(Collision other)
    {
        _isColliding = false; //if exiting, collision is false
        _collision = null; //remove the collision
    }

    void OnGrab(UnityEngine.InputSystem.InputAction.CallbackContext ctx) 
    {//if colliding with an object with a rigidbody, do something until grab is successful
        _isAttemptingGrab = true;
        StartCoroutine(TryGrab());

    }

    IEnumerator TryGrab()
    {
        while (_isAttemptingGrab) 
        {
            if (_collision != null && _collision.gameObject.TryGetComponent(out Rigidbody rb)) 
            {
                FixedJoint joint = _rigidbody.AddComponent<FixedJoint>();
                joint.connectedBody = rb;
                _isAttemptingGrab = false;
            }
            yield return null; //tries while loop multiple times
        }
    }

    void OnRelease(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        _isAttemptingGrab = false;

        FixedJoint joint = GetComponent<FixedJoint>();

        if (joint != null)
        {
            Destroy(joint);
        }
    }
}
