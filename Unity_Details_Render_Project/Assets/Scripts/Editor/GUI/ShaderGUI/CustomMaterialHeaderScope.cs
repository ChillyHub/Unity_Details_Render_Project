using System;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

public struct CustomMaterialHeaderScope : IDisposable
{
    const string k_KeyPrefix = "CoreRP:Material:UI_State:";

    /// <summary>Indicates whether the header is expanded or not. Is true if the header is expanded, false otherwise.</summary>
    public readonly bool expanded;

    bool spaceAtEnd;
#if !UNITY_2020_1_OR_NEWER
        int oldIndentLevel;
#endif

    /// <summary>
    /// Creates a material header scope to display the foldout in the material UI.
    /// </summary>
    /// <param name="title">GUI Content of the header.</param>
    /// <param name="bitExpanded">Bit index which specifies the state of the header (whether it is open or collapsed) inside Editor Prefs.</param>
    /// <param name="materialEditor">The current material editor.</param>
    /// <param name="spaceAtEnd">Set this to true to make the block include space at the bottom of its UI. Set to false to not include any space.</param>
    /// <param name="subHeader">Set to true to make this into a sub-header. This affects the style of the header. Set to false to make this use the standard style.</param>
    /// <param name="defaultExpandedState">The default state if the header is not present</param>
    /// <param name="documentationURL">[optional] Documentation page</param>
    public CustomMaterialHeaderScope(GUIContent title, uint bitExpanded, MaterialEditor materialEditor,
        bool spaceAtEnd = true, bool isToggle = false, bool subHeader = false,
        uint defaultExpandedState = uint.MaxValue, string documentationURL = "")
    {
        if (title == null)
            throw new ArgumentNullException(nameof(title));

        bool beforeExpanded = IsAreaExpanded(materialEditor, bitExpanded, defaultExpandedState);
        bool isActive = IsAreaActive(materialEditor, bitExpanded, defaultExpandedState);
        bool beforeActive = isActive;

#if !UNITY_2020_1_OR_NEWER
            oldIndentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel =
 subHeader ? 1 : 0; //fix for preset in 2019.3 (preset are one more indentation depth in material)
#endif
        
        this.spaceAtEnd = spaceAtEnd;
        if (!subHeader)
            CoreEditorUtils.DrawSplitter();
        GUILayout.BeginVertical();

        bool saveChangeState = GUI.changed;
        expanded = isToggle
            ? DrawToggleFoldout(title, beforeExpanded, ref isActive, null, null, null, documentationURL)
            : subHeader
                ? CoreEditorUtils.DrawSubHeaderFoldout(title, beforeExpanded, isBoxed: false)
                : CoreEditorUtils.DrawHeaderFoldout(title, beforeExpanded, documentationURL: documentationURL);
        if (expanded ^ beforeExpanded)
        {
            SetIsAreaExpanded(materialEditor, (uint)bitExpanded, expanded);
            saveChangeState = true;
        }

        if (isActive ^ beforeActive)
        {
            SetIsAreaActive(materialEditor, (uint)bitExpanded, isActive);
            saveChangeState = true;
        }

        GUI.changed = saveChangeState;

        if (expanded)
            ++EditorGUI.indentLevel;
    }

    /// <summary>
    /// Creates a material header scope to display the foldout in the material UI.
    /// </summary>
    /// <param name="title">Title of the header.</param>
    /// <param name="bitExpanded">Bit index which specifies the state of the header (whether it is open or collapsed) inside Editor Prefs.</param>
    /// <param name="materialEditor">The current material editor.</param>
    /// <param name="spaceAtEnd">Set this to true to make the block include space at the bottom of its UI. Set to false to not include any space.</param>
    /// <param name="subHeader">Set to true to make this into a sub-header. This affects the style of the header. Set to false to make this use the standard style.</param>
    public CustomMaterialHeaderScope(string title, uint bitExpanded, MaterialEditor materialEditor, bool spaceAtEnd = true,
        bool subHeader = false)
        : this(EditorGUIUtility.TrTextContent(title, string.Empty), bitExpanded, materialEditor, spaceAtEnd, subHeader)
    {
    }

