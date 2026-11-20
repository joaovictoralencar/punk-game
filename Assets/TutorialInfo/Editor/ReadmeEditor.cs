using System;
using System.IO;
using System.Linq;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(Readme))]
sealed partial class ReadmeEditor : Editor
{
    const string k_ShowedReadmeSessionStateName = "ReadmeEditor.showedReadme";
    const string k_ReadmeSourceDirectory = "Assets/TutorialInfo";
    const string k_ShowedSceneTemplateDialogConfigName = "ReadmeEditor.showedSceneTemplateDialog";
    const string k_NewSceneMenuItem = "File/New Scene";
    const string k_SceneTemplateDialogTypeName = "SceneTemplateDialog";
    const int k_MaxCenteringAttempts = 30;

    [OnCodeLoaded]
    static void OnCodeLoaded()
    {
        EditorApplication.delayCall += () =>
        {
            SelectReadmeOnOpen();
            ShowSceneTemplateDialogOnFirstOpen();
        };
    }

    static void SelectReadmeOnOpen()
    {
        if (SessionState.GetBool(k_ShowedReadmeSessionStateName, false))
            return;

        SessionState.SetBool(k_ShowedReadmeSessionStateName, true);
        SelectReadme();
    }

    static void ShowSceneTemplateDialogOnFirstOpen()
    {
        if (Application.isBatchMode)
            return;

        var commandLineArgs = Environment.GetCommandLineArgs();
        if (commandLineArgs.Contains("-automated"))
            return;

        if (EditorUserSettings.GetConfigValue(k_ShowedSceneTemplateDialogConfigName) == "1")
            return;

        EditorUserSettings.SetConfigValue(k_ShowedSceneTemplateDialogConfigName, "1");

        if (!EditorApplication.ExecuteMenuItem(k_NewSceneMenuItem))
        {
            Debug.LogWarning($"Couldn't open the scene template dialog via '{k_NewSceneMenuItem}'");
            return;
        }

        if (TryCenterSceneTemplateDialog())
            return;

        // The dialog may not be laid out on the same frame it's opened, so keep trying to
        // center it on the main editor window for a few frames.
        var attempts = 0;
        void CenterDialogWhenReady()
        {
            if (TryCenterSceneTemplateDialog() || ++attempts > k_MaxCenteringAttempts)
                EditorApplication.update -= CenterDialogWhenReady;
        }

        EditorApplication.update += CenterDialogWhenReady;
    }


    static bool TryCenterSceneTemplateDialog()
    {
        var dialog = FindSceneTemplateDialog();
        if (dialog != null)
        {
            CenterOnMainWindow(dialog);
            return true;
        }

        return false;
    }

    static EditorWindow FindSceneTemplateDialog()
    {
        foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            if (window.GetType().Name == k_SceneTemplateDialogTypeName)
                return window;
        return null;
    }

    static void CenterOnMainWindow(EditorWindow window)
    {
        var main = EditorGUIUtility.GetMainWindowPosition();
        var pos = window.position;
        pos.width = Mathf.Min(pos.width, main.width * 0.9f);
        pos.height = Mathf.Min(pos.height, main.height * 0.9f);
        pos.x = main.x + (main.width - pos.width) * 0.5f;
        pos.y = main.y + (main.height - pos.height) * 0.5f;
        window.position = pos;
    }

    static Readme SelectReadme()
    {
        var ids = AssetDatabase.FindAssetGUIDs("Readme t:Readme");
        if (ids.Length != 1)
        {
            Debug.LogWarning("Couldn't find a readme");
            return null;
        }

        var readmeObject = AssetDatabase.LoadAssetByGUID<Readme>(ids[0]);
        Selection.activeObject = readmeObject;
        return readmeObject;
    }

    static void RemoveTutorial()
    {
        if (EditorUtility.DisplayDialog("Remove Readme Assets",
                                        $"All contents under {k_ReadmeSourceDirectory} will be removed, are you sure you want to proceed?",
                                        "Proceed",
                                        "Cancel"))
        {
            if (Directory.Exists(k_ReadmeSourceDirectory))
                AssetDatabase.DeleteAsset(k_ReadmeSourceDirectory);
            else
                Debug.LogWarning($"Could not find the Readme folder at {k_ReadmeSourceDirectory}");

            var readmeAsset = SelectReadme();
            if (readmeAsset != null)
            {
                var path = AssetDatabase.GetAssetPath(readmeAsset);
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.Refresh();
        }
    }

    public override VisualElement CreateInspectorGUI()
    {
        var readme = (Readme)target;

        VisualElement root = new();
        root.styleSheets.Add(readme.commonStyle);
        root.styleSheets.Add(EditorGUIUtility.isProSkin ? readme.darkStyle : readme.lightStyle);

        VisualElement ChainWithClass(VisualElement created, string className)
        {
            created.AddToClassList(className);
            return created;
        }

        //Header
        VisualElement title = new();
        title.AddToClassList("title");
        title.Add(ChainWithClass(new Image { image = readme.icon }, "title__icon"));
        title.Add(ChainWithClass(new Label(readme.title), "title__text"));
        root.Add(title);

        //Content
        foreach (var section in readme.sections)
        {
            VisualElement part = new();
            part.AddToClassList("section");

            if (!string.IsNullOrEmpty(section.heading))
                part.Add(ChainWithClass(new Label(section.heading), "section__header"));

            if (!string.IsNullOrEmpty(section.text))
                part.Add(ChainWithClass(new Label(section.text), "section__body"));

            if (!string.IsNullOrEmpty(section.linkText))
            {
                var link = ChainWithClass(new Label(section.linkText), "section__link");
                link.RegisterCallback<ClickEvent, string>((_, url) => Application.OpenURL(url), section.url);
                part.Add(link);
            }

            root.Add(part);
        }

        var button = new Button(RemoveTutorial) { text = "Remove Readme Assets" };
        button.AddToClassList("remove-readme-button");
        root.Add(button);

        return root;
    }
}
