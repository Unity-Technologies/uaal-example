using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine.UI;
using UnityEngine;

using UaaL;

[UaaLiOSHostInterface]
public interface NativeAPI {
    int showHostMainWindow(string lastStringColor);
}

// https://github.com/Unity-Technologies/iOSNativeCodeSamples/tree/2019-dev/NativeIntegration/UI/OverlayUI
[UaaLiOSUaaLInterface]
public interface ScriptingAPI {
    void appendToText(string line);
}

public class Cube : MonoBehaviour, ScriptingAPI
{
    public Text text;    
    NativeAPI nativeAPI = UaaLPlugin.getInstance<NativeAPI>();

    public void appendToText(string line) { text.text += line + "\n"; }

    Cube() {
        UaaLPlugin.setInterface<ScriptingAPI>(this);
    }

    void Update()
    {
        //CScriptingAPI.ShowNativeDatePicker(CScriptingAPI.DateSelectedCallback);
                
        transform.Rotate(0, Time.deltaTime*10, 0);
        
        if (Application.platform == RuntimePlatform.Android)
            if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
    }

    string lastStringColor = "";
    void ChangeColor(string newColor)
    {
        appendToText( "Chancing Color to " + newColor );

        lastStringColor = newColor;
    
        if (newColor == "red") GetComponent<Renderer>().material.color = Color.red;
        else if (newColor == "blue") GetComponent<Renderer>().material.color = Color.blue;
        else if (newColor == "yellow") GetComponent<Renderer>().material.color = Color.yellow;
        else GetComponent<Renderer>().material.color = Color.black;
    }


    void showHostMainWindow()
    {
#if UNITY_ANDROID
        try
        {
            AndroidJavaClass jc = new AndroidJavaClass("com.company.product.OverrideUnityActivity");
            AndroidJavaObject overrideActivity = jc.GetStatic<AndroidJavaObject>("instance");
            overrideActivity.Call("showMainActivity", lastStringColor);
        } catch(Exception e)
        {
            appendToText("Exception during showHostMainWindow");
            appendToText(e.Message);
        }
#endif
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle("button");
        style.fontSize = 30;        
        if (GUI.Button(new Rect(10, 10, 200, 100), "Red", style)) ChangeColor("red");
        if (GUI.Button(new Rect(10, 110, 200, 100), "Blue", style)) ChangeColor("blue");
        if (GUI.Button(new Rect(10, 300, 400, 100), "Show Main With Color", style)) {
            appendToText( "showHostMainWindow result is: "  + nativeAPI.showHostMainWindow(lastStringColor) );
        }

        if (GUI.Button(new Rect(10, 400, 400, 100), "Unload", style)) Application.Unload();
        if (GUI.Button(new Rect(440, 400, 400, 100), "Quit", style)) Application.Quit();
    }
}


