using UnityEngine;

[DefaultExecutionOrder(100)]   // run after the SDK has updated the target
public class SurfaceHand : MonoBehaviour
{
    public Transform target;               // openxr hand root of the ORIGINAL hand
    public Vector3 posOffset;
    public Vector3 rotOffsetEuler;
    public LayerMask obstacleMask;         // Animals
    public BoxCollider box;                // this hand's trigger box
    public float skin = 0.002f;            // small gap so we never rest exactly in contact
    public int maxSlides = 4;
    public float snapDistance = 0.3f;      // snap when the goal is free and far away

    Vector3 pos;
    Quaternion rot;
    bool init;

    void Awake()
    {
        if (!box) box = GetComponent<BoxCollider>();
    }

    void LateUpdate()
    {
        Vector3 goalPos = target.TransformPoint(posOffset);
        Quaternion goalRot = target.rotation * Quaternion.Euler(rotOffsetEuler);

        if (!init) { pos = goalPos; init = true; }
        rot = goalRot;

        // goal free and far away (tracking jump, or you came out the far side): snap
        if ((goalPos - pos).magnitude > snapDistance && !Overlaps(goalPos))
            pos = goalPos;

        Depenetrate();

        Vector3 half = box.size * 0.5f;
        Vector3 remaining = goalPos - pos;
        for (int i = 0; i < maxSlides && remaining.sqrMagnitude > 1e-8f; i++)
        {
            Vector3 dir = remaining.normalized;
            float dist = remaining.magnitude;
            Vector3 center = pos + rot * box.center;

            if (Physics.BoxCast(center, half, dir, out RaycastHit hit, rot, dist + skin,
                                obstacleMask, QueryTriggerInteraction.Ignore))
            {
                float move = Mathf.Max(0f, hit.distance - skin);
                pos += dir * move;
                remaining = Vector3.ProjectOnPlane(dir * (dist - move), hit.normal);
            }
            else
            {
                pos += remaining;
                break;
            }
        }

        Depenetrate();
        transform.SetPositionAndRotation(pos, rot);
    }

    bool Overlaps(Vector3 p)
    {
        Vector3 c = p + rot * box.center;
        return Physics.CheckBox(c, box.size * 0.5f, rot, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    void Depenetrate()
    {
        for (int iter = 0; iter < 4; iter++)
        {
            Vector3 c = pos + rot * box.center;
            Collider[] hits = Physics.OverlapBox(c, box.size * 0.5f, rot, obstacleMask, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return;

            foreach (Collider o in hits)
            {
                if (Physics.ComputePenetration(box, pos, rot, o, o.transform.position, o.transform.rotation,
                                               out Vector3 dir, out float d))
                    pos += dir * (d + skin);
            }
        }
    }
}