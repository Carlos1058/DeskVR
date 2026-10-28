using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using DeskVR.Peripherals;
using DeskVR.OS;

namespace DeskVR.Editor
{
    public static class DeskVRWorkstationBuilder
    {
        private const string MainScenePath = "Assets/_Project/Scenes/DeskVR_Main.unity";
        private const string WorkstationRootName = "DeskVR_Workstation";

        [MenuItem("DeskVR/⭐ 1. Cargar y Configurar Escena Limpia de DeskVR", priority = 1)]
        public static void SetupAndOpenCleanScene()
        {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            EditorSceneManager.OpenScene(MainScenePath);
            BuildWorkstation();
            SetSceneAsDefaultBuildScene(MainScenePath);

            EditorUtility.DisplayDialog(
                "DeskVR - Escena Lista",
                "¡Estación completa y Estudio Luminoso configurados en DeskVR_Main!\n\n• Habitación de estudio moderna bien iluminada (4 focos de techo + lámpara)\n• Piso de madera cálida, ventana con luz de día y repisa\n• Teclado 3D interactivo con teclas QWERTY\n• Mouse 3D físico con físicas y hápticos\n• Ventana arrastrable de Bloc de Notas en pantalla\n• Sin silla que estorbe al usuario\n\n¡Dale a Play ▶️ para probar con tus Quest 2!",
                "¡Excelente!"
            );
        }

        [MenuItem("DeskVR/2. Reconstruir Estación y Estudio en Escena Actual", priority = 2)]
        public static void BuildWorkstationMenu()
        {
            BuildWorkstation();
            EditorUtility.DisplayDialog(
                "DeskVR",
                "¡Estudio Luminoso y Estación de trabajo reconstruidos con éxito!\n\nDale a Play ▶️ para probar.",
                "¡Genial!"
            );
        }

        public static void BuildWorkstationBatch()
        {
            EditorSceneManager.OpenScene(MainScenePath);
            BuildWorkstation();
            SetSceneAsDefaultBuildScene(MainScenePath);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[DeskVR] Batch build completed successfully!");
        }

        public static void BuildWorkstation()
        {
            var activeScene = EditorSceneManager.GetActiveScene();

            var existing = GameObject.Find(WorkstationRootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            // Remove any old plane floor so our room floor shines
            var oldPlanes = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in oldPlanes)
            {
                if (go.name == "Plane" && go.transform.parent == null)
                {
                    Undo.DestroyObjectImmediate(go);
                }
            }

            // 1. Root Workstation Object
            GameObject root = new GameObject(WorkstationRootName);
            Undo.RegisterCreatedObjectUndo(root, "Build DeskVR Workstation");
            root.transform.position = Vector3.zero;

            // Materials
            Material deskMat = CreateColorMaterial("DeskMat", new Color(0.32f, 0.22f, 0.15f), 0.4f);
            Material legMat = CreateColorMaterial("LegMat", new Color(0.12f, 0.12f, 0.14f), 0.2f);
            Material padMat = CreateColorMaterial("PadMat", new Color(0.08f, 0.12f, 0.18f), 0.1f);
            Material mouseMat = CreateColorMaterial("MouseMat", new Color(0.18f, 0.18f, 0.20f), 0.3f);
            Material screenBezelMat = CreateColorMaterial("BezelMat", new Color(0.05f, 0.05f, 0.05f), 0.2f);
            Material keyMat = CreateColorMaterial("KeyMat", new Color(0.92f, 0.92f, 0.94f), 0.2f);
            Material specialKeyMat = CreateColorMaterial("SpecialKeyMat", new Color(0.75f, 0.78f, 0.82f), 0.2f);

            // Bright, Clean Room Materials
            Material floorWoodMat = CreateColorMaterial("FloorWoodMat", new Color(0.58f, 0.42f, 0.28f), 0.45f); // Honey warm oak
            Material rugMat = CreateColorMaterial("RugMat", new Color(0.78f, 0.80f, 0.82f), 0.05f);             // Light cozy rug
            Material wallMat = CreateColorMaterial("WallPaintMat", new Color(0.96f, 0.95f, 0.92f), 0.05f);         // Bright warm off-white
            Material baseboardMat = CreateColorMaterial("BaseboardMat", new Color(0.32f, 0.22f, 0.15f), 0.3f);
            Material ceilingMat = CreateColorMaterial("CeilingMat", new Color(0.98f, 0.98f, 0.98f), 0.05f);
            Material windowFrameMat = CreateColorMaterial("WindowFrameMat", new Color(0.15f, 0.16f, 0.18f), 0.3f);
            Material plantPotMat = CreateColorMaterial("PlantPotMat", new Color(0.95f, 0.95f, 0.96f), 0.5f);
            Material plantLeafMat = CreateColorMaterial("PlantLeafMat", new Color(0.24f, 0.60f, 0.26f), 0.3f);
            Material lampMat = CreateColorMaterial("LampMetalMat", new Color(0.90f, 0.76f, 0.40f), 0.6f);

            // Build Cozy Study Room Environment (Without chair, with bright recessed downlights)
            BuildCozyRoomEnvironment(root.transform, floorWoodMat, rugMat, wallMat, baseboardMat, ceilingMat, windowFrameMat, plantPotMat, plantLeafMat, lampMat);

            // 2. Desk TableTop
            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Desk_TableTop";
            table.transform.SetParent(root.transform);
            table.transform.position = new Vector3(0f, 0.72f, 0.65f);
            table.transform.localScale = new Vector3(1.30f, 0.04f, 0.75f);
            table.GetComponent<MeshRenderer>().sharedMaterial = deskMat;

            // 3. Desk Legs (4 corners)
            Vector3[] legOffsets = new Vector3[]
            {
                new Vector3(-0.60f, 0.35f, 0.32f),
                new Vector3( 0.60f, 0.35f, 0.32f),
                new Vector3(-0.60f, 0.35f, 0.98f),
                new Vector3( 0.60f, 0.35f, 0.98f)
            };
            for (int i = 0; i < legOffsets.Length; i++)
            {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = $"Desk_Leg_{i + 1}";
                leg.transform.SetParent(root.transform);
                leg.transform.position = legOffsets[i];
                leg.transform.localScale = new Vector3(0.05f, 0.70f, 0.05f);
                leg.GetComponent<MeshRenderer>().sharedMaterial = legMat;
            }

            // 4. Mousepad (Desk Surface Reference)
            GameObject mousePad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mousePad.name = "Desk_MousePad";
            mousePad.transform.SetParent(root.transform);
            mousePad.transform.position = new Vector3(0.30f, 0.741f, 0.58f);
            mousePad.transform.localScale = new Vector3(0.38f, 0.002f, 0.28f);
            mousePad.GetComponent<MeshRenderer>().sharedMaterial = padMat;

            // 5. Monitor 3D Frame & Stand
            GameObject monitorStand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            monitorStand.name = "Monitor_StandBase";
            monitorStand.transform.SetParent(root.transform);
            monitorStand.transform.position = new Vector3(0f, 0.745f, 0.95f);
            monitorStand.transform.localScale = new Vector3(0.24f, 0.01f, 0.18f);
            monitorStand.GetComponent<MeshRenderer>().sharedMaterial = legMat;

            GameObject monitorPole = GameObject.CreatePrimitive(PrimitiveType.Cube);
            monitorPole.name = "Monitor_Pole";
            monitorPole.transform.SetParent(root.transform);
            monitorPole.transform.position = new Vector3(0f, 0.95f, 0.98f);
            monitorPole.transform.localScale = new Vector3(0.06f, 0.40f, 0.04f);
            monitorPole.GetComponent<MeshRenderer>().sharedMaterial = legMat;

            GameObject monitorBezel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            monitorBezel.name = "Monitor_Bezel";
            monitorBezel.transform.SetParent(root.transform);
            monitorBezel.transform.position = new Vector3(0f, 1.15f, 0.95f);
            monitorBezel.transform.localScale = new Vector3(0.92f, 0.54f, 0.025f);
            monitorBezel.GetComponent<MeshRenderer>().sharedMaterial = screenBezelMat;

            // 6. Monitor Screen (World Space UI Canvas)
            GameObject canvasGO = new GameObject("Monitor_Screen_Canvas");
            canvasGO.transform.SetParent(root.transform);
            canvasGO.transform.position = new Vector3(0f, 1.15f, 0.935f);
            canvasGO.transform.rotation = Quaternion.identity;
            canvasGO.transform.localScale = new Vector3(0.00045f, 0.00045f, 0.00045f);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);

