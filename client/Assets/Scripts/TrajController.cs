using System;
using System.Collections;
using System.Collections.Generic;
using Google.Maps;
using UnityEngine;
using UnityEngine.Serialization;
using Google.Maps.Coord;

public struct TaxiProperty
{
    public Vector3 start_pos, end_pos;
    public int start_ts, end_ts;
};

public class TrajController : MonoBehaviour
{
    public string uid;
    public List<Vector3> points;
    public List<int> times;
    public Transform target;
    public GameObject mesh;
    public MapsService mapsService;
    public TimeController timeController;
    public bool useGPU = false;
    public bool useLegacyUpdate = true;
    private Vector3 StartPoint, EndPoint;
    private int StartTs, EndTs;
    private float distance, Speed;
    private bool Finished;
    private int _lastPhase = -1;
    public TaxiProperty property;
    public bool Enabled = true;

    // Start is called before the first frame update
    private void Start()
    {
        var map = GameObject.FindGameObjectWithTag("Map");
    }

    // Update is called once per frame
    private void Update()
    {
        if (!Enabled) return;
        if (useGPU)
        {
            Update_GPU();
            return;
        }

        if (useLegacyUpdate)
        {
            Update_Legacy();
            return;
        }
        
        if (2 <= points.Count && times.Count == points.Count)
        {
            var currentTime = timeController.GetCurrentTime();

            if (currentTime < times[0]) // taxi should not move before start timestamp
            {
                target.position = points[0];
                mesh.SetActive(false);
                return;
            }

            mesh.SetActive(true);

            int currentPhase = -1;
            for (int i = 0; i < points.Count - 1; ++i)
            {
                if (times[i] <= currentTime && currentTime < times[i + 1])
                {
                    currentPhase = i;
                    break;
                }
            }

            if (currentPhase < 0)
            {
                Destroy(this.gameObject);
            }

            if (currentPhase == _lastPhase)
            {
                var timeDelta = (EndTs - StartTs) / timeController._speed;
                Speed = distance / timeDelta;
                target.position = Vector3.MoveTowards(target.position, EndPoint,
                    Speed * Time.deltaTime);
            }
            else
            {
                StartPoint = points[currentPhase];
                EndPoint = points[currentPhase + 1];
                StartTs = times[currentPhase];
                EndTs = times[currentPhase + 1];
                distance = Vector3.Distance(StartPoint, EndPoint);
                var timeDelta = (EndTs - StartTs) / timeController._speed;
                Speed = distance / timeDelta;
                target.position = Vector3.MoveTowards(StartPoint, EndPoint,
                    Speed * Time.deltaTime);
                target.rotation = Quaternion.LookRotation(EndPoint - StartPoint);
                _lastPhase = currentPhase;
            }
        }
        else
        {
            // Debug.LogError("From TrajController: Invalid length of points!");
        }
    }

    private void Update_Legacy()
    {
        if (2 <= points.Count && times.Count == points.Count)
        {
            var currentTime = timeController.GetCurrentTime();
            if (currentTime<times[0]) // taxi should not move before start timestamp
            {
                target.position = points[0];
                mesh.SetActive(false);
                return;
            }
            
            mesh.SetActive(true);
            
            if (times[0] <= currentTime && currentTime < times[1]) // begin to move after start timestamp
            {
                target.position = Vector3.MoveTowards(target.position, EndPoint,
                    Speed * Time.deltaTime);
            }
            else
            {
                if (3 <= points.Count)
                {
                    StartPoint = points[1];
                    EndPoint = points[2];
                    StartTs = times[1];
                    EndTs = times[2];
                    var distance = Vector3.Distance(StartPoint, EndPoint);
                    var timeDelta = (EndTs - StartTs) / timeController._speed;
                    Speed = distance / timeDelta;
                    target.position = Vector3.MoveTowards(StartPoint, EndPoint,
                        Speed * Time.deltaTime);
                    target.rotation = Quaternion.LookRotation(EndPoint - StartPoint);
                    points.RemoveAt(0);
                    times.RemoveAt(0);
                }
                else if (Finished)
                {
                    Destroy(gameObject);
                    // gameObject.SetActive(false);
                    // mesh.SetActive(false);
                    // points.Clear();
                    // times.Clear();
                    // Enabled = false;
                }
            }
        }
        else
        {
            // Debug.LogError("From TrajController: Invalid length of points!");
        }
    }

    private void Update_GPU()
    {
        var currentTime = timeController.GetCurrentTime();
        mesh.SetActive(currentTime >= times[0]);
        if (currentTime > times[1])
        {
            if (times.Count <= 2)
            {
                points.Clear();
                times.Clear();
                mesh.SetActive(false);
                return;
            }

            property.start_pos = points[1];
            property.end_pos = points[2];
            points.RemoveAt(0);
            property.start_ts = times[1];
            property.end_ts = times[2];
            times.RemoveAt(0);
        }
    }

    public void AddInstruction(Instruction instruction)
    {
        var empty = 0 == points.Count;
        if (empty)
        {
            var start = new LatLng(instruction.StartLat, instruction.StartLng);
            var startVector = mapsService.Projection.FromLatLngToVector3(start);
            points.Add(startVector);
            times.Add(instruction.StartTs);
        }

        var end = new LatLng(instruction.EndLat, instruction.EndLng);
        var endVector = mapsService.Projection.FromLatLngToVector3(end);
        points.Add(endVector);
        times.Add(instruction.EndTs);
        if (empty)
        {
            StartPoint = points[0];
            EndPoint = points[1];
            StartTs = times[0];
            EndTs = times[1];
            distance = Vector3.Distance(StartPoint, EndPoint);
            var timeDelta = (EndTs - StartTs) / timeController._speed;
            Speed = distance / timeDelta;
            target.position = Vector3.MoveTowards(StartPoint, EndPoint,
                Speed * Time.deltaTime);
            target.rotation = Quaternion.LookRotation(EndPoint - StartPoint);
        }
    }
    
    public void AddInstruction(Instruction instruction, Vector3 endVector)
    {
        var empty = 0 == points.Count;
        if (empty)
        {
            var start = new LatLng(instruction.StartLat, instruction.StartLng);
            var startVector = mapsService.Projection.FromLatLngToVector3(start);
            points.Add(startVector);
            times.Add(instruction.StartTs);
        }
        points.Add(endVector);
        times.Add(instruction.EndTs);
        if (instruction.IsEndInstruction)
        {
            Finished = true;
        }
        if (empty)
        {
            StartPoint = points[0];
            EndPoint = points[1];
            StartTs = times[0];
            EndTs = times[1];
            distance = Vector3.Distance(StartPoint, EndPoint);
            var timeDelta = (EndTs - StartTs) / timeController._speed;
            Speed = distance / timeDelta;
            target.position = Vector3.MoveTowards(StartPoint, EndPoint,
                Speed * Time.deltaTime);
            target.rotation = Quaternion.LookRotation(EndPoint - StartPoint);
        }
    }

    public void AddInstruction_GPU(Instruction instruction)
    {
        
    }
}