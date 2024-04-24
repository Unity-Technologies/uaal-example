using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.UI;
using UnityEngine;

#if UNITY_IOS || UNITY_TVOS
public class NativeAPI {
    [DllImport("__Internal")]
    public static extern void showHostMainWindow(string lastStringColor);
}
#endif

public class Cube : MonoBehaviour
{
    public Text text;
    string lastStringColor = "";

    void AppendToText(string line)
    {
        text.text += line + "\n";
    }

    void Update()
    {
        transform.Rotate(0, Time.deltaTime * 10, 0);

        if (Application.platform == RuntimePlatform.Android)
            if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
    }

    void ChangeColor(string newColor)
    {
        AppendToText("Changing Color to " + newColor);

        lastStringColor = newColor;

        if (newColor == "red") GetComponent<Renderer>().material.color = Color.red;
        else if (newColor == "blue") GetComponent<Renderer>().material.color = Color.blue;
        else if (newColor == "yellow") GetComponent<Renderer>().material.color = Color.yellow;
        else GetComponent<Renderer>().material.color = Color.black;
    }


    void ShowHostMainWindow()
    {
#if UNITY_ANDROID
        try
        {
            AndroidJavaClass jc = new AndroidJavaClass("com.unity.mynativeapp.SharedClass");
            jc.CallStatic("showMainActivity", lastStringColor);
        } catch(Exception e)
        {
            AppendToText("Exception during showHostMainWindow");
            AppendToText(e.Message);
        }
#elif UNITY_IOS || UNITY_TVOS
        NativeAPI.showHostMainWindow(lastStringColor);
#endif
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle("button");
        style.fontSize = 45;
        if (GUI.Button(new Rect(10, 10, 200, 100), "Red", style)) ChangeColor("red");
        if (GUI.Button(new Rect(10, 110, 200, 100), "Blue", style)) ChangeColor("blue");
        if (GUI.Button(new Rect(10, 300, 600, 100), "Show Main With Color", style)) ShowHostMainWindow();

        if (GUI.Button(new Rect(10, 400, 400, 100), "Unload", style)) Application.Unload();
        if (GUI.Button(new Rect(440, 400, 400, 100), "Quit", style)) Application.Quit();
    }
}