            canvasGO.AddComponent<CanvasScaler>();
            GraphicRaycaster raycaster = canvasGO.AddComponent<GraphicRaycaster>();

            // 6a. Wallpaper
            GameObject wallpaper = new GameObject("Wallpaper", typeof(RectTransform), typeof(Image));
            wallpaper.transform.SetParent(canvasGO.transform, false);
            RectTransform wallRect = wallpaper.GetComponent<RectTransform>();
            wallRect.anchorMin = Vector2.zero;
            wallRect.anchorMax = Vector2.one;
            wallRect.sizeDelta = Vector2.zero;
            wallpaper.GetComponent<Image>().color = new Color(0.08f, 0.16f, 0.28f);

            // 6b. Taskbar
            GameObject taskbar = new GameObject("Taskbar", typeof(RectTransform), typeof(Image));
            taskbar.transform.SetParent(canvasGO.transform, false);
            RectTransform barRect = taskbar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.sizeDelta = new Vector2(0f, 65f);
            taskbar.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.11f, 0.95f);

            // Start Button
            GameObject startBtn = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            startBtn.transform.SetParent(taskbar.transform, false);
            RectTransform startRect = startBtn.GetComponent<RectTransform>();
            startRect.anchorMin = new Vector2(0f, 0.5f);
            startRect.anchorMax = new Vector2(0f, 0.5f);
            startRect.pivot = new Vector2(0f, 0.5f);
            startRect.anchoredPosition = new Vector2(15f, 0f);
            startRect.sizeDelta = new Vector2(150f, 48f);
            startBtn.GetComponent<Image>().color = new Color(0.12f, 0.45f, 0.85f);

            GameObject startText = new GameObject("Label", typeof(RectTransform), typeof(Text));
            startText.transform.SetParent(startBtn.transform, false);
            RectTransform startTextRect = startText.GetComponent<RectTransform>();
            startTextRect.anchorMin = Vector2.zero;
            startTextRect.anchorMax = Vector2.one;
            startTextRect.sizeDelta = Vector2.zero;
            Text txtStart = startText.GetComponent<Text>();
            txtStart.text = "🖥️ DeskVR";
            txtStart.alignment = TextAnchor.MiddleCenter;
            txtStart.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txtStart.fontSize = 24;
            txtStart.color = Color.white;

