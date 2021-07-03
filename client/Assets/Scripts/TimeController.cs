using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TimeController : MonoBehaviour
{
    public int startTimestamp;
    public Dropdown speedSelector;
    public Dispatcher dispatcher;
    public Text current;
    public Text loading;
    public Slider slider;
    public Text SliderText;
    public Toggle syncTimeWithSlider;
    public float _speed;
    public ComputeShader taxiPos;
    private double CurrentTimestamp;
    private bool Paused = true;
    private string PauseMessage = "Loading map...";
    public readonly DateTime ZeroDateTime =
        new DateTime(1970,1,1,0,0,0,0,System.DateTimeKind.Utc);

    // Start is called before the first frame update
    private void Start()
    {
        CurrentTimestamp = startTimestamp;
        dispatcher.transmittedTimestamp = startTimestamp;
        SetSpeed();
    }

    // Update is called once per frame
    private void FixedUpdate()
    {
        var timestring = ZeroDateTime.AddSeconds(CurrentTimestamp).ToLocalTime().ToString();
        current.text = timestring;
        loading.text = ZeroDateTime.AddSeconds(dispatcher.transmittedTimestamp).ToLocalTime().ToString();
        if (Paused) return;
        if (CurrentTimestamp + 1 >= dispatcher.transmittedTimestamp) return;
        var delta = _speed * Time.fixedDeltaTime;
        CurrentTimestamp += delta;
        if (syncTimeWithSlider.isOn)
        {
            slider.value = (int) CurrentTimestamp;
            SliderText.text = timestring;
        }
    }

    public void Pause(string message)
    {
        PauseMessage = message;
        Paused = true;
    }

    public void Resume()
    {
        PauseMessage = "";
        Paused = false;
    }

    public double GetCurrentTime()
    {
        return CurrentTimestamp;
    }

    public void SetSpeed()
    {
        _speed = (float) Math.Pow(2, speedSelector.value + 1);
        Debug.Log($"set speed {_speed}");
    }

    public void ResetTime(int timestamp)
    {
        CurrentTimestamp = timestamp;
    }
}