using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.Assertions.Must;

public class WeaponWindowTool : EditorWindow
{
    private int selectedTab = 0;
    private string[] tabNames = { "Receiver", "Muzzle", "Optic", "Stock", "Magazine" };

    private List<PartData>[] assetLibrary = new List<PartData>[5];
    private Vector2 scrollPos;
    private Dictionary<PartData, bool> calibrationStatus = new Dictionary<PartData, bool>();

    // Workbench State Machine
    private enum WorkbenchMode { Idle, Calibration, Equip }
    private WorkbenchMode currentMode = WorkbenchMode.Idle;

    private bool showCalibrationAssist = true;

    private float splitterPosPercent = 0.4f;
    private bool isResizing = false;
    private const float splitterWidth = 5f;

    private float safeRightWidth = 400f;

    // Calibration Mode Variables
    private GameObject currentTargetObject;
    private PartData currentPrefabAsset;
    private List<GameObject> currentDummies = new List<GameObject>();
    private Dictionary<GameObject, string> dummyToSocketMap = new Dictionary<GameObject, string>();

    // Equip Mode Variables
    private PartData[] equipLoadout = new PartData[5];
    private GameObject equipAssemblyRoot;

    [MenuItem("Tools/Weapon Workbench/ Cool Main Workbench", priority = 1)]
    public static void ShowWindow()
    {
        GetWindow<WeaponWindowTool>("Weapon Workbench").minSize = new Vector2(850, 550);
    }

    private void OnEnable()
    {
        for (int i = 0; i < assetLibrary.Length; i++)
            if (assetLibrary[i] == null) assetLibrary[i] = new List<PartData>();

        WeaponWindowSettings.LoadSettings();
        LoadLibrary();

        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        // Define absolute minimum pixel widths for both panels to prevent layout collapse
        float minLeftPx = 300f;
        float minRightPx = 380f;

        // Calculate target width based on current percentage
        float targetLeftWidth = position.width * splitterPosPercent;

        // Clamp the left width to ensure it respects both left and right minimum limits
        float maxLeftPx = Mathf.Max(minLeftPx, position.width - minRightPx);
        float actualLeftWidth = Mathf.Clamp(targetLeftWidth, minLeftPx, maxLeftPx);

        // Update the percentage backward in case the window was resized and hit a clamp limit
        splitterPosPercent = actualLeftWidth / position.width;

        GUILayout.BeginHorizontal();

        // 1. Left Panel (Workbench)
        DrawLeftWorkbench(actualLeftWidth);

        // 2. Splitter logic
        Rect splitterRect = new Rect(actualLeftWidth, 0, splitterWidth, position.height);
        EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

        Event e = Event.current;
        if (e.type == EventType.MouseDown && splitterRect.Contains(e.mousePosition)) isResizing = true;
        if (isResizing) {
            float newPercent = e.mousePosition.x / position.width;
            splitterPosPercent = Mathf.Clamp(newPercent, 0.2f, 0.8f);
            Repaint();
        }
        if (e.type == EventType.MouseUp) isResizing = false;

        EditorGUI.DrawRect(new Rect(actualLeftWidth, 0, 2, position.height), new Color(0.4f, 0.4f, 0.4f, 1f));

        // 3. Right Panel (Asset Library)
        float actualRightWidth = position.width - actualLeftWidth - 2f;

        // Cache the safe width only during the Layout event to prevent layout/repaint mismatch exceptions
        if (Event.current.type == EventType.Layout) {
            safeRightWidth = actualRightWidth;
        }

        Rect rightPanelRect = new Rect(actualLeftWidth + 2f, 0, actualRightWidth, position.height);
        HandleDragAndDrop(rightPanelRect);

        DrawRightLibrary(safeRightWidth);

        GUILayout.EndHorizontal();
    }

