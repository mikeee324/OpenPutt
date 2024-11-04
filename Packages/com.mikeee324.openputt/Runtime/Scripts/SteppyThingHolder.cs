using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class SteppyThingHolder : UdonSharpBehaviour
{
    public Collider myCollider;
    public MeshRenderer myRenderer;

    void Start()
    {
        if (!Utilities.IsValid(myCollider))
            myCollider = GetComponent<Collider>();
        if (!Utilities.IsValid(myRenderer))
            myRenderer = GetComponent<MeshRenderer>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        myCollider.isTrigger = false;
        myRenderer.enabled = true;
    }

    private void OnCollisionExit(Collision collision)
    {
        myCollider.isTrigger = true;
        myRenderer.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        myCollider.isTrigger = false;
        myRenderer.enabled = true;
    }

    void OnTriggerExit(Collider other)
    {
        myCollider.isTrigger = true;
        myRenderer.enabled = false;
    }
}
