using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Transactions;
using Google.Maps;
using Google.Maps.Coord;
using Google.Maps.Examples;
using Google.Protobuf.Collections;
using Grpc.Core;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;


public class Dispatcher : MonoBehaviour
{
    enum DownloadStatus
    {
        Done,
        RequestDownload,
        Downloading,
        Downloaded,
        Applying
    }

    public GameObject prefab;
    public GameObject prefabParent;
    public Camera freeCamera;
    public int transmittedTimestamp = -1;
    public int transmitBatch = 100;
    public int preloadBuffer = 500;

    public TimeController timeController;

    // public MapController mapControler;
    public MapController mapController;
    public Text status;
    private DownloadStatus downloadStatus;
    public Text numOfTaxi;
    public Text taxiDestroyed;
    public Slider timeSlider;
    public int ApplyInstructionsPerFrame = 50;
    [SerializeField] ComputeShader computeShader = default;
    private Dictionary<string, TrajController> _objects = new Dictionary<string, TrajController>();
    private List<TrajController> _sleepingobjects = new List<TrajController>();
    private TrajVis3D.TrajVis3DClient _client;
    private Channel _channel;
    private readonly string _server = "127.0.0.1:9623";
    private bool Transmitting = false;
    private int TaxiDestroyedCount = 0;
    private Vector2 FloatingOrigin;
    private float MercatorScale;
    private RepeatedField<Instruction> insts;
    private bool propertiesLoaded = false;

    private readonly int
        ShaderLatLngsPositionID = Shader.PropertyToID("lat_lngs"),
        ShaderResultPositionID = Shader.PropertyToID("result"),
        ShaderFloatingOriginPositionID = Shader.PropertyToID("floating_origin"),
        ShaderMercatorScalePositionID = Shader.PropertyToID("mercator_scale");

    private int ShaderLatLngToVectorKernelID;

    struct TaxiEntity
    {
        public string Uid;
        public List<Vector3> Points;
        public List<uint> Times;
        public bool Terminated;
    }

    private Dictionary<string, TaxiEntity> TaxiEntities = new Dictionary<string, TaxiEntity>();

    private async Task<int> GetInstructionsBetween()
    {
        Transmitting = true;
        downloadStatus = DownloadStatus.Downloading;
        status.text = "开始载入";
        TimePeriod period = new TimePeriod();
        period.StartTs = transmittedTimestamp;
        period.EndTs = transmittedTimestamp + transmitBatch;
        status.text = "正在下载";
        // var call = _client.GetInstructionsBetween(period);
        // var stream = call.ResponseStream;
        // insts.Clear();
        // while (await stream.MoveNext())
        // {
        //     insts.Add(stream.Current);
        // }
        var call = await _client.GetInstructionSetBetweenAsync(period);
        insts = call.Instructions;
        downloadStatus = DownloadStatus.Downloaded;
        status.text = "下载完成";
        return 0;
    }

    private int GetProperties()
    {
        propertiesLoaded = false;
        var empty = new Empty();
        var call = _client.GetProperties(empty);
        timeSlider.minValue = call.FirstTimestamp;
        timeSlider.maxValue = call.LastTimestamp;
        timeSlider.value = call.FirstTimestamp;
        timeController.ResetTime(call.FirstTimestamp);
        transmittedTimestamp = call.FirstTimestamp - 1;
        mapController.latLng = new LatLng(call.CenterLat, call.CenterLng);
        mapController.bounds = new Bounds(Vector3.zero, new Vector3(call.LengthX, 0, call.LengthZ));
        mapController.LoadMap();
        BeginDispatch();
        return 0;
    }

    public void SetTimeSliderTimestampValue(Text sliderValue)
    {
        sliderValue.text = timeController.ZeroDateTime.AddSeconds(timeSlider.value).ToLocalTime().ToString();
    }

    public void ChangeTime()
    {
        int timestamp = (int) timeSlider.value;
        foreach (var d in _objects.Values)
        {
            Destroy(d.gameObject);
        }

        _objects.Clear();
        timeController.ResetTime(timestamp);
        transmittedTimestamp = timestamp - 1;
        timeController.syncTimeWithSlider.isOn = true;
    }