            // 6c. Desktop App Icon (Bloc de Notas)
            GameObject notepadIcon = new GameObject("Icon_BlocNotas", typeof(RectTransform), typeof(Image), typeof(Button));
            notepadIcon.transform.SetParent(canvasGO.transform, false);
            RectTransform iconRect = notepadIcon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(50f, -50f);
            iconRect.sizeDelta = new Vector2(110f, 110f);
            Image iconImg = notepadIcon.GetComponent<Image>();
            iconImg.color = new Color(0.2f, 0.5f, 0.9f, 0.8f);

            Button iconBtn = notepadIcon.GetComponent<Button>();
            ColorBlock cb = iconBtn.colors;
            cb.highlightedColor = new Color(0.4f, 0.7f, 1.0f);
            cb.pressedColor = new Color(0.1f, 0.3f, 0.7f);
            iconBtn.colors = cb;

            GameObject iconText = new GameObject("IconLabel", typeof(RectTransform), typeof(Text));
            iconText.transform.SetParent(notepadIcon.transform, false);
            RectTransform iconTextRect = iconText.GetComponent<RectTransform>();
            iconTextRect.anchorMin = Vector2.zero;
            iconTextRect.anchorMax = Vector2.one;
            iconTextRect.sizeDelta = Vector2.zero;
            Text txtIcon = iconText.GetComponent<Text>();
            txtIcon.text = "📝\nBloc de\nNotas";
            txtIcon.alignment = TextAnchor.MiddleCenter;
            txtIcon.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txtIcon.fontSize = 20;
            txtIcon.color = Color.white;

            // 6d. Notepad Window
            BuildNotepadWindow(canvasGO.transform, iconBtn);

            // Find and tag VR Camera
            Camera vrCam = FindAndTagVRCamera();
            if (vrCam != null)
            {
                canvas.worldCamera = vrCam;
            }

            // 6e. Screen Cursor
            GameObject cursorGO = new GameObject("ScreenCursor", typeof(RectTransform), typeof(Image));
            cursorGO.transform.SetParent(canvasGO.transform, false);
            RectTransform cursorRect = cursorGO.GetComponent<RectTransform>();
            cursorRect.pivot = new Vector2(0.08f, 0.92f);
            cursorRect.sizeDelta = new Vector2(36f, 48f);
            cursorRect.anchoredPosition = Vector2.zero;
            Image curImg = cursorGO.GetComponent<Image>();
            curImg.color = Color.white;
            curImg.raycastTarget = false;

            VirtualScreenCursor screenCursor = cursorGO.AddComponent<VirtualScreenCursor>();
            SerializedObject cursorSO = new SerializedObject(screenCursor);
            cursorSO.FindProperty("screenRect").objectReferenceValue = canvasRect;
            cursorSO.FindProperty("cursorRect").objectReferenceValue = cursorRect;
            cursorSO.FindProperty("raycaster").objectReferenceValue = raycaster;
            if (vrCam != null) cursorSO.FindProperty("eventCamera").objectReferenceValue = vrCam;
            cursorSO.FindProperty("cursorSpeedMultiplier").floatValue = 1.0f;
            cursorSO.ApplyModifiedProperties();

            // 7. Virtual Mouse
            GameObject mouseGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mouseGO.name = "VirtualMouse";
            mouseGO.transform.SetParent(root.transform);
            mouseGO.transform.position = new Vector3(0.30f, 0.755f, 0.58f);
            mouseGO.transform.localScale = new Vector3(0.065f, 0.030f, 0.110f);
            mouseGO.GetComponent<MeshRenderer>().sharedMaterial = mouseMat;

            Rigidbody rb = mouseGO.GetComponent<Rigidbody>();
            if (rb == null) rb = mouseGO.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            XRGrabInteractable grab = mouseGO.GetComponent<XRGrabInteractable>();
            if (grab == null) grab = mouseGO.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.trackPosition = false;
            grab.trackRotation = false;
            grab.throwOnDetach = false;

            VirtualMouseController mouseCtrl = mouseGO.AddComponent<VirtualMouseController>();
            SerializedObject mouseSO = new SerializedObject(mouseCtrl);
            mouseSO.FindProperty("deskSurface").objectReferenceValue = mousePad.transform;
            mouseSO.FindProperty("deskAreaSize").vector2Value = new Vector2(0.36f, 0.26f);
            mouseSO.FindProperty("heightOffset").floatValue = 0.015f;
            mouseSO.FindProperty("mouseSensitivity").floatValue = 6500f;
            mouseSO.ApplyModifiedProperties();

            // 8. Virtual Keyboard
            BuildVirtualKeyboard(root.transform, legMat, keyMat, specialKeyMat);

            // 9. Controller Touch Pointers for 3D Keyboard
            ConfigureControllerPointerTips();

            // Lighting Setup (Natural Warm Daylight + Ambient Boost)
            ConfigureLighting();

