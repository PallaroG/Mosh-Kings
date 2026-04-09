using UnityEngine;

public class Billboardhand : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        transform.position = GetComponent<Camera>().transform.position + GetComponent<Camera>().transform.forward * 0.5f 
                        + GetComponent<Camera>().transform.right * 0.3f 
                        + GetComponent<Camera>().transform.up * -0.3f;

        transform.rotation = GetComponent<Camera>().transform.rotation;
    }

}   