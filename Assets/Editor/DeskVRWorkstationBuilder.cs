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
                "¡Se abrió tu escena 'DeskVR_Main' y se construyó la estación completa:\n\n• Mesa y patas de oficina\n• Monitor con escritorio simulado\n• Mouse 3D físico con físicas y hápticos\n• Teclado 3D físico completo con teclas presionables\n\n¡Dale a Play ▶️ para probar con tus Quest 2!",
                "¡Excelente!"
            );
        }

        [MenuItem("DeskVR/2. Reconstruir Estación en Escena Actual", priority = 2)]
        public static void BuildWorkstationMenu()
        {
            BuildWorkstation();
            EditorUtility.DisplayDialog(
                "DeskVR",
                "¡Estación de trabajo (Mesa, Monitor, Mouse 3D y Teclado 3D) actualizada en la escena actual!\n\nDale a Play ▶️ para probar.",
                "¡Genial!"
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

            // Materials
            Material deskMat = CreateColorMaterial("DeskMat", new Color(0.25f, 0.18f, 0.12f)); // Wood brown
            Material legMat = CreateColorMaterial("LegMat", new Color(0.12f, 0.12f, 0.14f));  // Dark metal
            Material padMat = CreateColorMaterial("PadMat", new Color(0.08f, 0.12f, 0.18f));  // Navy pad
            Material mouseMat = CreateColorMaterial("MouseMat", new Color(0.18f, 0.18f, 0.20f)); // Matte dark mouse
            Material screenBezelMat = CreateColorMaterial("BezelMat", new Color(0.05f, 0.05f, 0.05f));
            Material keyMat = CreateColorMaterial("KeyMat", new Color(0.92f, 0.92f, 0.94f));  // Off-white keys
            Material specialKeyMat = CreateColorMaterial("SpecialKeyMat", new Color(0.75f, 0.78f, 0.82f)); // Darker gray for enter/space

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

            // 6c. Desktop App Icons
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

            // 6d. Screen Cursor
            GameObject cursorGO = new GameObject("ScreenCursor", typeof(RectTransform), typeof(Image));
            cursorGO.transform.SetParent(canvasGO.transform, false);
            RectTransform cursorRect = cursorGO.GetComponent<RectTransform>();
            cursorRect.pivot = new Vector2(0f, 1f);
            cursorRect.sizeDelta = new Vector2(28f, 28f);
            cursorRect.anchoredPosition = Vector2.zero;
            Image curImg = cursorGO.GetComponent<Image>();
            curImg.color = Color.white;
            curImg.raycastTarget = false;

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
            grab.throwOnDetach = false;

            VirtualMouseController mouseCtrl = mouseGO.AddComponent<VirtualMouseController>();
            SerializedObject mouseSO = new SerializedObject(mouseCtrl);
            mouseSO.FindProperty("deskSurface").objectReferenceValue = mousePad.transform;
            mouseSO.FindProperty("deskAreaSize").vector2Value = new Vector2(0.36f, 0.26f);
            mouseSO.FindProperty("heightOffset").floatValue = 0.015f;
            mouseSO.FindProperty("mouseSensitivity").floatValue = 1300f;
            mouseSO.ApplyModifiedProperties();

            // 8. Virtual Keyboard (Physical 3D Object with full QWERTY layout)
            BuildVirtualKeyboard(root.transform, legMat, keyMat, specialKeyMat);

            // Ensure EventSystem exists in the scene
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            // Save Scene
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("<color=green><b>[DeskVR]</b> ¡Estación de trabajo completa (Mesa + Monitor + Mouse + Teclado 3D) construida!</color>");
        }

        private static void BuildVirtualKeyboard(Transform parent, Material chassisMat, Material keyMat, Material specialKeyMat)
        {
            GameObject kbRoot = new GameObject("VirtualKeyboard");
            kbRoot.transform.SetParent(parent, false);
            // Positioned in front of user, left of mouse
            kbRoot.transform.position = new Vector3(-0.06f, 0.748f, 0.58f);
            kbRoot.transform.rotation = Quaternion.Euler(4f, 0f, 0f);

            var kbRb = kbRoot.AddComponent<Rigidbody>();
            kbRb.isKinematic = true;
            kbRb.useGravity = false;

            kbRoot.AddComponent<VirtualKeyboardController>();

            // Keyboard Chassis Base
            GameObject chassis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chassis.name = "Keyboard_Chassis";
            chassis.transform.SetParent(kbRoot.transform, false);
            chassis.transform.localPosition = Vector3.zero;
            chassis.transform.localScale = new Vector3(0.40f, 0.012f, 0.15f);
            chassis.GetComponent<MeshRenderer>().sharedMaterial = chassisMat;

            // Key Definitions: (Label, Value, ActionType, WidthMultiplier)
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
                ("Q", "q", KeyActionType.Character, 1f),
                ("W", "w", KeyActionType.Character, 1f),
                ("E", "e", KeyActionType.Character, 1f),
                ("R", "r", KeyActionType.Character, 1f),
                ("T", "t", KeyActionType.Character, 1f),
                ("Y", "y", KeyActionType.Character, 1f),
                ("U", "u", KeyActionType.Character, 1f),
                ("I", "i", KeyActionType.Character, 1f),
                ("O", "o", KeyActionType.Character, 1f),
                ("P", "p", KeyActionType.Character, 1f)
            };

            var row2 = new (string, string, KeyActionType, float)[]
            {
                ("A", "a", KeyActionType.Character, 1f),
                ("S", "s", KeyActionType.Character, 1f),
                ("D", "d", KeyActionType.Character, 1f),
                ("F", "f", KeyActionType.Character, 1f),
                ("G", "g", KeyActionType.Character, 1f),
                ("H", "h", KeyActionType.Character, 1f),
                ("J", "j", KeyActionType.Character, 1f),
                ("K", "k", KeyActionType.Character, 1f),
                ("L", "l", KeyActionType.Character, 1f),
                ("Enter", "\n", KeyActionType.Enter, 1.9f)
            };

            var row3 = new (string, string, KeyActionType, float)[]
            {
                ("Z", "z", KeyActionType.Character, 1f),
                ("X", "x", KeyActionType.Character, 1f),
                ("C", "c", KeyActionType.Character, 1f),
                ("V", "v", KeyActionType.Character, 1f),
                ("B", "b", KeyActionType.Character, 1f),
                ("N", "n", KeyActionType.Character, 1f),
                ("M", "m", KeyActionType.Character, 1f),
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

                // Calculate total width of row to center it
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

            // Trigger collider for touch / poke detection
            BoxCollider col = keyGO.GetComponent<BoxCollider>();
            col.isTrigger = true;

            VirtualKey vKey = keyGO.AddComponent<VirtualKey>();
            vKey.Configure(type, value, label);

            // Small Label Canvas on top of the key
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
