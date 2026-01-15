using System;
using UnityEngine;

public class RotateCameraTest : MonoBehaviour
{
    [SerializeField] private float radius = 5f;
    [SerializeField] private float speed = 1f;

    private Vector3 startPosition;

    private float lastTime = 0f;
    private float latency = 0f;
    
    private void Start()
    {
        startPosition = transform.position;
    }

    private void OnEnable()
    {
        latency += Time.time - lastTime;
    }

    // Update is called once per frame
    void Update()
    {
        var time = Time.time - latency;
        
        transform.position = startPosition +
                             (transform.up * Mathf.Sin(time * speed) +
                              transform.right * (Mathf.Cos(time * speed) - 1)) * radius;
        lastTime = Time.time;
    }
}