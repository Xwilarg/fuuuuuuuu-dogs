using NordicGameJam.Player;
using System.Collections.Generic;
using NordicGameJam.SO;
using UnityEngine;

public class GravityPath : MonoBehaviour
{
    public GameObject PathVisualPrefab;
    public float PathSpeed;
    public Vector3 CurrentMomentum {get => PathMomentum;}

    [SerializeField]
    private PlayerInfo _info;

    private Transform PathVisual;
    public Vector3 PathMomentum { private set; get; }

    private PlayerController _pc;

    private const float G = 1f;

    private Camera _cam;

    private void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _cam = Camera.main;
    }

    // http://answers.unity.com/answers/502236/view.html
    public static Bounds CalculateBounds(Camera cam)
    {
        float screenAspect = Screen.width / (float)Screen.height;
        float cameraHeight = cam.orthographicSize * 2;
        Bounds bounds = new(
            cam.transform.position,
            new Vector3(cameraHeight * screenAspect, cameraHeight, 0));
        return bounds;
    }

    void Start()
    {
        PathVisual = Instantiate(PathVisualPrefab, transform.position, Quaternion.identity).transform;
        PathVisual.SetParent(transform);
        PathMomentum = Vector3.right;
        PathSpeed = 0;
    }

    void FixedUpdate()
    {
        RenderPath(_info.DistAhead, _pc.TheoricalSpeed01);
        if (_pc.DidMove)
        {
            (transform.position, PathMomentum) = PathDelta(transform.position, PathMomentum, Time.fixedDeltaTime * PathSpeed, GetAttractors(), _pc.Speed01);
        }
    }

    private List<Attractor> GetAttractors()
    {
        List<Attractor> attractors = new List<Attractor>();
            foreach(Attractor a in GameObject.FindObjectsOfType<Attractor>())
                if(a.Activated)
                    attractors.Add(a);
        return attractors;
    }

    public void SetVisualMomentum(Vector3 dir)
    {
        PathMomentum = dir;
    }

    private void RenderPath(float distAhead, float relSpeed)
    {
        float delta = _info.TimeStep;
        float simLength = 0;

        PathVisual.position = transform.position;
        LineRenderer line = PathVisual.GetComponent<LineRenderer>();
        line.positionCount = 0;

        List<Attractor> attractors = GetAttractors();
        
        Vector3 momentum = PathMomentum;
        Vector3 position = transform.position;

        int i = 0;
        while(simLength < distAhead)
        {
            line.positionCount += 1;
            line.SetPosition(i, position);
            i++;
            
            Vector3 next;
            (next, momentum) = PathDelta(position, momentum, delta, attractors, relSpeed);
            float step = Vector3.Distance(position, next);
            if(simLength+step < distAhead)
                position = next;
            else
                position += (next - position).normalized * (distAhead-simLength);
            simLength += step;
        }
    }

    private (Vector3, Vector3) PathDelta(Vector3 pos, Vector3 momentum, float deltaT, List<Attractor> attractors, float relSpeed)
    {
        Vector3 m = momentum;
        foreach(Attractor att in attractors)
        {
            Vector3 dir = (att.transform.position - pos);
            float falloff = dir.magnitude >= att.MaxAttractionDistance ? Mathf.Exp(-10 * (dir.magnitude-att.MaxAttractionDistance)) : 1;

            dir = dir.normalized;
            m += dir * (1 / (dir.magnitude * dir.magnitude)) * G * att.Strenght * deltaT * falloff * (_info.UseForce ? (1f - relSpeed) : 1f);
        }

        Vector3 delta = m*deltaT;

        var bounds = CalculateBounds(_cam);
        bounds.min += new Vector3(2f, 2f);
        bounds.max -= new Vector3(2f, 2f);

        (bool hitBounds, Vector3 normal, float upto) = BoundsHit(pos, pos+(m*deltaT), bounds);
        if(hitBounds)
        {
            Vector3 deltaM = m-momentum;
            m = momentum + deltaM * (upto/(m.magnitude*deltaT));
            m = Vector3.Reflect(m, normal);
            m += deltaM * (1 - (upto/(m.magnitude*deltaT)));
            delta = delta.normalized * upto + m.normalized * (delta.magnitude-upto);
        }

        return (pos+delta, m);
    }

    private (bool, Vector3, float) BoundsHit(Vector3 before, Vector3 after, Bounds b)
    {
        Vector3 delta = after-before;

        if(after.x < b.min.x)
            return (true, Vector3.right, delta.magnitude * ((b.min.x-before.x) / (after.x-before.x)));
        if(after.y < b.min.y)
            return (true, Vector3.up, delta.magnitude * ((b.min.y-before.y) / (after.y-before.y)));
        if(after.x > b.max.x)
            return (true, Vector3.left, delta.magnitude * ((b.max.x-before.x) / (after.x-before.x)));
        if(after.y > b.max.y)
            return (true, Vector3.down, delta.magnitude * ((b.max.y-before.y) / (after.y-before.y)));
        return (false, Vector3.zero, 0);
    }
}