    /// <summary>
    /// Creates a material header scope to display the foldout in the material UI.
    /// </summary>
    /// <param name="rect">Rect of the header.</param>
    /// <param name="title">GUI Content of the header.</param>
    /// <param name="bitExpanded">Bit index which specifies the state of the header (whether it is open or collapsed) inside Editor Prefs.</param>
    /// <param name="materialEditor">The current material editor.</param>
    /// <param name="spaceAtEnd">Set this to true to make the block include space at the bottom of its UI. Set to false to not include any space.</param>
    /// <param name="isToggle">Set this to true to make the block include toggle at the front of its UI. Set to false to not include toggle.</param>
    /// <param name="subHeader">Set to true to make this into a sub-header. This affects the style of the header. Set to false to make this use the standard style.</param>
    /// <param name="defaultExpandedState">The default state if the header is not present</param>
    /// <param name="documentationURL">[optional] Documentation page</param>
    public CustomMaterialHeaderScope(Rect rect, GUIContent title, uint bitExpanded, MaterialEditor materialEditor,
        bool spaceAtEnd = true, bool isToggle = false, bool subHeader = false,
        uint defaultExpandedState = uint.MaxValue, string documentationURL = "")
    {
        if (title == null)
            throw new ArgumentNullException(nameof(title));

        bool beforeExpanded = IsAreaExpanded(materialEditor, bitExpanded, defaultExpandedState);
        bool isActive = IsAreaActive(materialEditor, bitExpanded, defaultExpandedState);
        bool beforeActive = isActive;

#if !UNITY_2020_1_OR_NEWER
            oldIndentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel =
 subHeader ? 1 : 0; //fix for preset in 2019.3 (preset are one more indentation depth in material)
#endif

        this.spaceAtEnd = spaceAtEnd;
        if (!subHeader)
        {
            DrawSplitter(rect);
            rect.yMin += 1.0f;
        }

        GUILayout.BeginVertical();

        bool saveChangeState = GUI.changed;
        expanded = isToggle
            ? DrawToggleFoldout(rect, title, beforeExpanded, ref isActive, null, null, null,
                documentationURL)
            : subHeader
                ? CoreEditorUtils.DrawSubHeaderFoldout(title, beforeExpanded, isBoxed: false)
                : DrawHeaderFoldout(rect, title, beforeExpanded, documentationURL: documentationURL);
        if (expanded ^ beforeExpanded)
        {
            SetIsAreaExpanded(materialEditor, (uint)bitExpanded, expanded);
            saveChangeState = true;
        }

        if (isActive ^ beforeActive)
        {
            SetIsAreaActive(materialEditor, (uint)bitExpanded, isActive);
            saveChangeState = true;
        }

        GUI.changed = saveChangeState;

        if (expanded)
            ++EditorGUI.indentLevel;
    }

    /// <summary>
    /// Creates a material header scope to display the foldout in the material UI.
    /// </summary>
    /// <param name="rect">Rect of the header.</param>
    /// <param name="title">Title of the header.</param>
    /// <param name="bitExpanded">Bit index which specifies the state of the header (whether it is open or collapsed) inside Editor Prefs.</param>
    /// <param name="materialEditor">The current material editor.</param>
    /// <param name="spaceAtEnd">Set this to true to make the block include space at the bottom of its UI. Set to false to not include any space.</param>
    /// <param name="isToggle">Set this to true to make the block include toggle at the front of its UI. Set to false to not include toggle.</param>
    /// <param name="subHeader">Set to true to make this into a sub-header. This affects the style of the header. Set to false to make this use the standard style.</param>
    public CustomMaterialHeaderScope(Rect rect, string title, uint bitExpanded, MaterialEditor materialEditor,
        bool spaceAtEnd = true, bool isToggle = false, bool subHeader = false)
        : this(rect, EditorGUIUtility.TrTextContent(title, string.Empty), bitExpanded, materialEditor, spaceAtEnd,
            isToggle, subHeader)
    {
    }

