using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// A simple free camera to be added to a Unity game object.
/// 
/// Keys:
///	wasd / arrows	- movement
///	q/e 			- up/down (local space)
///	r/f 			- up/down (world space)
///	pageup/pagedown	- up/down (world space)
///	hold shift		- enable fast movement mode
///	right mouse  	- enable free look
///	mouse			- free look / rotation
///     
/// </summary>
public class FreeCamera : MonoBehaviour
{
    /// <summary>
    /// Normal speed of camera movement.
    /// </summary>
    public float movementSpeed = 10f;

    /// <summary>
    /// Speed of camera movement when shift is held down,
    /// </summary>
    public float fastMovementSpeed = 100f;

    /// <summary>
    /// Sensitivity for free look.
    /// </summary>
    public float freeLookSensitivity = 3f;

    /// <summary>
    /// Amount to zoom the camera when using the mouse wheel.
    /// </summary>
    public float zoomSensitivity = 10f;

    /// <summary>
    /// Amount to zoom the camera when using the mouse wheel (fast mode).
    /// </summary>
    public float fastZoomSensitivity = 50f;

    /// <summary>
    /// Set to true when free looking (on right mouse button).
    /// </summary>
    private bool FreeLooking = false;
    
    private bool lookingAtTaxi = false;

    private Transform tracing = null;

    private Vector3 DeltaPosition;

    void Update()
    {
        lookingAtTaxi = tracing != null;
        var fastMode = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        var movementSpeed = fastMode ? this.fastMovementSpeed : this.movementSpeed;

        if (lookingAtTaxi)
        {
            transform.position = tracing.position + DeltaPosition;
            transform.LookAt(tracing);
        }
        
        if (Input.GetMouseButtonDown(0))
        {
            if (!lookingAtTaxi)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit))
                {
                    GameObject go = hit.collider.gameObject;
                    if (go.CompareTag("TaxiMesh"))
                    {
                        tracing = go.transform;
                        DeltaPosition = transform.position - tracing.position;
                        lookingAtTaxi = true;
                        Debug.Log("following taxi");
                    }
                }
            } else
            {
                tracing = null;
                lookingAtTaxi = false;
                Debug.Log("start freelance");
            }
        }

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            transform.position = transform.position + -transform.right * (movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            transform.position = transform.position + transform.right * (movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            var x = transform.forward;
            transform.position = transform.position + new Vector3(x.x, 0, x.z) * (movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            var x = transform.forward;
            transform.position = transform.position + new Vector3(-x.x, 0, -x.z) * (movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.Q))
        {
            transform.position = transform.position + transform.up * (movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.E))
        {
            transform.position = transform.position + -transform.up * (movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.PageUp) || Input.GetKey(KeyCode.Space))
        {
            transform.position = transform.position + Vector3.up * (movementSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.PageDown) || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.LeftCommand))
        {
            transform.position = transform.position + -Vector3.up * (movementSpeed * Time.deltaTime);
        }

        if (FreeLooking && !lookingAtTaxi)
        {
            float newRotationX = transform.localEulerAngles.y + Input.GetAxis("Mouse X") * freeLookSensitivity;
            float newRotationY = transform.localEulerAngles.x - Input.GetAxis("Mouse Y") * freeLookSensitivity;
            transform.localEulerAngles = new Vector3(newRotationY, newRotationX, 0f);
        }
        
        float axis = Input.GetAxis("Mouse ScrollWheel");
        if (axis != 0)
        {
            var zoomSensitivity = fastMode ? this.fastZoomSensitivity : this.zoomSensitivity;
            transform.position = transform.position + transform.forward * (axis * zoomSensitivity);
        }
        
        if (Input.GetKeyDown(KeyCode.Mouse1))
        {
            StartLooking();
        }
        else if (Input.GetKeyUp(KeyCode.Mouse1))
        {
            StopLooking();
        }

        if(!lookingAtTaxi)
        {
            if (transform.position.y > 600)
            {
                var tmp = transform.position;
                transform.position = new Vector3(tmp.x, 600, tmp.z);
            }

            if (transform.position.y < 50)
            {
                var tmp = transform.position;
                transform.position = new Vector3(tmp.x, 50, tmp.z);
            }

            var rotEuler = transform.rotation.eulerAngles;
            if (rotEuler.x < 40)
            {
                transform.rotation = Quaternion.Euler(40, rotEuler.y, rotEuler.z);
            }
        }

        if (lookingAtTaxi)
        {
            DeltaPosition = transform.position - tracing.position;
        }
    }

    void OnDisable()
    {
        StopLooking();
    }

    /// <summary>
    /// Enable free looking.
    /// </summary>
    public void StartLooking()
    {
        FreeLooking = true;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    /// <summary>
    /// Disable free looking.
    /// </summary>
    public void StopLooking()
    {
        FreeLooking = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}