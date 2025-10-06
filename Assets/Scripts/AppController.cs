using System.Threading;
using UnityEngine;

public class AppController : MonoBehaviour
{

    void Start()
    {

    }

    void Update()
    {
        
    }

    public void OnExitButtonClicked()
    {
        Debug.Log("Exit button clicked. Stopping client and quitting...");
        Application.Quit();
    }
}
