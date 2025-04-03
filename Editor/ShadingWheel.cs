using UnityEditor;
using UnityEngine;
using System;

[InitializeOnLoad]
public class ShadingWheel
{
    private static bool isActive = false;
    private static Vector2 initialMousePosition;
    private static Vector2 mousePosition;
    private static bool inDeadZone;

    static string selectedShading;
    static string currentSceneViewShading;

    public enum Direction { Left = 0, Down = 1, Right = 2, Up = 3 }
    private static int[] buttonToShadingMap = { 0, 1, 2, 3 };

    private static Texture2D deadzoneBorderTexture, wheelIndicatorTexture;
    
    //Preferences
    private static float wheelRadius = 135f;
    private static float deadZoneRadius = 10f;
    private static bool[] toggleStates = new bool[3];
    private static KeyCode activationKey = KeyCode.Z;
    
    //Shading names and icons
    public static readonly string[] ShadingModeNames = {
        "None", 
        "Shaded",
        "Wireframe",
        "Shaded Wireframe" 
    };
    private static readonly Texture[] ShadingIcons = {
        null,
        EditorGUIUtility.IconContent("d_Shaded").image,
        EditorGUIUtility.IconContent("d_wireframe").image,
        EditorGUIUtility.IconContent("d_ShadedWireframe").image
    };

    private static void LoadContent() {
        deadzoneBorderTexture = LoadTextureByName("wheel_outline");
        wheelIndicatorTexture = LoadTextureByName("wheel_indicator");
    }

