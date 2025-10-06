using UnityEngine;

public class SlateSpawner : MonoBehaviour
{
    [Header("Assign the Slate prefab from MRTK3 UX/Prefabs")]
    public GameObject slatePrefab;

    [Tooltip("Distance in front of the user to spawn the slate.")]
    public float spawnDistance = 1.0f;

    private GameObject spawnedSlate;

    void Start()
    {
        SpawnSlateInFrontOfUser();
    }

    public void SpawnSlateInFrontOfUser()
    {
        var slate = Instantiate(slatePrefab);
        slate.transform.position = Camera.main.transform.position + Camera.main.transform.forward * spawnDistance;
        slate.transform.rotation = Quaternion.LookRotation(slate.transform.position - Camera.main.transform.position);
    }
}