            // Ensure EventSystem exists in the scene
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            // Save Scene
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("<color=green><b>[DeskVR]</b> ¡Estudio Luminoso y Estación de trabajo construidos exitosamente!</color>");
        }

        private static void BuildCozyRoomEnvironment(Transform parent, Material floorMat, Material rugMat, Material wallMat, Material baseboardMat, Material ceilingMat, Material windowFrameMat, Material plantPotMat, Material plantLeafMat, Material lampMat)
        {
            GameObject room = new GameObject("Room_Environment");
            room.transform.SetParent(parent, false);

            // 1. Warm Wood Floor
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Room_Floor";
            floor.transform.SetParent(room.transform, false);
            floor.transform.position = new Vector3(0f, -0.015f, 0.40f);
            floor.transform.localScale = new Vector3(4.60f, 0.03f, 4.60f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

            // 2. Cozy Carpet / Rug under Desk
            GameObject rug = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rug.name = "Room_Rug";
            rug.transform.SetParent(room.transform, false);
            rug.transform.position = new Vector3(0f, 0.002f, 0.50f);
            rug.transform.localScale = new Vector3(2.20f, 0.003f, 2.00f);
            rug.GetComponent<MeshRenderer>().sharedMaterial = rugMat;

            // 3. Walls (Room bounds: X: -2.3 to 2.3, Z: -1.9 to 2.7, Y: 0 to 2.8)
            // Back Wall (Behind monitor)
            GameObject backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backWall.name = "Wall_Back";
            backWall.transform.SetParent(room.transform, false);
            backWall.transform.position = new Vector3(0f, 1.40f, 2.65f);
            backWall.transform.localScale = new Vector3(4.60f, 2.80f, 0.10f);
            backWall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // Wall Behind User
            GameObject frontWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frontWall.name = "Wall_BehindUser";
            frontWall.transform.SetParent(room.transform, false);
            frontWall.transform.position = new Vector3(0f, 1.40f, -1.85f);
            frontWall.transform.localScale = new Vector3(4.60f, 2.80f, 0.10f);
            frontWall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // Right Wall
            GameObject rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightWall.name = "Wall_Right";
            rightWall.transform.SetParent(room.transform, false);
            rightWall.transform.position = new Vector3(2.25f, 1.40f, 0.40f);
            rightWall.transform.localScale = new Vector3(0.10f, 2.80f, 4.60f);
            rightWall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // Left Wall with Big Daylight Window
            GameObject leftWallBottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWallBottom.name = "Wall_Left_Bottom";
            leftWallBottom.transform.SetParent(room.transform, false);
            leftWallBottom.transform.position = new Vector3(-2.25f, 0.45f, 0.40f);
            leftWallBottom.transform.localScale = new Vector3(0.10f, 0.90f, 4.60f);
            leftWallBottom.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            GameObject leftWallTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWallTop.name = "Wall_Left_Top";
            leftWallTop.transform.SetParent(room.transform, false);
            leftWallTop.transform.position = new Vector3(-2.25f, 2.45f, 0.40f);
            leftWallTop.transform.localScale = new Vector3(0.10f, 0.70f, 4.60f);
            leftWallTop.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            GameObject leftWallSideA = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWallSideA.name = "Wall_Left_SideA";
            leftWallSideA.transform.SetParent(room.transform, false);
            leftWallSideA.transform.position = new Vector3(-2.25f, 1.45f, -1.15f);
            leftWallSideA.transform.localScale = new Vector3(0.10f, 1.30f, 1.50f);
            leftWallSideA.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            GameObject leftWallSideB = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWallSideB.name = "Wall_Left_SideB";
            leftWallSideB.transform.SetParent(room.transform, false);
            leftWallSideB.transform.position = new Vector3(-2.25f, 1.45f, 1.95f);
            leftWallSideB.transform.localScale = new Vector3(0.10f, 1.30f, 1.50f);
            leftWallSideB.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // Window Frame
            GameObject winFrame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            winFrame.name = "Window_Frame";
            winFrame.transform.SetParent(room.transform, false);
            winFrame.transform.position = new Vector3(-2.24f, 1.45f, 0.40f);
            winFrame.transform.localScale = new Vector3(0.08f, 1.25f, 1.70f);
            winFrame.GetComponent<MeshRenderer>().sharedMaterial = windowFrameMat;

            // Sky / Daylight Vista Quad outside window
            GameObject skyVista = GameObject.CreatePrimitive(PrimitiveType.Quad);
            skyVista.name = "Window_SkyVista";
            skyVista.transform.SetParent(room.transform, false);
            skyVista.transform.position = new Vector3(-2.35f, 1.50f, 0.40f);
            skyVista.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            skyVista.transform.localScale = new Vector3(2.5f, 1.8f, 1f);
            Material skyVistaMat = CreateColorMaterial("SkyVistaMat", new Color(0.82f, 0.92f, 0.99f), 0.1f);
            skyVista.GetComponent<MeshRenderer>().sharedMaterial = skyVistaMat;
            UnityEngine.Object.DestroyImmediate(skyVista.GetComponent<Collider>());

            // Baseboards
            CreateBaseboard(room.transform, new Vector3(0f, 0.05f, 2.59f), new Vector3(4.5f, 0.10f, 0.02f), baseboardMat);
            CreateBaseboard(room.transform, new Vector3(2.19f, 0.05f, 0.40f), new Vector3(0.02f, 0.10f, 4.5f), baseboardMat);

            // 4. Ceiling & 4 Bright Recessed Downlights
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Room_Ceiling";
            ceiling.transform.SetParent(room.transform, false);
            ceiling.transform.position = new Vector3(0f, 2.81f, 0.40f);
            ceiling.transform.localScale = new Vector3(4.60f, 0.02f, 4.60f);
            ceiling.GetComponent<MeshRenderer>().sharedMaterial = ceilingMat;

            // 4 Powerful Recessed Downlights
            Vector3[] ceilingLightPositions = new Vector3[]
            {
                new Vector3( 0.00f, 2.70f,  0.65f), // Directly above desk & monitor
                new Vector3( 0.00f, 2.70f, -0.60f), // Above user
                new Vector3(-1.10f, 2.70f,  1.10f), // Left side
                new Vector3( 1.10f, 2.70f,  1.10f)  // Right side
            };

            Material fixtureMat = CreateColorMaterial("FixtureMat", new Color(0.96f, 0.96f, 0.96f), 0.8f);

            for (int i = 0; i < ceilingLightPositions.Length; i++)
            {
                GameObject fixture = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fixture.name = $"Ceiling_LightFixture_{i + 1}";
                fixture.transform.SetParent(room.transform, false);
                fixture.transform.position = ceilingLightPositions[i];
                fixture.transform.localScale = new Vector3(0.24f, 0.015f, 0.24f);
                fixture.GetComponent<MeshRenderer>().sharedMaterial = fixtureMat;
                UnityEngine.Object.DestroyImmediate(fixture.GetComponent<Collider>());

                GameObject lightGO = new GameObject($"Downlight_{i + 1}");
                lightGO.transform.SetParent(fixture.transform, false);
                lightGO.transform.localPosition = new Vector3(0f, -0.05f, 0f);

                Light pLight = lightGO.AddComponent<Light>();
                pLight.type = LightType.Point;
                pLight.color = new Color(1.0f, 0.97f, 0.92f);
                pLight.intensity = (i == 0) ? 3.0f : 2.2f; // High intensity over the desk
                pLight.range = 9.0f;
            }

            // 5. Desk Lamp (with warm bright light)
            BuildDeskLamp(room.transform, lampMat);

            // 6. Succulent Plant on Desk
            BuildDeskPlant(room.transform, plantPotMat, plantLeafMat);

            // 7. Floating Shelf on Back Wall (with books & art)
            BuildWallShelf(room.transform, baseboardMat);
        }

        private static void CreateBaseboard(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject bb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bb.name = "Baseboard";
            bb.transform.SetParent(parent, false);
            bb.transform.position = pos;
            bb.transform.localScale = scale;
            bb.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static void BuildDeskLamp(Transform parent, Material lampMat)
        {
            GameObject lampRoot = new GameObject("Desk_Lamp");
            lampRoot.transform.SetParent(parent, false);
            lampRoot.transform.position = new Vector3(-0.52f, 0.742f, 0.88f);

            GameObject lampBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lampBase.transform.SetParent(lampRoot.transform, false);
            lampBase.transform.localPosition = Vector3.zero;
            lampBase.transform.localScale = new Vector3(0.12f, 0.008f, 0.12f);
            lampBase.GetComponent<MeshRenderer>().sharedMaterial = lampMat;

            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.transform.SetParent(lampRoot.transform, false);
            stem.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            stem.transform.localScale = new Vector3(0.015f, 0.18f, 0.015f);
            stem.GetComponent<MeshRenderer>().sharedMaterial = lampMat;

            GameObject shade = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shade.transform.SetParent(lampRoot.transform, false);
            shade.transform.localPosition = new Vector3(0.08f, 0.35f, -0.06f);
            shade.transform.localRotation = Quaternion.Euler(35f, -40f, 0f);
            shade.transform.localScale = new Vector3(0.10f, 0.06f, 0.10f);
            shade.GetComponent<MeshRenderer>().sharedMaterial = lampMat;

            GameObject lightGO = new GameObject("Lamp_WarmLight");
            lightGO.transform.SetParent(shade.transform, false);
            lightGO.transform.localPosition = new Vector3(0f, -0.04f, 0f);
            Light pLight = lightGO.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.color = new Color(1.0f, 0.92f, 0.78f);
            pLight.intensity = 2.4f;
            pLight.range = 2.5f;
        }

        private static void BuildDeskPlant(Transform parent, Material plantPotMat, Material plantLeafMat)
        {
            GameObject plantRoot = new GameObject("Desk_Plant_Succulent");
            plantRoot.transform.SetParent(parent, false);
            plantRoot.transform.position = new Vector3(0.54f, 0.742f, 0.88f);

            GameObject pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pot.transform.SetParent(plantRoot.transform, false);
            pot.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            pot.transform.localScale = new Vector3(0.09f, 0.04f, 0.09f);
            pot.GetComponent<MeshRenderer>().sharedMaterial = plantPotMat;

            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f;
                GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leaf.name = $"Leaf_{i}";
                leaf.transform.SetParent(plantRoot.transform, false);
                leaf.transform.localPosition = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * 0.025f, 0.085f, Mathf.Cos(angle * Mathf.Deg2Rad) * 0.025f);
                leaf.transform.localRotation = Quaternion.Euler(20f, angle, 0f);
                leaf.transform.localScale = new Vector3(0.035f, 0.015f, 0.06f);
                leaf.GetComponent<MeshRenderer>().sharedMaterial = plantLeafMat;
            }
        }

        private static void BuildWallShelf(Transform parent, Material woodMat)
        {
            GameObject shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shelf.name = "Wall_Shelf";
            shelf.transform.SetParent(parent, false);
            shelf.transform.position = new Vector3(0f, 1.85f, 2.58f);
            shelf.transform.localScale = new Vector3(1.30f, 0.03f, 0.22f);
            shelf.GetComponent<MeshRenderer>().sharedMaterial = woodMat;

            Material bookMat1 = CreateColorMaterial("BookBlueMat", new Color(0.18f, 0.35f, 0.65f), 0.2f);
            Material bookMat2 = CreateColorMaterial("BookRedMat", new Color(0.68f, 0.22f, 0.20f), 0.2f);

            GameObject book1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            book1.name = "Book_1";
            book1.transform.SetParent(shelf.transform, false);
            book1.transform.localPosition = new Vector3(-0.30f, 3.5f, 0f);
            book1.transform.localScale = new Vector3(0.06f, 5.5f, 0.7f);
            book1.GetComponent<MeshRenderer>().sharedMaterial = bookMat1;

            GameObject book2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            book2.name = "Book_2";
            book2.transform.SetParent(shelf.transform, false);
            book2.transform.localPosition = new Vector3(-0.22f, 3.2f, 0f);
            book2.transform.localScale = new Vector3(0.05f, 4.8f, 0.68f);
            book2.GetComponent<MeshRenderer>().sharedMaterial = bookMat2;
        }

        private static void ConfigureLighting()
        {
            // 1. Ambient Lighting (Illuminates interior room surfaces evenly)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.76f, 0.78f, 0.82f);
            RenderSettings.ambientIntensity = 1.35f;

            // 2. Sunlight / Directional Light
            Light dirLight = UnityEngine.Object.FindFirstObjectByType<Light>();
            if (dirLight != null && dirLight.type == LightType.Directional)
            {
                dirLight.transform.rotation = Quaternion.Euler(35f, 55f, 0f);
                dirLight.color = new Color(1.0f, 0.97f, 0.92f);
                dirLight.intensity = 1.35f;
                dirLight.shadows = LightShadows.Soft;
            }
        }

        private static void BuildNotepadWindow(Transform canvasTransform, Button triggerIconBtn)
        {
            GameObject winGO = new GameObject("Window_BlocNotas", typeof(RectTransform), typeof(Image));
            winGO.transform.SetParent(canvasTransform, false);
            RectTransform winRect = winGO.GetComponent<RectTransform>();
            winRect.anchorMin = new Vector2(0.5f, 0.5f);
            winRect.anchorMax = new Vector2(0.5f, 0.5f);
            winRect.pivot = new Vector2(0.5f, 0.5f);
            winRect.anchoredPosition = new Vector2(80f, 30f);
            winRect.sizeDelta = new Vector2(740f, 480f);
            winGO.GetComponent<Image>().color = new Color(0.92f, 0.93f, 0.95f);

            VirtualWindow vWin = winGO.AddComponent<VirtualWindow>();

            GameObject titleBar = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
            titleBar.transform.SetParent(winGO.transform, false);
            RectTransform titleRect = titleBar.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0f, 44f);
            titleBar.GetComponent<Image>().color = new Color(0.14f, 0.18f, 0.26f);

            GameObject titleTextGO = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
            titleTextGO.transform.SetParent(titleBar.transform, false);
            RectTransform ttRect = titleTextGO.GetComponent<RectTransform>();
            ttRect.anchorMin = new Vector2(0f, 0f);
            ttRect.anchorMax = new Vector2(1f, 1f);
            ttRect.offsetMin = new Vector2(16f, 0f);
            ttRect.offsetMax = new Vector2(-100f, 0f);
            Text tt = titleTextGO.GetComponent<Text>();
            tt.text = "📝 Bloc de Notas (DeskVR)";
            tt.alignment = TextAnchor.MiddleLeft;
            tt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tt.fontSize = 20;
            tt.fontStyle = FontStyle.Bold;
            tt.color = Color.white;

            GameObject closeBtnGO = new GameObject("Btn_Close", typeof(RectTransform), typeof(Image), typeof(Button));
            closeBtnGO.transform.SetParent(titleBar.transform, false);
            RectTransform cbRect = closeBtnGO.GetComponent<RectTransform>();
            cbRect.anchorMin = new Vector2(1f, 0.5f);
            cbRect.anchorMax = new Vector2(1f, 0.5f);
            cbRect.pivot = new Vector2(1f, 0.5f);
            cbRect.anchoredPosition = new Vector2(-6f, 0f);
            cbRect.sizeDelta = new Vector2(36f, 32f);
            closeBtnGO.GetComponent<Image>().color = new Color(0.85f, 0.25f, 0.25f);
            Button closeBtn = closeBtnGO.GetComponent<Button>();

            GameObject closeTextGO = new GameObject("X", typeof(RectTransform), typeof(Text));
            closeTextGO.transform.SetParent(closeBtnGO.transform, false);
            RectTransform ctRect = closeTextGO.GetComponent<RectTransform>();
            ctRect.anchorMin = Vector2.zero;
            ctRect.anchorMax = Vector2.one;
            ctRect.sizeDelta = Vector2.zero;
            Text ct = closeTextGO.GetComponent<Text>();
            ct.text = "✕";
            ct.alignment = TextAnchor.MiddleCenter;
            ct.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ct.fontSize = 20;
            ct.color = Color.white;

            GameObject contentArea = new GameObject("ContentArea", typeof(RectTransform), typeof(Image));
            contentArea.transform.SetParent(winGO.transform, false);
            RectTransform caRect = contentArea.GetComponent<RectTransform>();
            caRect.anchorMin = Vector2.zero;
            caRect.anchorMax = Vector2.one;
            caRect.offsetMin = new Vector2(12f, 12f);
            caRect.offsetMax = new Vector2(-12f, -50f);
            contentArea.GetComponent<Image>().color = Color.white;

            GameObject textGO = new GameObject("DisplayText", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(contentArea.transform, false);
            RectTransform textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 16f);
            textRect.offsetMax = new Vector2(-16f, -16f);
            Text displayText = textGO.GetComponent<Text>();
            displayText.text = "";
            displayText.alignment = TextAnchor.UpperLeft;
            displayText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            displayText.fontSize = 24;
            displayText.color = new Color(0.1f, 0.12f, 0.16f);

            GameObject phGO = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            phGO.transform.SetParent(contentArea.transform, false);
            RectTransform phRect = phGO.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = new Vector2(16f, 16f);
            phRect.offsetMax = new Vector2(-16f, -16f);
            Text phText = phGO.GetComponent<Text>();
            phText.text = "Toca las teclas del teclado 3D en tu mesa para escribir aquí...";
            phText.alignment = TextAnchor.UpperLeft;
            phText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            phText.fontSize = 22;
            phText.fontStyle = FontStyle.Italic;
            phText.color = new Color(0.55f, 0.60f, 0.68f);

            TextEditorApp editorApp = contentArea.AddComponent<TextEditorApp>();
            SerializedObject editorSO = new SerializedObject(editorApp);
            editorSO.FindProperty("contentText").objectReferenceValue = displayText;
            editorSO.FindProperty("placeholderText").objectReferenceValue = phText;
            editorSO.FindProperty("parentWindow").objectReferenceValue = vWin;
            editorSO.ApplyModifiedProperties();

            SerializedObject winSO = new SerializedObject(vWin);
            winSO.FindProperty("windowTitle").stringValue = "Bloc de Notas";
            winSO.FindProperty("windowRect").objectReferenceValue = winRect;
            winSO.FindProperty("titleBarRect").objectReferenceValue = titleRect;
            winSO.FindProperty("titleText").objectReferenceValue = tt;
            winSO.FindProperty("closeButton").objectReferenceValue = closeBtn;
            winSO.ApplyModifiedProperties();

            if (triggerIconBtn != null)
            {
                UnityEditor.Events.UnityEventTools.AddPersistentListener(triggerIconBtn.onClick, vWin.Open);
            }

            vWin.Open();
        }

        private static void BuildVirtualKeyboard(Transform parent, Material chassisMat, Material keyMat, Material specialKeyMat)
        {
            GameObject kbRoot = new GameObject("VirtualKeyboard");
            kbRoot.transform.SetParent(parent, false);
            kbRoot.transform.position = new Vector3(-0.06f, 0.748f, 0.58f);
            kbRoot.transform.rotation = Quaternion.Euler(4f, 0f, 0f);

            var kbRb = kbRoot.AddComponent<Rigidbody>();
            kbRb.isKinematic = true;
            kbRb.useGravity = false;

            kbRoot.AddComponent<VirtualKeyboardController>();

            GameObject chassis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chassis.name = "Keyboard_Chassis";
            chassis.transform.SetParent(kbRoot.transform, false);
            chassis.transform.localPosition = Vector3.zero;
            chassis.transform.localScale = new Vector3(0.40f, 0.012f, 0.15f);
            chassis.GetComponent<MeshRenderer>().sharedMaterial = chassisMat;

            var row0 = new (string, string, KeyActionType, float)[]
            {
                ("1", "1", KeyActionType.Character, 1f),
                ("2", "2", KeyActionType.Character, 1f),
                ("3", "3", KeyActionType.Character, 1f),
                ("4", "4", KeyActionType.Character, 1f),
                ("5", "5", KeyActionType.Character, 1f),
                ("6", "6", KeyActionType.Character, 1f),
                ("7", "7", KeyActionType.Character, 1f),
                ("8", "8", KeyActionType.Character, 1f),
                ("9", "9", KeyActionType.Character, 1f),
                ("0", "0", KeyActionType.Character, 1f),
                ("Borrar", "", KeyActionType.Backspace, 1.8f)
            };

            var row1 = new (string, string, KeyActionType, float)[]
            {
                ("Q", "Q", KeyActionType.Character, 1f),
                ("W", "W", KeyActionType.Character, 1f),
                ("E", "E", KeyActionType.Character, 1f),
                ("R", "R", KeyActionType.Character, 1f),
                ("T", "T", KeyActionType.Character, 1f),
                ("Y", "Y", KeyActionType.Character, 1f),
                ("U", "U", KeyActionType.Character, 1f),
                ("I", "I", KeyActionType.Character, 1f),
                ("O", "O", KeyActionType.Character, 1f),
                ("P", "P", KeyActionType.Character, 1f)
            };

            var row2 = new (string, string, KeyActionType, float)[]
            {
                ("A", "A", KeyActionType.Character, 1f),
                ("S", "S", KeyActionType.Character, 1f),
                ("D", "D", KeyActionType.Character, 1f),
                ("F", "F", KeyActionType.Character, 1f),
                ("G", "G", KeyActionType.Character, 1f),
                ("H", "H", KeyActionType.Character, 1f),
                ("J", "J", KeyActionType.Character, 1f),
                ("K", "K", KeyActionType.Character, 1f),
                ("L", "L", KeyActionType.Character, 1f),
                ("Enter", "\n", KeyActionType.Enter, 1.9f)
            };

            var row3 = new (string, string, KeyActionType, float)[]
            {
                ("Z", "Z", KeyActionType.Character, 1f),
                ("X", "X", KeyActionType.Character, 1f),
                ("C", "C", KeyActionType.Character, 1f),
                ("V", "V", KeyActionType.Character, 1f),
                ("B", "B", KeyActionType.Character, 1f),
                ("N", "N", KeyActionType.Character, 1f),
                ("M", "M", KeyActionType.Character, 1f),
                (",", ",", KeyActionType.Character, 1f),
                (".", ".", KeyActionType.Character, 1f)
            };

            var row4 = new (string, string, KeyActionType, float)[]
            {
                ("Espacio", " ", KeyActionType.Space, 6.2f)
            };

            var allRows = new[] { row0, row1, row2, row3, row4 };

            float startZ = 0.052f;
            float rowSpacing = 0.026f;
            float standardKeyWidth = 0.026f;
            float keyHeight = 0.008f;
            float keyDepth = 0.022f;

            for (int r = 0; r < allRows.Length; r++)
            {
                var row = allRows[r];
                float currentZ = startZ - (r * rowSpacing);

                float totalRowWidth = 0f;
                foreach (var k in row)
                {
                    totalRowWidth += (standardKeyWidth * k.Item4) + 0.004f;
                }
                float currentX = -totalRowWidth * 0.5f;

                foreach (var keyDef in row)
                {
                    float width = standardKeyWidth * keyDef.Item4;
                    float posX = currentX + (width * 0.5f);

                    CreateKeyObject(kbRoot.transform, keyDef.Item1, keyDef.Item2, keyDef.Item3,
                        new Vector3(posX, 0.008f, currentZ),
                        new Vector3(width, keyHeight, keyDepth),
                        keyDef.Item3 == KeyActionType.Character ? keyMat : specialKeyMat);

                    currentX += width + 0.004f;
                }
            }
        }

        private static void CreateKeyObject(Transform parent, string label, string value, KeyActionType type, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject keyGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            keyGO.name = $"Key_{label}";
            keyGO.transform.SetParent(parent, false);
            keyGO.transform.localPosition = localPos;
            keyGO.transform.localScale = localScale;
            keyGO.GetComponent<MeshRenderer>().sharedMaterial = mat;

            BoxCollider col = keyGO.GetComponent<BoxCollider>();
            col.isTrigger = true;

            VirtualKey vKey = keyGO.AddComponent<VirtualKey>();
            vKey.Configure(type, value, label);

            GameObject labelCanvas = new GameObject("LabelCanvas");
            labelCanvas.transform.SetParent(keyGO.transform, false);
            labelCanvas.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            labelCanvas.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelCanvas.transform.localScale = new Vector3(0.0025f, 0.0025f, 0.0025f);

            Canvas c = labelCanvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            RectTransform rt = labelCanvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100f, 80f);

            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(labelCanvas.transform, false);
            RectTransform textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;

            Text txt = textGO.AddComponent<Text>();
            txt.text = label;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = label.Length > 2 ? 22 : 36;
            txt.fontStyle = FontStyle.Bold;
            txt.color = new Color(0.12f, 0.12f, 0.15f);
        }

        private static Camera FindAndTagVRCamera()
        {
            var allCams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Camera chosenCam = null;

            foreach (var cam in allCams)
            {
                string n = cam.name.ToLower();
                if (n == "main camera" || n.Contains("vr") || n.Contains("head"))
                {
                    chosenCam = cam;
                    break;
                }
            }

            if (chosenCam == null && allCams.Length > 0)
            {
                chosenCam = allCams[0];
            }

            if (chosenCam != null)
            {
                chosenCam.gameObject.tag = "MainCamera";
            }

            return chosenCam;
        }

        private static void ConfigureControllerPointerTips()
        {
            var allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Material tipMat = CreateColorMaterial("PointerTipMat", new Color(0.18f, 0.72f, 1.0f), 0.85f);

            foreach (var t in allTransforms)
            {
                string n = t.name.ToLower();
                if (n == "left controller" || n == "right controller" || n == "lefthand controller" || n == "righthand controller")
                {
                    Transform existingTip = t.Find("DeskVR_PointerTip");
                    GameObject tipGO;
                    if (existingTip == null)
                    {
                        tipGO = new GameObject("DeskVR_PointerTip");
                        tipGO.transform.SetParent(t, false);
                    }
                    else
                    {
                        tipGO = existingTip.gameObject;
                    }

                    // Forward tip of controller (Quest 2 natural pointing angle & reach)
                    tipGO.transform.localPosition = new Vector3(0f, -0.015f, 0.085f);
                    tipGO.transform.localRotation = Quaternion.identity;

                    var col = tipGO.GetComponent<SphereCollider>();
                    if (col == null) col = tipGO.AddComponent<SphereCollider>();
                    col.radius = 0.018f;
                    col.isTrigger = true;

                    var rb = tipGO.GetComponent<Rigidbody>();
                    if (rb == null) rb = tipGO.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                    rb.useGravity = false;

                    Transform visTrans = tipGO.transform.Find("VisualTip");
                    GameObject visualTip;
                    if (visTrans == null)
                    {
                        visualTip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        visualTip.name = "VisualTip";
                        visualTip.transform.SetParent(tipGO.transform, false);
                        UnityEngine.Object.DestroyImmediate(visualTip.GetComponent<Collider>());
                    }
                    else
                    {
                        visualTip = visTrans.gameObject;
                    }

                    visualTip.transform.localPosition = Vector3.zero;
                    visualTip.transform.localScale = new Vector3(0.014f, 0.014f, 0.014f);
                    visualTip.GetComponent<MeshRenderer>().sharedMaterial = tipMat;
                }
            }
        }

        private static void SetSceneAsDefaultBuildScene(string scenePath)
        {
            var scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(scenePath, true)
            };
            EditorBuildSettings.scenes = scenes;
        }

        private static Material CreateColorMaterial(string matName, Color color, float smoothness = 0.35f)
        {
            string folder = "Assets/_Project/Materials";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            }
            string path = $"{folder}/{matName}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.color = color;
                existing.SetFloat("_Smoothness", smoothness);
                return existing;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            Material mat = new Material(urpLit);
            mat.color = color;
            mat.SetFloat("_Smoothness", smoothness);

            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
