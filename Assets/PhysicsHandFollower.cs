using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PhysicsHandFollower : MonoBehaviour
{
    public Transform target;
    public Vector3 posOffset;
    public Vector3 rotOffsetEuler;

    public float maxSpeed = 25f, teleportDistance = 0.75f;
    public float contactGain = 0.15f, contactMaxSpeed = 1.5f;
    public float blockedTeleportDistance = 0.3f;

    public LayerMask obstacleMask;   // set to Animals in the Inspector
    public Vector3 palmHalfExtents = new Vector3(0.05f, 0.025f, 0.0375f);
    public Vector3 palmCenter = new Vector3(-0.05f, 0.01f, 0f);

    Rigidbody rb;
    bool touching;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.maxDepenetrationVelocity = 1.5f;
        rb.angularDamping = 20f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void OnCollisionStay(Collision c) { touching = true; }

    void FixedUpdate()
    {
        Vector3 goalPos = target.TransformPoint(posOffset);
        Quaternion goalRot = target.rotation * Quaternion.Euler(rotOffsetEuler);
        Vector3 delta = goalPos - rb.position;

        rb.angularVelocity = Vector3.zero;

        float limit = touching ? blockedTeleportDistance : teleportDistance;
        if (delta.magnitude > limit)
        {
            Vector3 checkPos = goalPos + goalRot * palmCenter;
            bool goalInside = Physics.CheckBox(checkPos, palmHalfExtents, goalRot,
                                               obstacleMask, QueryTriggerInteraction.Ignore);
            if (!goalInside)
            {
                rb.position = goalPos;
                rb.rotation = goalRot;
                rb.linearVelocity = Vector3.zero;
                touching = false;
                return;
            }
        }

        float gain = touching ? contactGain : 1f;
        float maxV = touching ? contactMaxSpeed : maxSpeed;
        rb.linearVelocity = Vector3.ClampMagnitude(delta / Time.fixedDeltaTime * gain, maxV);
        rb.MoveRotation(goalRot);
        touching = false;
    }
}