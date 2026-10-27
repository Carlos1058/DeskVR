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
            // 1. Save whatever scene the user has open
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            // 2. Open our clean dedicated DeskVR scene
            var mainScene = EditorSceneManager.OpenScene(MainScenePath);

            // 3. Build the workstation cleanly inside it
            BuildWorkstation();

            // 4. Set as default build scene 0
            SetSceneAsDefaultBuildScene(MainScenePath);

            EditorUtility.DisplayDialog(
                "DeskVR - Escena Lista",
                "¡Se abrió tu escena limpia 'DeskVR_Main' y se construyó la estación de trabajo sin objetos de prueba de la plantilla!\n\nAhora dale a Play ▶️ para probar con tus Quest 2.",
                "¡Entendido!"
            );
        }

        [MenuItem("DeskVR/2. Reconstruir Estación en Escena Actual", priority = 2)]
        public static void BuildWorkstationMenu()
        {
            BuildWorkstation();
            EditorUtility.DisplayDialog(
                "DeskVR",
                "¡Estación de trabajo (Mesa, Monitor y Mouse 3D) actualizada en la escena actual!\n\nDale a Play ▶️ para probar.",
                "¡Excelente!"
            );
        }

        public static void BuildWorkstation()
        {
            var activeScene = EditorSceneManager.GetActiveScene();

            var existing = GameObject.Find(WorkstationRootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            // 1. Root Workstation Object
            GameObject root = new GameObject(WorkstationRootName);
            Undo.RegisterCreatedObjectUndo(root, "Build DeskVR Workstation");
            root.transform.position = Vector3.zero;

            // Materials creation or color setup
            Material deskMat = CreateColorMaterial("DeskMat", new Color(0.25f, 0.18f, 0.12f)); // Wood brown
            Material legMat = CreateColorMaterial("LegMat", new Color(0.12f, 0.12f, 0.14f));  // Dark metal
            Material padMat = CreateColorMaterial("PadMat", new Color(0.08f, 0.12f, 0.18f));  // Navy pad
            Material mouseMat = CreateColorMaterial("MouseMat", new Color(0.18f, 0.18f, 0.20f)); // Matte dark mouse
            Material screenBezelMat = CreateColorMaterial("BezelMat", new Color(0.05f, 0.05f, 0.05f));

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
            mousePad.transform.position = new Vector3(0.28f, 0.741f, 0.58f);
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
            wallpaper.GetComponent<Image>().color = new Color(0.08f, 0.16f, 0.28f); // Windows Dark Blue

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

            // 6c. Desktop App Icons (Test Interactive Target)
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

            // 6d. Screen Cursor (2D Arrow/Pointer)
            GameObject cursorGO = new GameObject("ScreenCursor", typeof(RectTransform), typeof(Image));
            cursorGO.transform.SetParent(canvasGO.transform, false);
            RectTransform cursorRect = cursorGO.GetComponent<RectTransform>();
            cursorRect.pivot = new Vector2(0f, 1f); // Arrow tip at top-left
            cursorRect.sizeDelta = new Vector2(28f, 28f);
            cursorRect.anchoredPosition = Vector2.zero;
            Image curImg = cursorGO.GetComponent<Image>();
            curImg.color = Color.white;
            curImg.raycastTarget = false; // Don't raycast self!

            VirtualScreenCursor screenCursor = cursorGO.AddComponent<VirtualScreenCursor>();
            SerializedObject cursorSO = new SerializedObject(screenCursor);
            cursorSO.FindProperty("screenRect").objectReferenceValue = canvasRect;
            cursorSO.FindProperty("cursorRect").objectReferenceValue = cursorRect;
            cursorSO.FindProperty("raycaster").objectReferenceValue = raycaster;
            cursorSO.ApplyModifiedProperties();

            // 7. Virtual Mouse (Physical 3D Object on table)
            GameObject mouseGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mouseGO.name = "VirtualMouse";
            mouseGO.transform.SetParent(root.transform);
            mouseGO.transform.position = new Vector3(0.28f, 0.755f, 0.58f);
            mouseGO.transform.localScale = new Vector3(0.065f, 0.030f, 0.110f);
            mouseGO.GetComponent<MeshRenderer>().sharedMaterial = mouseMat;

            // Rigidbody and Grab Interactable
            Rigidbody rb = mouseGO.GetComponent<Rigidbody>();
            if (rb == null) rb = mouseGO.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            XRGrabInteractable grab = mouseGO.GetComponent<XRGrabInteractable>();
            if (grab == null) grab = mouseGO.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;

            VirtualMouseController mouseCtrl = mouseGO.AddComponent<VirtualMouseController>();
            SerializedObject mouseSO = new SerializedObject(mouseCtrl);
            mouseSO.FindProperty("deskSurface").objectReferenceValue = mousePad.transform;
            mouseSO.FindProperty("deskAreaSize").vector2Value = new Vector2(0.36f, 0.26f);
            mouseSO.FindProperty("heightOffset").floatValue = 0.015f;
            mouseSO.FindProperty("mouseSensitivity").floatValue = 1300f;
            mouseSO.ApplyModifiedProperties();

            // Ensure EventSystem exists in the scene
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            // Save Scene
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("<color=green><b>[DeskVR]</b> ¡Estación de trabajo construida exitosamente en " + activeScene.name + "!</color>");
        }

        private static void SetSceneAsDefaultBuildScene(string scenePath)
        {
            var scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(scenePath, true)
            };
            EditorBuildSettings.scenes = scenes;
        }

        private static Material CreateColorMaterial(string matName, Color color)
        {
            string folder = "Assets/_Project/Materials";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            }
            string path = $"{folder}/{matName}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            Material mat = new Material(urpLit);
            mat.color = color;
            mat.SetFloat("_Smoothness", 0.35f);

            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
