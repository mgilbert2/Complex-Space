using System;
using System.IO;
using UnityEditor;

[InitializeOnLoad]
public static class BeachEditorRequests
{
    static BeachEditorRequests() {EditorApplication.update+=Update;}
    static void Update()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating)return;
        string request="../ApplyBeachShape.request";
        if(File.Exists(request))
        {
            if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
            if(!Consume(request))return;
            try {BeachShoreline.ApplyToOpenScene();}
            catch(Exception ex){File.WriteAllText("../ShapeUpdate.txt",ex.ToString());UnityEngine.Debug.LogException(ex);}
        }
        request="../CheckBeachShape.request";
        if(File.Exists(request) && !EditorApplication.isPlaying)
        {
            if(!Consume(request))return;
            BeachPlayCheck.RunInOpenEditor();
        }
        request="../PreviewBeachExperience.request";
        if(File.Exists(request) && !EditorApplication.isPlayingOrWillChangePlaymode && !SessionState.GetBool("Beach.PlayCheck",false))
        {
            if(!Consume(request))return;
            BeachSceneBuilder.Open();EditorApplication.isPlaying=true;
        }
    }

    static bool Consume(string path)
    {
        try{File.Delete(path);return true;}
        catch(IOException){return false;}
    }
}