    /// <summary>Disposes of the material scope header and cleans up any resources it used.</summary>
    void IDisposable.Dispose()
    {
        if (expanded)
        {
            if (spaceAtEnd && (Event.current.type == EventType.Repaint || Event.current.type == EventType.Layout))
                EditorGUILayout.Space();
            --EditorGUI.indentLevel;
        }

#if !UNITY_2020_1_OR_NEWER
            EditorGUI.indentLevel = oldIndentLevel;
#endif
        GUILayout.EndVertical();
    }

    public static bool IsAreaExpanded(MaterialEditor editor, uint mask, uint defaultExpandedState = uint.MaxValue)
    {
        string key = GetEditorPrefsKey(editor);

        if (EditorPrefs.HasKey(key))
        {
            uint state = (uint)EditorPrefs.GetInt(key);
            return (state & mask) > 0;
        }

        EditorPrefs.SetInt(key, (int)defaultExpandedState);
        return (defaultExpandedState & mask) > 0;
    }

    public static void SetIsAreaExpanded(MaterialEditor editor, uint mask, bool value)
    {
        string key = GetEditorPrefsKey(editor);

        uint state = (uint)EditorPrefs.GetInt(key);

        if (value)
        {
            state |= mask;
        }
        else
        {
            mask = ~mask;
            state &= mask;
        }

        EditorPrefs.SetInt(key, (int)state);
    }


    public static bool IsAreaActive(MaterialEditor editor, uint mask, uint defaultExpandedState = uint.MaxValue)
    {
        string key = GetEditorActiveKey(editor);

        if (EditorPrefs.HasKey(key))
        {
            uint state = (uint)EditorPrefs.GetInt(key);
            return (state & mask) > 0;
        }

        EditorPrefs.SetInt(key, (int)defaultExpandedState);
        return (defaultExpandedState & mask) > 0;
    }

    /// <summary>
    /// Sets if the area is expanded <see cref="MaterialEditor"/>
    /// </summary>
    /// <param name="editor"><see cref="MaterialEditor"/></param>
    /// <param name="mask">The mask identifying the area to check the state</param>
    public static void SetIsAreaActive(MaterialEditor editor, uint mask, bool value)
    {
        string key = GetEditorActiveKey(editor);

        uint state = (uint)EditorPrefs.GetInt(key);

        if (value)
        {
            state |= mask;
        }
        else
        {
            mask = ~mask;
            state &= mask;
        }

        EditorPrefs.SetInt(key, (int)state);
    }

    static string GetEditorPrefsKey(MaterialEditor editor)
    {
        return k_KeyPrefix + (editor.target as Material).shader.name;
    }

    static string GetEditorActiveKey(MaterialEditor editor)
    {
        return k_KeyPrefix + "Mat_" + (editor.target as Material).name;
    }

    private static bool DrawToggleFoldout(GUIContent title, bool isExpanded, ref bool isActive,
        Action<Vector2> contextAction, Func<bool> hasMoreOptions, Action toggleMoreOptions, string documentationURL)
    {
        var backgroundRect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(1f, 17f));

        var labelRect = backgroundRect;
        labelRect.xMin += 32f;
        labelRect.xMax -= 20f + 16 + 5;

        var foldoutRect = backgroundRect;
        foldoutRect.y += 1f;
        foldoutRect.width = 13f;
        foldoutRect.height = 13f;

        var toggleRect = backgroundRect;
        toggleRect.x += 16f;
        toggleRect.y += 2f;
        toggleRect.width = 13f;
        toggleRect.height = 13f;

        // Background rect should be full-width
        backgroundRect.xMin = 0f;
        backgroundRect.width += 4f;

        // Background
        float backgroundTint = EditorGUIUtility.isProSkin ? 0.1f : 1f;
        EditorGUI.DrawRect(backgroundRect, new Color(backgroundTint, backgroundTint, backgroundTint, 0.2f));