    private static Texture2D LoadTextureByName(string textureName){
        Texture2D tex = Resources.Load<Texture2D>(textureName);
        if(tex != null) return tex;

        //Package search
        tex = (Texture2D)AssetDatabase.LoadAssetAtPath($"Packages/com.renzk.shadingwheel/Content/{textureName}.png", typeof(Texture2D));
        if(tex != null) return tex;

        string[] guids = AssetDatabase.FindAssets(textureName + " t:Texture2D");
        if(guids.Length > 0){
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        Debug.LogWarning($"Sprite {textureName} not found in package or assets, fallback to primitive discs");
        return null;
    }
    

    static ShadingWheel(){
        LoadContent();
        LoadPreferences();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    public static void LoadPreferences(){
        //Load settings
        wheelRadius = EditorPrefs.GetFloat("ShadingWheel_Radius", 135f);
        deadZoneRadius = EditorPrefs.GetFloat("ShadingWheel_Deadzone", 20f);
        activationKey = (KeyCode)EditorPrefs.GetInt("ShadingWheel_ActivationKey", (int)KeyCode.Z);
        
        //Load toggle states
        for(int i = 0; i < toggleStates.Length; i++){
            toggleStates[i] = EditorPrefs.GetBool($"{"ShadingWheel_Toggle_"}{i}", true);
        }
        
        //Load button order
        string orderString = EditorPrefs.GetString("ShadingWheel_Order", "0,1,2,3");
        string[] orderParts = orderString.Split(',');
        if(orderParts.Length == 4){
            for(int i = 0; i < 4; i++){
                if(int.TryParse(orderParts[i], out int index) && index >= 0 && index < 4){
                    buttonToShadingMap[i] = index;
                }
                else{
                    buttonToShadingMap = new int[] { 0, 1, 2, 3 };
                    break;
                }
            }
        }
        
        UpdateCurrentShadingSelection();
    }

    private static void UpdateCurrentShadingSelection(){
        //Set initial selected shading based on current scene view mode
        SceneView sceneView = SceneView.lastActiveSceneView;
        if(sceneView != null){
            if(sceneView.renderMode == DrawCameraMode.Wireframe){
                currentSceneViewShading = ShadingModeNames[2]; //Wireframe
            }
            else if(sceneView.renderMode == DrawCameraMode.Textured){
                currentSceneViewShading = ShadingModeNames[1]; //Shaded
            }
            else if(sceneView.renderMode == DrawCameraMode.TexturedWire){
                currentSceneViewShading = ShadingModeNames[3]; //Shaded Wireframe
            }
            else{
                currentSceneViewShading = ShadingModeNames[0]; //None
            }
        }
    }

    private static void OnSceneGUI(SceneView sceneView){
        //Here we detect the shortcut press or apply the shading mode
        // based on mouse direction to wheel ratio
        Event currentEvent = Event.current;
        if(currentEvent.control || currentEvent.command) return;
        
        if(currentEvent.type == EventType.KeyDown && currentEvent.keyCode == activationKey){
            if(!isActive){
                StartWheel(currentEvent);
            }
            isActive = true;
            sceneView.Repaint();
        }

        if(currentEvent.type == EventType.KeyUp && currentEvent.keyCode == activationKey){
            if(isActive){
                EndWheel(currentEvent, sceneView);
            }
            isActive = false;
            sceneView.Repaint();
        }

        if(isActive){
            mousePosition = currentEvent.mousePosition;
            DrawShadingWheel(sceneView);
            HandleMouseHover(mousePosition);
        }
    }

    private static void StartWheel(Event cachedEvent){
        initialMousePosition = cachedEvent.mousePosition;
        UpdateCurrentShadingSelection();
    }

    private static void EndWheel(Event cachedEvent, SceneView sceneView){
        float distanceFromCenter = Vector2.Distance(initialMousePosition, cachedEvent.mousePosition);
        if(distanceFromCenter > deadZoneRadius){
            ApplyShadingMode(sceneView);
        }
    }

    private static void DrawShadingWheel(SceneView sceneView){
        Handles.BeginGUI();

        if(toggleStates[0]){
            //Deadzone border wheel
            if (deadzoneBorderTexture != null){
                float diameter = (deadZoneRadius + 10) * 2;
                Rect textureRect = new Rect(
                    initialMousePosition.x - deadZoneRadius - 10,
                    initialMousePosition.y - deadZoneRadius - 10, diameter, diameter);
                GUI.DrawTexture(textureRect, deadzoneBorderTexture, ScaleMode.ScaleToFit);
                
                //Wheel indicator rendering
                if (!inDeadZone && wheelIndicatorTexture != null){
                    Vector2 direction = mousePosition - initialMousePosition;
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90;
                    
                    Matrix4x4 matrixBackup = GUI.matrix;
                    GUIUtility.RotateAroundPivot(angle, initialMousePosition);
                    GUI.DrawTexture(textureRect, wheelIndicatorTexture, ScaleMode.ScaleToFit);
                    GUI.matrix = matrixBackup;
                }
            }else{
                //if missing image files, draw primitive disc
                Handles.color = Color.white;
                Handles.DrawWireDisc(initialMousePosition, Vector3.forward, deadZoneRadius + 10);
            }
        }

        //Draw wheel radius border if enabled
        if(toggleStates[2]){
            Handles.color = Color.grey;
            Handles.DrawWireDisc(initialMousePosition, Vector3.forward, wheelRadius);
        }

        //Draw buttons
        float effectiveRadius = wheelRadius;
        DrawButton(GetPositionForDirection(Direction.Left, effectiveRadius), (int)Direction.Left);
        DrawButton(GetPositionForDirection(Direction.Down, effectiveRadius), (int)Direction.Down);
        DrawButton(GetPositionForDirection(Direction.Right, effectiveRadius), (int)Direction.Right);
        DrawButton(GetPositionForDirection(Direction.Up, effectiveRadius), (int)Direction.Up);

        //Draw "shading" label if enabled in prefs
        if(toggleStates[1]){
            Rect centerRect = new Rect(initialMousePosition.x - 50, initialMousePosition.y - (deadZoneRadius + 35), 100, 20);
            GUIStyle centeredStyle = new GUIStyle(EditorStyles.label){ alignment = TextAnchor.MiddleCenter };
            GUI.Label(centerRect, "Shading", centeredStyle);
        }

        Handles.EndGUI();
    }

    private static Vector2 GetPositionForDirection(Direction direction, float radius){
        switch(direction){
            case Direction.Left: return initialMousePosition + new Vector2(-radius, 0);
            case Direction.Down: return initialMousePosition + new Vector2(0, radius);
            case Direction.Right: return initialMousePosition + new Vector2(radius, 0);
            case Direction.Up: return initialMousePosition + new Vector2(0, -radius);
            default: return initialMousePosition;
        }
    }

    private static void DrawButton(Vector2 position, int directionIndex){
        int shadingModeIndex = buttonToShadingMap[directionIndex];
        string text = ShadingModeNames[shadingModeIndex];
        
        Color originalBackgroundColor = GUI.backgroundColor;
        Color originalContentColor = GUI.contentColor;

        //Determine button colors
        bool isCurrentShading = text == currentSceneViewShading;
        Color buttonColor;
        
        
        if(isCurrentShading){ //Blue Highlight (representing current shading)
            buttonColor = (selectedShading == text && !inDeadZone) ? 
                new Color(0.7f, 0.9f, 1f) : new Color(0.6f, 0.8f, 1f);
        }
        else{ //Normal colors (representing different shading)
            buttonColor = (selectedShading == text && !inDeadZone) ? 
                new Color(0.85f, 0.85f, 0.85f) : new Color(0.7f, 0.7f, 0.7f);
        }

        GUI.backgroundColor = buttonColor;
        GUI.contentColor = Color.white;

        //Calculate button size
        float textWidth = GUI.skin.button.CalcSize(new GUIContent(text)).x;
        float buttonWidth = Mathf.Max(100, textWidth + 20);
        
        //This is the shading wheel button map placement, if you want to add more shading options.
        Rect buttonRect = new Rect(0, 0, buttonWidth, 30);
        switch((Direction)directionIndex){
            case Direction.Left: buttonRect = new Rect(position.x - buttonWidth, position.y -15, buttonWidth, 30); break;
            case Direction.Right: buttonRect = new Rect(position.x, position.y -15, buttonWidth, 30); break;
            case Direction.Up: buttonRect = new Rect(position.x - buttonWidth/2, position.y - 30, buttonWidth, 30); break;
            case Direction.Down: buttonRect = new Rect(position.x - buttonWidth/2, position.y, buttonWidth, 30); break;
        }

        //Create button with icon if NOT "none"
        GUIContent buttonContent = ShadingIcons[shadingModeIndex] != null ? 
            new GUIContent(" " + text, ShadingIcons[shadingModeIndex]) : 
            new GUIContent(text);

        if(GUI.Button(buttonRect, buttonContent)){
            selectedShading = text;
        }

        GUI.backgroundColor = originalBackgroundColor;
        GUI.contentColor = originalContentColor;
    }

    private static void HandleMouseHover(Vector2 mousePos){
        float distanceFromCenter = Vector2.Distance(initialMousePosition, mousePos);
        
        //Deadzone detection
        if(distanceFromCenter < deadZoneRadius){
            selectedShading = "";
            inDeadZone = true;
            return;
        }
        
        inDeadZone = false;
        
        //Get direction
        Vector2 relativePos = mousePos - initialMousePosition;
        bool isHorizontal = Mathf.Abs(relativePos.x) > Mathf.Abs(relativePos.y);
        
        Direction selectedDirection = isHorizontal ? 
            (relativePos.x < 0 ? Direction.Left : Direction.Right) : 
            (relativePos.y > 0 ? Direction.Down : Direction.Up);

        selectedShading = ShadingModeNames[buttonToShadingMap[(int)selectedDirection]];
    }

    private static void ApplyShadingMode(SceneView sceneView){
        if(string.IsNullOrEmpty(selectedShading)) return;

        switch(selectedShading){
            case "Wireframe":
                sceneView.renderMode = DrawCameraMode.Wireframe;
                sceneView.sceneLighting = false;
                break;
            case "Shaded":
                sceneView.renderMode = DrawCameraMode.Textured;
                sceneView.sceneLighting = true;
                break;
            case "Shaded Wireframe":
                sceneView.renderMode = DrawCameraMode.TexturedWire;
                sceneView.sceneLighting = true;
                break;
        }

        sceneView.Repaint();
        UpdateCurrentShadingSelection();
    }

    [MenuItem("Tools/Renzk/Shading Wheel Preferences")]
    public static void ShowPreferences(){
        ShadingWheelPreferencesWindow window = EditorWindow.GetWindow<ShadingWheelPreferencesWindow>("Shading Wheel Preferences");
        window.minSize = new Vector2(350, 300);
        window.Show();
    }
}

public class ShadingWheelPreferencesWindow : EditorWindow
{
    private int[] buttonToShadingMap = new int[4];
    private string[] directionNames = { "Up", "Down", "Left", "Right" };
    private int[] directionIndices = { 3, 1, 0, 2 };
    private float wheelRadius = 135f;
    private float deadZoneRadius = 20f;
    private bool[] toggleStates = new bool[3];
    private string[] toggleLabels = { "Show Deadzone Wheel", "Show Shading Label", "Show Wheel Radius" };
    private KeyCode activationKey = KeyCode.Z;

    private void OnEnable(){
        //Load settings
        wheelRadius = EditorPrefs.GetFloat("ShadingWheel_Radius", 135f);
        deadZoneRadius = EditorPrefs.GetFloat("ShadingWheel_Deadzone", 20f);
        activationKey = (KeyCode)EditorPrefs.GetInt("ShadingWheel_ActivationKey", (int)KeyCode.Z);
        
        //Load toggleable states
        for(int i = 0; i < toggleStates.Length; i++){
            toggleStates[i] = EditorPrefs.GetBool($"{"ShadingWheel_Toggle_"}{i}", true);
        }
        
        //Load directions order
        string orderString = EditorPrefs.GetString("ShadingWheel_Order", "0,1,2,3");
        string[] orderParts = orderString.Split(',');
        for(int i = 0; i < 4; i++){
            buttonToShadingMap[i] = orderParts.Length > i && int.TryParse(orderParts[i], out int index) ? index : i;
        }
    }

    private void OnGUI(){
        //Preview Whell
        DrawWheelPreview();
        EditorGUILayout.Space();

        GUILayout.Label("Settings", EditorStyles.boldLabel);
        
        //Radius sliders
        DrawSlider("Wheel Radius", ref wheelRadius, 50f, 200f);
        DrawSlider("Dead Zone Radius", ref deadZoneRadius, 0f, 50f);
        
        //Activation key selection
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Activation Key", GUILayout.Width(150));
        activationKey = (KeyCode)EditorGUILayout.EnumPopup(activationKey);
        EditorGUILayout.EndHorizontal();
        
        //Toggle options
        for(int i = 0; i < toggleStates.Length; i++){
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(toggleLabels[i], GUILayout.Width(150));
            toggleStates[i] = EditorGUILayout.Toggle(toggleStates[i]);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        GUILayout.Label("Wheel Directions", EditorStyles.boldLabel);

        //Preferences directions dropdown
        for(int i = 0; i < 4; i++){
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(directionNames[i] + ":", GUILayout.Width(100));
            buttonToShadingMap[directionIndices[i]] = EditorGUILayout.Popup(buttonToShadingMap[directionIndices[i]], ShadingWheel.ShadingModeNames);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        
        //Preferences save/reset
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button("Save Preferences", GUILayout.Height(30))){
            SavePreferences();
        }
        if(GUILayout.Button("Reset", GUILayout.Height(30), GUILayout.Width(100))){
            ResetToDefaults();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSlider(string label, ref float value, float min, float max){
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(150));
        value = EditorGUILayout.Slider(value, min, max);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawWheelPreview(){
        Rect previewArea = EditorGUILayout.GetControlRect(false, 220);
        EditorGUI.DrawRect(new Rect(previewArea.x, previewArea.y, previewArea.width, previewArea.height), new Color(0.2f, 0.2f, 0.2f));
        
        Vector2 center = new Vector2(previewArea.x + previewArea.width / 2, previewArea.y + previewArea.height / 2);
        float previewDownscaleFactor = 0.4f;
        float radius = wheelRadius * previewDownscaleFactor;

        //Wheel radius preview
        if(toggleStates[2]){
            Handles.color = Color.grey;
            Handles.DrawWireDisc(center, Vector3.forward, radius);
        }

        //Wheel deadzone border preview
        if(toggleStates[0]){
            Handles.color = Color.white;
            Handles.DrawWireDisc(center, Vector3.forward, (deadZoneRadius + 10) * previewDownscaleFactor);
        }
        
        //Deadzone preview
        Handles.color = new Color(0.8f, 0.2f, 0.2f, 0.5f);
        Handles.DrawSolidDisc(center, Vector3.forward, deadZoneRadius * previewDownscaleFactor);

        //Draw buttons for preview wheel
        DrawPreviewButton(center + new Vector2(-radius, 0), ShadingWheel.ShadingModeNames[buttonToShadingMap[0]], ShadingWheel.Direction.Left); 
        DrawPreviewButton(center + new Vector2(0, radius), ShadingWheel.ShadingModeNames[buttonToShadingMap[1]], ShadingWheel.Direction.Down);
        DrawPreviewButton(center + new Vector2(radius, 0), ShadingWheel.ShadingModeNames[buttonToShadingMap[2]], ShadingWheel.Direction.Right);
        DrawPreviewButton(center + new Vector2(0, -radius), ShadingWheel.ShadingModeNames[buttonToShadingMap[3]], ShadingWheel.Direction.Up);
    }

    private void DrawPreviewButton(Vector2 position, string text, ShadingWheel.Direction dir){
        if(string.IsNullOrEmpty(text)) text = "None";
        
        float textWidth = GUI.skin.button.CalcSize(new GUIContent(text)).x;
        float buttonWidth = Mathf.Max(80, textWidth + 20);
        Rect buttonRect = new Rect(0, 0, buttonWidth, 30);
        
        switch(dir){
            case ShadingWheel.Direction.Left: buttonRect = new Rect(position.x - buttonWidth, position.y - 15, buttonWidth, 30); break;
            case ShadingWheel.Direction.Right: buttonRect = new Rect(position.x, position.y - 15, buttonWidth, 30); break;
            case ShadingWheel.Direction.Up: buttonRect = new Rect(position.x - buttonWidth/2, position.y - 30, buttonWidth, 30); break;
            case ShadingWheel.Direction.Down: buttonRect = new Rect(position.x - buttonWidth/2, position.y, buttonWidth, 30); break;
        }

        GUIStyle centeredButtonStyle = new GUIStyle(GUI.skin.box);
        centeredButtonStyle.alignment = TextAnchor.MiddleCenter;
        
        GUI.color = new Color(0.7f, 0.7f, 0.7f);
        GUI.Box(buttonRect, text, centeredButtonStyle);
        GUI.color = Color.white;
    }

    private void SavePreferences(){
        //Save general settings as editor prefs
        EditorPrefs.SetFloat("ShadingWheel_Radius", wheelRadius);
        EditorPrefs.SetFloat("ShadingWheel_Deadzone", deadZoneRadius);
        EditorPrefs.SetInt("ShadingWheel_ActivationKey", (int)activationKey);
        EditorPrefs.SetString("ShadingWheel_Order", $"{buttonToShadingMap[0]},{buttonToShadingMap[1]},{buttonToShadingMap[2]},{buttonToShadingMap[3]}");

        //Save toggleable states loop
        for(int i = 0; i < toggleStates.Length; i++){
            EditorPrefs.SetBool($"{"ShadingWheel_Toggle_"}{i}", toggleStates[i]);
        }
        
        //Reload prefs after finishing
        ShadingWheel.LoadPreferences();
    }

    private void ResetToDefaults(){
        wheelRadius = 135f;
        deadZoneRadius = 10f;
        activationKey = KeyCode.Z;
        buttonToShadingMap = new int[] { 2, 1, 3, 0 };
        Array.Fill(toggleStates, true);
        Repaint();
    }
}