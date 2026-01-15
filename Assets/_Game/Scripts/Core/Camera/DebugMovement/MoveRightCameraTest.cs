using System;
using UnityEngine;

public class MoveRightCameraTest : MonoBehaviour
{
    [SerializeField] private float speed = 1f;
    
    // Update is called once per frame
    void Update()
    {
        transform.position += transform.right * (speed * Time.deltaTime);
    }
}