    private void DrawLeftWorkbench(float width)
    {
        // Lock the width to prevent UI content from expanding the container
        GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.MinWidth(width), GUILayout.MaxWidth(width));
        GUILayout.Label("Workbench", EditorStyles.largeLabel);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        if (currentMode == WorkbenchMode.Idle) {
            GUILayout.Space(30);
            GUILayout.Label("Double-click an asset on the right to enter operation mode:\n\n Uncalibrated -> [Calibration Mode]\n Calibrated -> [Equip Mode]", EditorStyles.centeredGreyMiniLabel);
        } else if (currentMode == WorkbenchMode.Calibration) {
            DrawCalibrationUI();
        } else if (currentMode == WorkbenchMode.Equip) {
            DrawEquipUI();
        }

        GUILayout.EndVertical();
    }

    private void DrawCalibrationUI()
    {
        GUILayout.Space(10);
        EditorGUILayout.HelpBox($"Mode: Calibrating Base Prefab\nTarget: {currentTargetObject.name}\nAlign it with the dummy in the Scene using Move(W)/Rotate(E).", MessageType.Info);

        GUILayout.Space(5);

        // Toggle visual assist and force a Scene view repaint immediately if changed
        bool newAssistState = EditorGUILayout.ToggleLeft(" Show 3D Visual Assist", showCalibrationAssist, EditorStyles.boldLabel);
        if (newAssistState != showCalibrationAssist) {
            showCalibrationAssist = newAssistState;

            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.Repaint();
            else
                SceneView.RepaintAll();
        }

        GUILayout.Space(20);
        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button(" Complete Alignment & Generate Prefab", GUILayout.Height(50))) {
            CompleteCalibration();
            GUIUtility.ExitGUI();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10);
        if (GUILayout.Button("Cancel")) {
            ClearWorkbench();
            GUIUtility.ExitGUI();
        }
    }

    private void DrawEquipUI()
    {
        GUILayout.Space(10);
        EditorGUILayout.HelpBox("Mode: Real-time Assembly. Double-click calibrated attachments on the right to snap and replace current parts.", MessageType.Info);

        GUILayout.Space(10);
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.ObjectField("Receiver (Base)", equipLoadout[0], typeof(GameObject), false);
        EditorGUILayout.ObjectField("Muzzle", equipLoadout[1], typeof(GameObject), false);
        EditorGUILayout.ObjectField("Optic", equipLoadout[2], typeof(GameObject), false);
        EditorGUILayout.ObjectField("Stock", equipLoadout[3], typeof(GameObject), false);
        EditorGUILayout.ObjectField("Magazine", equipLoadout[4], typeof(GameObject), false);
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(20);

        GUI.backgroundColor = new Color(1f, 0.7f, 0.2f);
        if (GUILayout.Button(" Randomize Full Weapon", GUILayout.Height(35))) {
            RandomizeEquipAssembly();
        }

        GUILayout.Space(10);
        GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
        if (GUILayout.Button(" Save Full Assembly", GUILayout.Height(50))) {
            SaveEquipAssembly();
            GUIUtility.ExitGUI();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10);
        if (GUILayout.Button("Clear Workbench")) {
            ClearWorkbench();
            GUIUtility.ExitGUI();
        }
    }

    private void RandomizeEquipAssembly()
    {
        bool hasValidBody = false;

        // Iterate through all 5 categories
        for (int i = 0; i < 5; i++) {
            List<PartData> validAssets = new List<PartData>();
            foreach (PartData obj in assetLibrary[i]) {
                if (obj != null && calibrationStatus.ContainsKey(obj) && calibrationStatus[obj] == true) {
                    validAssets.Add(obj);
                }
            }

            // Pick a random valid asset if available
            if (validAssets.Count > 0) {
                int randomIndex = Random.Range(0, validAssets.Count);
                equipLoadout[i] = validAssets[randomIndex];

                if (i == 0) hasValidBody = true;
            } else {
                equipLoadout[i] = null;
            }
        }

        if (!hasValidBody) {
            EditorUtility.DisplayDialog("Notice", "Missing a calibrated Receiver in your library. Cannot randomize!", "OK");
        }

        RefreshEquipAssembly();
    }

    private void DrawRightLibrary(float width)
    {
        GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.MinWidth(width), GUILayout.MaxWidth(width));
        GUILayout.Label("Asset Library", EditorStyles.largeLabel);

        // Custom wrapping toolbar logic
        GUILayout.BeginHorizontal();
        float currentWidth = 0;
        float maxWidth = width - 15f;

        for (int i = 0; i < tabNames.Length; i++) {
            GUIContent content = new GUIContent(tabNames[i]);
            float btnWidth = EditorStyles.toolbarButton.CalcSize(content).x;

            if (currentWidth + btnWidth > maxWidth) {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                currentWidth = 0;
            }

            if (GUILayout.Toggle(selectedTab == i, tabNames[i], EditorStyles.toolbarButton, GUILayout.Width(btnWidth))) {
                selectedTab = i;
            }
            currentWidth += btnWidth + 4f;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        DrawAssetList(width);
        GUILayout.EndVertical();
    }

    private void HandleDragAndDrop(Rect dropArea)
    {
        Event evt = Event.current;
        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) {
            if (!dropArea.Contains(evt.mousePosition)) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform) {
                DragAndDrop.AcceptDrag();
                bool hasChanged = false;
                foreach (Object draggedObj in DragAndDrop.objectReferences) {
                    if (draggedObj is PartData go) {
                        if (!assetLibrary[selectedTab].Contains(go)) {
                            assetLibrary[selectedTab].Add(go);
                            calibrationStatus[go] = CheckIfCalibrated(go.partObject, selectedTab);
                            hasChanged = true;
                        }
                    }
                }
                if (hasChanged) SaveLibrary();
            }
            evt.Use();
        }
    }

    private bool CheckIfCalibrated(GameObject go, int tabIndex)
    {
        // Receiver expects exactly 4 specific sockets
        if (tabIndex == 0) {
            return go.transform.Find("Socket_Muzzle") != null &&
                   go.transform.Find("Socket_Optic") != null &&
                   go.transform.Find("Socket_Stock") != null &&
                   go.transform.Find("Socket_Magazine") != null;
        }
        // Attachments expect to be wrapped in an empty parent object
        else {
            return go.GetComponent<MeshRenderer>() == null && go.transform.childCount > 0;
        }
    }

    private void DrawAssetList(float width)
    {
        List<PartData> currentList = assetLibrary[selectedTab];

        // Sanitize the list by removing any externally deleted assets before UI rendering
        bool removedAny = false;
        for (int i = currentList.Count - 1; i >= 0; i--) {
            if (currentList[i] == null) {
                currentList.RemoveAt(i);
                removedAny = true;
            }
        }
        if (removedAny) SaveLibrary();

        if (currentList.Count == 0) return;

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        float iconSize = 75f;
        float itemWidth = 85f;
        float itemHeight = 100f;
        int columns = Mathf.Max(1, Mathf.FloorToInt((width - 30f) / itemWidth));

        GUILayout.BeginHorizontal();
        for (int i = 0; i < currentList.Count; i++) {
            PartData obj = currentList[i];

            // Backup null check to prevent ghost references from crashing the GUI loop
            if (obj == null) {
                currentList.RemoveAt(i);
                i--;
                SaveLibrary();
                continue;
            }

            if (i > 0 && i % columns == 0) {
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
            }

            bool isCalibrated = calibrationStatus.ContainsKey(obj) && calibrationStatus[obj];

            GUILayout.BeginVertical(GUILayout.Width(itemWidth), GUILayout.Height(itemHeight));

            Texture2D preview = AssetPreview.GetAssetPreview(obj);
            if (preview == null) preview = AssetPreview.GetMiniThumbnail(obj);

            Rect buttonRect = GUILayoutUtility.GetRect(iconSize, iconSize);
            Event e = Event.current;

            // Handle Context Menu (Right Click)
            if (e.type == EventType.MouseDown && e.button == 1 && buttonRect.Contains(e.mousePosition)) {
                PartData menuObj = obj;
                int menuTab = selectedTab;
                GenericMenu menu = new GenericMenu();

                if (isCalibrated) {
                    menu.AddItem(new GUIContent(" Equip Part"), false, () => EnterEquipMode(menuObj, menuTab));
                }

                menu.AddItem(new GUIContent(" (Re)Calibrate"), false, () => EnterCalibrationMode(menuObj, menuTab));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent(" Remove"), false, () => {
                    assetLibrary[menuTab].Remove(menuObj);
                    calibrationStatus.Remove(menuObj);
                    SaveLibrary();
                });
                menu.ShowAsContext();
                e.Use();
            }
            // Handle Auto-assign Mode (Double Left Click)
            else if (e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 2 && buttonRect.Contains(e.mousePosition)) {
                if (isCalibrated) EnterEquipMode(obj, selectedTab);
                else EnterCalibrationMode(obj, selectedTab);
                e.Use();
            }

            // Invisible button to capture hovering/rendering the preview texture
            if (GUI.Button(buttonRect, preview)) { }

            Rect statusRect = new Rect(buttonRect.xMax - 25, buttonRect.yMax - 25, 25, 25);
            GUIStyle statusStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 };
            if (isCalibrated) GUI.Label(statusRect, "", statusStyle);
            else { GUI.color = Color.yellow; GUI.Label(statusRect, "", statusStyle); GUI.color = Color.white; }

            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter, clipping = TextClipping.Clip };
            GUILayout.Label(obj.name, labelStyle, GUILayout.Width(iconSize));

            GUILayout.EndVertical();
        }
        GUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }

    private void ClearWorkbench()
    {
        // Cleanup Calibration Mode entities
        if (currentTargetObject != null) DestroyImmediate(currentTargetObject);
        foreach (var dummy in currentDummies) if (dummy != null) DestroyImmediate(dummy);
        currentDummies.Clear();
        dummyToSocketMap.Clear();
        currentTargetObject = null;
        currentPrefabAsset = null;

        // Cleanup Equip Mode entities
        if (equipAssemblyRoot != null) DestroyImmediate(equipAssemblyRoot);
        for (int i = 0; i < equipLoadout.Length; i++) equipLoadout[i] = null;

        currentMode = WorkbenchMode.Idle;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (currentMode != WorkbenchMode.Calibration || currentTargetObject == null) return;
        if (!showCalibrationAssist) return;

        if (selectedTab == 0) {
            // For Receiver calibration: Draw all dummy socket locations
            foreach (var dummy in currentDummies) {
                if (dummy != null) {
                    DrawSocketVisual(dummy.transform.position, dummy.transform.rotation, dummy.name.Replace("[Dummy] ", ""));
                }
            }
        } else {
            // For Attachment calibration: Draw only the relevant target socket on the dummy Receiver
            if (currentDummies.Count > 0 && currentDummies[0] != null) {
                string targetSocketName = GetSocketNameForEquip(selectedTab);
                Transform targetSocket = currentDummies[0].transform.Find(targetSocketName);

                if (targetSocket != null) {
                    DrawSocketVisual(targetSocket.position, targetSocket.rotation, targetSocketName);
                }
            }
        }
    }

    private void DrawSocketVisual(Vector3 position, Quaternion rotation, string labelName)
    {
        // Translucent green core sphere indicating the absolute center point
        Handles.color = new Color(0.2f, 1f, 0.2f, 0.3f);
        Handles.SphereHandleCap(0, position, rotation, 0.04f, EventType.Repaint);

        // Gyroscope-like orthogonal wireframe discs
        Handles.color = new Color(0.2f, 1f, 0.2f, 0.8f);
        Handles.DrawWireDisc(position, rotation * Vector3.up, 0.04f);
        Handles.DrawWireDisc(position, rotation * Vector3.right, 0.04f);
        Handles.DrawWireDisc(position, rotation * Vector3.forward, 0.04f);

        // Thick blue arrow indicating the Z-forward forward/insertion direction
        Handles.color = new Color(0.2f, 0.6f, 1f, 1f);
        Handles.ArrowHandleCap(0, position, rotation, 0.15f, EventType.Repaint);

        // Floating label 
        GUIStyle labelStyle = new GUIStyle();
        labelStyle.normal.textColor = Color.green;
        labelStyle.fontSize = 12;
        labelStyle.fontStyle = FontStyle.Bold;

        Handles.Label(position + Vector3.up * 0.06f, labelName, labelStyle);
    }

    private void EnterCalibrationMode(PartData targetPrefab, int tabIndex)
    {
        if (WeaponWindowSettings.dummyBody == null || WeaponWindowSettings.dummyMuzzle == null) {
            if (EditorUtility.DisplayDialog("Missing Dummy", "Please configure base dummies first!\nGo to settings now?", "Settings", "Cancel")) WeaponWindowSettings.ShowWindow();
            return;
        }

        ClearWorkbench();
        currentMode = WorkbenchMode.Calibration;
        currentPrefabAsset = targetPrefab;
        currentTargetObject = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefab);

        // Completely unpack the prefab instance to prevent nested cyclical exceptions when overriding
        PrefabUtility.UnpackPrefabInstance(currentTargetObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        if (tabIndex == 0) // Receiver Calibration
        {
            currentTargetObject.transform.position = Vector3.zero;

            GameObject mDummy = SpawnDummy(WeaponWindowSettings.dummyMuzzle, "Socket_Muzzle");
            GameObject oDummy = SpawnDummy(WeaponWindowSettings.dummyScope, "Socket_Optic");
            GameObject sDummy = SpawnDummy(WeaponWindowSettings.dummyStock, "Socket_Stock");
            GameObject magDummy = SpawnDummy(WeaponWindowSettings.dummyMag, "Socket_Magazine");

            // Snap the dummies to existing sockets to allow for fine-tuning of already calibrated receivers
            Transform mS = currentTargetObject.transform.Find("Socket_Muzzle");
            if (mS != null && mDummy != null) { mDummy.transform.position = mS.position; mDummy.transform.rotation = mS.rotation; }

            Transform oS = currentTargetObject.transform.Find("Socket_Optic");
            if (oS != null && oDummy != null) { oDummy.transform.position = oS.position; oDummy.transform.rotation = oS.rotation; }

            Transform sS = currentTargetObject.transform.Find("Socket_Stock");
            if (sS != null && sDummy != null) { sDummy.transform.position = sS.position; sDummy.transform.rotation = sS.rotation; }

            Transform magS = currentTargetObject.transform.Find("Socket_Magazine");
            if (magS != null && magDummy != null) { magDummy.transform.position = magS.position; magDummy.transform.rotation = magS.rotation; }
        } else // Attachment Calibration
          {
            SpawnDummy(WeaponWindowSettings.dummyBody, "Body");

            string socketName = GetSocketNameForEquip(tabIndex);
            Transform targetSocket = currentDummies[0].transform.Find(socketName);

            if (targetSocket != null) {
                // Preserve local offset if the asset has been previously calibrated
                currentTargetObject.transform.position = targetSocket.position;
                currentTargetObject.transform.rotation = targetSocket.rotation;
            } else {
                currentTargetObject.transform.position = Vector3.zero;
            }
        }

        Selection.activeGameObject = currentTargetObject;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
    }

    private GameObject SpawnDummy(GameObject dummyPrefab, string socketName)
    {
        if (dummyPrefab == null) return null;
        GameObject dummy = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab);
        dummy.transform.position = Vector3.zero;
        dummy.name = "[Dummy] " + dummyPrefab.name;
        currentDummies.Add(dummy);
        dummyToSocketMap[dummy] = socketName;
        return dummy;
    }

    private void CompleteCalibration()
    {
        bool success = (selectedTab == 0) ? CalibrateGunBody() : CalibrateAttachment(selectedTab);

        if (success) {
            string originalPath = AssetDatabase.GetAssetPath(currentPrefabAsset);
            string savePath = originalPath;
            bool isRawModel = originalPath.ToLower().EndsWith(".fbx") || originalPath.ToLower().EndsWith(".obj");

            if (isRawModel) {
                savePath = System.IO.Path.ChangeExtension(originalPath, ".prefab");
                savePath = AssetDatabase.GenerateUniqueAssetPath(savePath);
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(currentTargetObject, savePath, InteractionMode.UserAction);
            if (savedPrefab != null) {
                int index = assetLibrary[selectedTab].IndexOf(currentPrefabAsset);
                if (index >= 0) {
                    PartData partData = assetLibrary[selectedTab][index];
                    partData.partObject = savedPrefab;
                    calibrationStatus[partData] = true;
                    EditorUtility.SetDirty(partData);
                    AssetDatabase.SaveAssets();
                }
                SaveLibrary();
                Debug.Log($"[Calibration Success] Asset processed and saved to: {savePath}");
            }
            ClearWorkbench();
        }
    }

    private bool CalibrateGunBody()
    {
        // Align generated socket transforms with the manipulated dummy transforms
        foreach (var kvp in dummyToSocketMap) {
            Transform socket = currentTargetObject.transform.Find(kvp.Value);
            if (socket == null) {
                GameObject sObj = new GameObject(kvp.Value);
                socket = sObj.transform;
                socket.SetParent(currentTargetObject.transform);
            }
            socket.position = kvp.Key.transform.position;
            socket.rotation = kvp.Key.transform.rotation;
        }
        return true;
    }

    private bool CalibrateAttachment(int tabIndex)
    {
        string targetSocketName = "";
        switch (tabIndex) {
            case 1: targetSocketName = "Socket_Muzzle"; break;
            case 2: targetSocketName = "Socket_Optic"; break;
            case 3: targetSocketName = "Socket_Stock"; break;
            case 4: targetSocketName = "Socket_Magazine"; break;
        }

        Transform targetSocket = currentDummies[0].transform.Find(targetSocketName);
        if (targetSocket == null) return false;

        if (currentTargetObject.GetComponent<MeshRenderer>() != null) {
            // Wrap raw meshes into an empty parent object to bake the pivot
            GameObject newRoot = new GameObject(currentTargetObject.name + "_Prefab");
            newRoot.transform.position = targetSocket.position;
            newRoot.transform.rotation = targetSocket.rotation;
            currentTargetObject.transform.SetParent(newRoot.transform, true);
            currentTargetObject = newRoot;
        } else {
            // Temporarily unparent children, move the parent root, and reparent children to bake the pivot
            List<Transform> children = new List<Transform>();
            foreach (Transform child in currentTargetObject.transform) children.Add(child);
            foreach (Transform child in children) child.SetParent(null, true);
            currentTargetObject.transform.position = targetSocket.position;
            currentTargetObject.transform.rotation = targetSocket.rotation;
            foreach (Transform child in children) child.SetParent(currentTargetObject.transform, true);
        }
        return true;
    }

    private void EnterEquipMode(PartData obj, int tabIndex)
    {
        if (currentMode == WorkbenchMode.Calibration) ClearWorkbench();

        currentMode = WorkbenchMode.Equip;
        equipLoadout[tabIndex] = obj;
        RefreshEquipAssembly();
    }

    private void RefreshEquipAssembly()
    {
        if (equipAssemblyRoot != null) DestroyImmediate(equipAssemblyRoot);

        equipAssemblyRoot = new GameObject("[Assembling] New Weapon");
        equipAssemblyRoot.transform.position = Vector3.zero;

        GameObject bodyInstance = null;

        // 1. Instantiate the Receiver to act as the base
        if (equipLoadout[0] != null) {
            bodyInstance = (GameObject)PrefabUtility.InstantiatePrefab(equipLoadout[0].partObject, equipAssemblyRoot.transform);
            bodyInstance.transform.localPosition = Vector3.zero;
            bodyInstance.transform.localRotation = Quaternion.identity;
        }

        


        // 2. Attach selected parts 
        for (int i = 1; i <= 4; i++) {
            if (equipLoadout[i] != null) {

                bool result = CompatibilityTest.Compatible(equipLoadout[0], equipLoadout[i]);

                if (!result) {
                    Debug.Log("Part cannot fit the weapon or the receiver is not selected");
                    equipLoadout[i] = null;
                    continue;
                }

                GameObject accInstance = (GameObject)PrefabUtility.InstantiatePrefab(equipLoadout[i].partObject);

                if (bodyInstance != null) {
                    string socketName = GetSocketNameForEquip(i);
                    Transform socket = bodyInstance.transform.Find(socketName);

                    if (socket != null) {
                        // passing 'false' maintains world positions, locking it instantly into the baked socket pivot
                        accInstance.transform.SetParent(socket, false);
                        accInstance.transform.localPosition = Vector3.zero;
                        accInstance.transform.localRotation = Quaternion.identity;
                    } else {
                        accInstance.transform.SetParent(equipAssemblyRoot.transform, false);
                        Debug.LogWarning($"Assembly failed: Receiver is missing '{socketName}' socket!");
                    }
                } else {
                    accInstance.transform.SetParent(equipAssemblyRoot.transform, false);
                }
            }
        }

        Selection.activeGameObject = equipAssemblyRoot;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
    }

    private string GetSocketNameForEquip(int tabIndex)
    {
        switch (tabIndex) {
            case 1: return "Socket_Muzzle";
            case 2: return "Socket_Optic";
            case 3: return "Socket_Stock";
            case 4: return "Socket_Magazine";
            default: return "";
        }
    }

    private void SaveEquipAssembly()
    {
        if (equipAssemblyRoot == null || equipLoadout[0] == null) {
            EditorUtility.DisplayDialog("Cannot Save", "A Receiver must be included to save the complete weapon!", "OK");
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject("Save Full Weapon", "NewWeaponLoadout", "prefab", "Select save path");
        if (string.IsNullOrEmpty(path)) return;

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(equipAssemblyRoot, path, InteractionMode.UserAction);
        if (savedPrefab != null) {
            Debug.Log($"[Success] New weapon assembled and saved to: {path}");
            EditorGUIUtility.PingObject(savedPrefab);
        }
    }

    private void SaveLibrary()
    {
        for (int i = 0; i < assetLibrary.Length; i++) {
            List<string> guids = new List<string>();
            foreach (PartData go in assetLibrary[i]) {
                if (go != null) {
                    string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(go));
                    if (!string.IsNullOrEmpty(guid)) guids.Add(guid);
                }
            }
            EditorPrefs.SetString("WT_AssetLib_Tab_" + i, string.Join(",", guids));
        }

        List<string> calibratedGuids = new List<string>();
        foreach (var kvp in calibrationStatus) {
            if (kvp.Key != null && kvp.Value == true) {
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(kvp.Key));
                if (!string.IsNullOrEmpty(guid)) calibratedGuids.Add(guid);
            }
        }
        EditorPrefs.SetString("WT_AssetLib_Calibrated", string.Join(",", calibratedGuids));
    }

    private void LoadLibrary()
    {
        calibrationStatus.Clear();

        string calibratedString = EditorPrefs.GetString("WT_AssetLib_Calibrated", "");
        HashSet<string> calibratedSet = new HashSet<string>(calibratedString.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));

        for (int i = 0; i < assetLibrary.Length; i++) {
            assetLibrary[i] = new List<PartData>();
            string guidsString = EditorPrefs.GetString("WT_AssetLib_Tab_" + i, "");

            if (!string.IsNullOrEmpty(guidsString)) {
                string[] guids = guidsString.Split(',');
                foreach (string guid in guids) {
                    if (string.IsNullOrEmpty(guid)) continue;
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path)) {
                        PartData go = AssetDatabase.LoadAssetAtPath<PartData>(path);
                        if (go != null) {
                            assetLibrary[i].Add(go);
                            calibrationStatus[go] = calibratedSet.Contains(guid) || CheckIfCalibrated(go.partObject, i);
                        }
                    }
                }
            }
        }
    }
}