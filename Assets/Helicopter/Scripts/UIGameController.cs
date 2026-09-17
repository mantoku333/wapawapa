using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.XR;

public class UIGameController : MonoBehaviour
{
    public Text EngineForceView;
    public GameObject RestartButton;
    public GameObject InfoButton;
    public GameObject InfoPanel;
    public float VrCanvasDistance = 1.5f;

	// Use this for initialization
    public static UIGameController runtime;

    private void Awake()
    {
        runtime = this;
    }

    void Start ()
	{
        ConfigureCanvasForVr();
	    ShowInfo();
	}
	
	// Update is called once per frame
	void Update () {
	
	}

    private void ShowInfoPanel(bool isShow)
    {
        EngineForceView.gameObject.SetActive(!isShow);
        RestartButton.SetActive(!isShow);
        InfoButton.SetActive(!isShow);
        InfoPanel.SetActive(isShow);
    }

    public void ShowInfo()
    {
        ShowInfoPanel(true);
    }
    public void HideInfo()
    {
        ShowInfoPanel(false);
    }

    public void RestartGame()
    {
        Application.LoadLevel("Main");
    }

    private void ConfigureCanvasForVr()
    {
        if (!XRSettings.enabled)
            return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        var xrCamera = Camera.main;
        if (xrCamera == null)
            xrCamera = FindObjectOfType<Camera>();

        if (xrCamera == null)
            return;

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = xrCamera;
        canvas.planeDistance = VrCanvasDistance;
        canvas.sortingOrder = 100;
    }
}