        // Title
        using (new EditorGUI.DisabledScope(!isActive))
            EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);

        // Foldout
        isExpanded = UnityEngine.GUI.Toggle(foldoutRect, isExpanded, GUIContent.none, EditorStyles.foldout);

        // Active checkbox
        isActive = UnityEngine.GUI.Toggle(toggleRect, isActive, GUIContent.none, CoreEditorStyles.smallTickbox);

        // Context menu
        var contextMenuIcon = CoreEditorStyles.contextMenuIcon.image;
        var contextMenuRect = new Rect(labelRect.xMax + 3f + 16 + 5, labelRect.y + 1f, 16, 16);

        if (contextAction == null && hasMoreOptions != null)
        {
            // If no contextual menu add one for the additional properties.
            contextAction = pos => OnContextClick(pos, hasMoreOptions, toggleMoreOptions);
        }

        if (contextAction != null)
        {
            if (UnityEngine.GUI.Button(contextMenuRect, CoreEditorStyles.contextMenuIcon,
                    CoreEditorStyles.contextMenuStyle))
                contextAction(new Vector2(contextMenuRect.x, contextMenuRect.yMax));
        }

        // Documentation button
        ShowHelpButton(contextMenuRect, documentationURL, title);

        // Handle events
        var e = Event.current;

        if (e.type == EventType.MouseDown)
        {
            if (backgroundRect.Contains(e.mousePosition))
            {
                // Left click: Expand/Collapse
                if (e.button == 0)
                    isExpanded = !isExpanded;
                // Right click: Context menu
                else if (contextAction != null)
                    contextAction(e.mousePosition);

                e.Use();
            }
        }

        return isExpanded;
    }

    public static bool DrawToggleFoldout(Rect backgroundRect, GUIContent title, bool isExpanded, ref bool isActive,
        Action<Vector2> contextAction, Func<bool> hasMoreOptions, Action toggleMoreOptions, string documentationURL)
    {
        var labelRect = backgroundRect;
        labelRect.xMin += 32f;
        labelRect.xMax -= 20f + 16 + 5;

        var foldoutRect = backgroundRect;
        foldoutRect.y += 1f;
        foldoutRect.width = 13f;
        foldoutRect.height = 13f;

        var toggleRect = backgroundRect;
        toggleRect.x += 16f;
        toggleRect.y += 2f;
        toggleRect.width = 13f;
        toggleRect.height = 13f;

        // Background rect should be full-width
        backgroundRect.xMin = 0f;
        backgroundRect.width += 4f;

        // Background
        float backgroundTint = EditorGUIUtility.isProSkin ? 0.1f : 1f;
        EditorGUI.DrawRect(backgroundRect, new Color(backgroundTint, backgroundTint, backgroundTint, 0.2f));

        // Title
        using (new EditorGUI.DisabledScope(!isActive))
            EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);

        // Foldout
        isExpanded = UnityEngine.GUI.Toggle(foldoutRect, isExpanded, GUIContent.none, EditorStyles.foldout);

        // Active checkbox
        isActive = UnityEngine.GUI.Toggle(toggleRect, isActive, GUIContent.none, CoreEditorStyles.smallTickbox);

        // Context menu
        var contextMenuIcon = CoreEditorStyles.contextMenuIcon.image;
        var contextMenuRect = new Rect(labelRect.xMax + 3f + 16 + 5, labelRect.y + 1f, 16, 16);

        if (contextAction == null && hasMoreOptions != null)
        {
            // If no contextual menu add one for the additional properties.
            contextAction = pos => OnContextClick(pos, hasMoreOptions, toggleMoreOptions);
        }

        if (contextAction != null)
        {
            if (UnityEngine.GUI.Button(contextMenuRect, CoreEditorStyles.contextMenuIcon,
                    CoreEditorStyles.contextMenuStyle))
                contextAction(new Vector2(contextMenuRect.x, contextMenuRect.yMax));
        }

        // Documentation button
        ShowHelpButton(contextMenuRect, documentationURL, title);

        // Handle events
        var e = Event.current;

        if (e.type == EventType.MouseDown)
        {
            if (backgroundRect.Contains(e.mousePosition))
            {
                // Left click: Expand/Collapse
                if (e.button == 0)
                    isExpanded = !isExpanded;
                // Right click: Context menu
                else if (contextAction != null)
                    contextAction(e.mousePosition);

                e.Use();
            }
        }

        return isExpanded;
    }
    
    private static void DrawSplitter(Rect rect, bool isBoxed = false)
    {
        rect.height = 1.0f;
        float xMin = rect.xMin;

        // Splitter rect should be full-width
        rect.xMin = 0f;
        rect.width += 4f;

        if (isBoxed)
        {
            rect.xMin = xMin == 7.0 ? 4.0f : EditorGUIUtility.singleLineHeight;
            rect.width -= 1;
        }

        if (Event.current.type != EventType.Repaint)
            return;

        EditorGUI.DrawRect(rect, !EditorGUIUtility.isProSkin
            ? new Color(0.6f, 0.6f, 0.6f, 1.333f)
            : new Color(0.12f, 0.12f, 0.12f, 1.333f));
    }

    public static bool DrawHeaderFoldout(Rect backgroundRect, GUIContent title, bool state, bool isBoxed = false,
        Func<bool> hasMoreOptions = null, Action toggleMoreOptions = null, string documentationURL = "",
        Action<Vector2> contextAction = null)
    {
        float xMin = backgroundRect.xMin;

        var labelRect = backgroundRect;
        labelRect.xMin += 16f;
        labelRect.xMax -= 20f;

        var foldoutRect = backgroundRect;
        foldoutRect.y += 1f;
        foldoutRect.width = 13f;
        foldoutRect.height = 13f;
        foldoutRect.x = labelRect.xMin + 15 * (EditorGUI.indentLevel - 1); //fix for presset

        // Background rect should be full-width
        backgroundRect.xMin = 0f;
        backgroundRect.width += 4f;

        if (isBoxed)
        {
            labelRect.xMin += 5;
            foldoutRect.xMin += 5;
            backgroundRect.xMin = xMin == 7.0 ? 4.0f : EditorGUIUtility.singleLineHeight;
            backgroundRect.width -= 1;
        }

        // Background
        float backgroundTint = EditorGUIUtility.isProSkin ? 0.1f : 1f;
        EditorGUI.DrawRect(backgroundRect, new Color(backgroundTint, backgroundTint, backgroundTint, 0.2f));

        // Title
        EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);

        // Active checkbox
        state = GUI.Toggle(foldoutRect, state, GUIContent.none, EditorStyles.foldout);

        // Context menu
        var menuIcon = CoreEditorStyles.paneOptionsIcon;
        var menuRect = new Rect(labelRect.xMax + 3f, labelRect.y + 1f, 16, 16);

        // Add context menu for "Additional Properties"
        if (contextAction == null && hasMoreOptions != null)
        {
            contextAction = pos => OnContextClick(pos, hasMoreOptions, toggleMoreOptions);
        }

        if (contextAction != null)
        {
            if (GUI.Button(menuRect, CoreEditorStyles.contextMenuIcon, CoreEditorStyles.contextMenuStyle))
                contextAction(new Vector2(menuRect.x, menuRect.yMax));
        }

        // Documentation button
        ShowHelpButton(menuRect, documentationURL, title);

        var e = Event.current;

        if (e.type == EventType.MouseDown)
        {
            if (backgroundRect.Contains(e.mousePosition))
            {
                if (e.button != 0 && contextAction != null)
                    contextAction(e.mousePosition);
                else if (e.button == 0)
                {
                    state = !state;
                    e.Use();
                }

                e.Use();
            }
        }

        return state;
    }
    
    private static void OnContextClick(Vector2 position, Func<bool> hasMoreOptions, Action toggleMoreOptions)
    {
        var menu = new GenericMenu();

        menu.AddItem(EditorGUIUtility.TrTextContent("Show Additional Properties"), hasMoreOptions.Invoke(), () => toggleMoreOptions.Invoke());
        menu.AddItem(EditorGUIUtility.TrTextContent("Show All Additional Properties..."), false, () => CoreRenderPipelinePreferences.Open());

        menu.DropDown(new Rect(position, Vector2.zero));
    }
    
    private static void ShowHelpButton(Rect contextMenuRect, string documentationURL, GUIContent title)
    {
        if (string.IsNullOrEmpty(documentationURL))
            return;

        var documentationRect = contextMenuRect;
        documentationRect.x -= 16 + 2;

        var documentationIcon = new GUIContent(CoreEditorStyles.iconHelp, $"Open Reference for {title.text}.");

        if (GUI.Button(documentationRect, documentationIcon, CoreEditorStyles.iconHelpStyle))
            Help.BrowseURL(documentationURL);
    }
}