    internal IEnumerator ApplyInstructionsBetween()
    {
        status.text = "正在应用";
        downloadStatus = DownloadStatus.Applying;
        int instLength = insts.Count;

        if (instLength > 0)
        {
            var _instdict = new Dictionary<string, List<Tuple<Instruction, Vector3>>>();
            Vector2[] insts_gps = new Vector2[instLength];

            for (int i = 0; i < instLength; ++i)
            {
                insts_gps[i].x = insts[i].EndLat;
                insts_gps[i].y = insts[i].EndLng;
            }

            var latLngComputeBuffer = new ComputeBuffer(instLength, sizeof(float) * 2);
            var resultComputeBuffer = new ComputeBuffer(instLength, sizeof(float) * 3);
            Graphics.SetRandomWriteTarget(1, latLngComputeBuffer, true);
            latLngComputeBuffer.SetData(insts_gps);

            computeShader.SetVector(ShaderFloatingOriginPositionID, FloatingOrigin);
            computeShader.SetFloat(ShaderMercatorScalePositionID, MercatorScale);
            computeShader.SetBuffer(ShaderLatLngToVectorKernelID, ShaderLatLngsPositionID, latLngComputeBuffer);
            computeShader.SetBuffer(ShaderLatLngToVectorKernelID, ShaderResultPositionID, resultComputeBuffer);
            int groups = Mathf.CeilToInt(instLength / 64f);
            computeShader.Dispatch(ShaderLatLngToVectorKernelID, groups, 1, 1);

            Vector3[] insts_pos = new Vector3[instLength];
            latLngComputeBuffer.Release();
            resultComputeBuffer.GetData(insts_pos);
            resultComputeBuffer.Release();

            for (int i = 0; i < instLength; ++i)
            {
                var inst = insts[i];
                if (!_instdict.ContainsKey(inst.Uid))
                {
                    _instdict.Add(inst.Uid, new List<Tuple<Instruction, Vector3>>());
                }

                _instdict[inst.Uid].Add(new Tuple<Instruction, Vector3>(inst, insts_pos[i]));
            }

            var taxiInsCount = _instdict.Values.Count;
            Debug.Log($"apply {taxiInsCount} taxi's instructions");
            int count = 0;
            foreach (var val in _instdict.Values)
            {
                AddInstructionToTrajController(val);
                count++;
                if (count == ApplyInstructionsPerFrame)
                {
                    count = 0;
                    yield return 0;
                }
            }
            status.text = $"新载入下{transmitBatch}秒的轨迹数据，包含{taxiInsCount}辆出租车的信息";
        }
        else
        {
            status.text = $"新载入下{transmitBatch}秒的轨迹数据，包含{0}辆出租车的信息";
        }

        transmittedTimestamp = transmittedTimestamp + transmitBatch;
        Transmitting = false;
        downloadStatus = DownloadStatus.Done;
        yield return 0;
    }

    async Task<int> AddInstructionToTrajController(List<Tuple<Instruction, Vector3>> instructions)
    {
        var key = instructions[0].Item1.Uid;
        TrajController newTrajController = null;
        if (!_objects.ContainsKey(key))
        {
            // bool reuse = false;
            // var sleepingCount = _sleepingobjects.Count;
            // for (int i = 0; i < sleepingCount; ++i)
            // {
            //     if (_sleepingobjects[i].Enabled == false)
            //     {
            //         newTrajController = _sleepingobjects[i];
            //         newTrajController.Enabled = true;
            //         newTrajController.gameObject.SetActive(true);
            //         newTrajController.mesh.SetActive(true);
            //         reuse = true;
            //         Debug.Log($"reuse #{i} of {sleepingCount}");
            //         _sleepingobjects.RemoveAt(i);
            //         _objects.Add(key, newTrajController);
            //         break;
            //     }
            //     Debug.Log($"all {sleepingCount} sleeping taxi are still running!");
            // }
            // if (!reuse)
            // {
            newTrajController = Instantiate(prefab,
                    new Vector3(0, 0, 0),
                    Quaternion.identity,
                    prefabParent.transform)
                .GetComponent<TrajController>();
            _objects.Add(key, newTrajController);
            // }
        }
        else
        {
            newTrajController = _objects[key];
        }

        // if (!(newTrajContoller is null)) StartCoroutine(CallAddInstruction(newTrajContoller, instruction));
        if (!(newTrajController is null))
            foreach (var instruction in instructions)
                newTrajController.AddInstruction(instruction.Item1, instruction.Item2);
        else throw new Exception("Invalid Traj Controller!");

        if (instructions[instructions.Count - 1].Item1.IsEndInstruction)
        {
            _objects.Remove(key);
            _sleepingobjects.Add(newTrajController);
            TaxiDestroyedCount++;
        }

        return 0;
    }

    // Start is called before the first frame update
    void Start()
    {
        _channel = new Channel(_server, ChannelCredentials.Insecure);
        _client = new TrajVis3D.TrajVis3DClient(_channel);

        GetProperties();
    }

    public void BeginDispatch()
    {
        // var latLng = mapController.LatLng;
        var latLng = mapController.latLng;

        double num = Math.Sin(latLng.Lat * (Math.PI / 180.0));
        FloatingOrigin = new Vector2(
            (float) latLng.Lng * ((float) Math.PI / 180f) * 6378137.0f,
            0.5f * (float) Math.Log((1.0 + num) / (1.0 - num)) * 6378137.0f);

        MercatorScale = (float) 1.0 / (float) Math.Cos(latLng.Lat * (Math.PI / 180.0));

        ShaderLatLngToVectorKernelID = computeShader.FindKernel("LatLngToVector");

        propertiesLoaded = true;
    }

    // Update is called once per frame
    async void Update()
    {
        if (!propertiesLoaded) return;
        if (timeController.GetCurrentTime() + preloadBuffer > transmittedTimestamp)
        {
            if (downloadStatus == DownloadStatus.Done)
            {
                Debug.Log($"query {transmittedTimestamp}+{transmitBatch}");
                GetInstructionsBetween();
            }
            else if (downloadStatus == DownloadStatus.Downloaded)
            {
                StartCoroutine(ApplyInstructionsBetween());
            }
        }
    }

    private void FixedUpdate()
    {
        taxiDestroyed.text = $"已完成的订单数：{TaxiDestroyedCount}";
        numOfTaxi.text = $"地图上的出租车数量：{_objects.Count}";
    }
}