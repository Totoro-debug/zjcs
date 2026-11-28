    [Serializable] sealed class Bindings { public Alias[] bindings; }
    [Serializable] sealed class Alias { public string file,path; }
    static void CheckHud(OfficialBattleHudCatalog catalog)
    {
        var cameraObject=new GameObject("HUD image validation camera");
        var camera=cameraObject.AddComponent<Camera>();
        camera.transform.position=new Vector3(0,0,-30);
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.06f,.09f,.12f);
        camera.cullingMask=1<<30;
        var canvasObject=new GameObject("Actual battle HUD image validation",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
        var canvas=canvasObject.GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=10;
        var scaler=canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(720,1280);
        scaler.matchWidthOrHeight=.5f;
        var renderer=new OfficialBattleHudRenderer(catalog);
        int callbacks=0;
        var root=renderer.Create("main",canvasObject.transform,(path,node)=>
        {
            var button=node.GetComponent<UnityEngine.UI.Button>();
            if(button!=null) button.onClick.AddListener(()=>callbacks++);
        });
        foreach(var transform in canvasObject.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer=30;
        foreach(var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            if(image.sprite!=null) Require(image.sprite.texture!=null,"actual rendered HUD texture "+image.name);
        int expected=0;
        foreach(var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            if(!button.isActiveAndEnabled || !button.IsInteractable()) continue;
            expected++;
            var data=new UnityEngine.EventSystems.PointerEventData(null) {button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
            button.OnPointerClick(data);
        }
        Require(expected>0 && callbacks==expected,"test-process button callbacks");
        var target=new RenderTexture(720,1280,24);
        var pixels=new Texture2D(720,1280,TextureFormat.RGBA32,false);
        var previous=RenderTexture.active; camera.targetTexture=target;
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
        pixels.ReadPixels(new Rect(0,0,720,1280),0,0); pixels.Apply();
        Require(pixels.GetPixels32().Select(p=>(p.r<<16)|(p.g<<8)|p.b).Distinct().Count()>100,"actual HUD frame contains rendered art and text");
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"current-hud-main.png"),pixels.EncodeToPNG());
        RenderTexture.active=previous; camera.targetTexture=null;
        Object.DestroyImmediate(canvasObject); Object.DestroyImmediate(cameraObject);
        Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
    }
