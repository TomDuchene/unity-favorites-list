﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

#if UNITY_5_4_OR_NEWER
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
#endif

using BrokenVector.FavoritesList.Utils;

namespace BrokenVector.FavoritesList
{
    public class MainWindow : EditorWindow
    {
        // Constants
        private const float ICON_SIZE = 18;
        private const float CLEAR_BUTTON_WIDTH = 60f;
        private const float REORDER_BUTTON_WIDTH = 50f;
        private const float PING_BUTTON_WIDTH = 19f;
        private const float ARROW_BUTTON_WIDTH = 20f;
        private const float BUTTON_HEIGHT = 19f;

        // Statics
        private static Vector2 ICON_SPACE;
        private static float BORDER_SPACE;
        internal static Texture DEFAULT_ICON;
        private static Texture CLOSE_ICON;

        // Fields
        private static ListData list;

        // Utility
        private static bool reorderMode = false;
        private int framesBeforeProjectWindowUnlock = -1;

        #region Editor Window
        [MenuItem(Constants.WINDOW_PATH), MenuItem(Constants.WINDOW_PATH_ALT)]
        private static void ShowWindow()
        {
            LoadResources();

            //var window = GetWindow(typeof(MainWindow)); // refocus an already opened window
            var window = CreateInstance<MainWindow>(); // allows creating multiple windows

#if UNITY_5_4_OR_NEWER
            
            window.titleContent = new GUIContent(Constants.ASSET_NAME, EditorGUIUtility.IconContent("d_Favorite Icon").image);
#else
            window.title = Constants.ASSET_NAME;
#endif

            window.minSize = new Vector2(100, 100);
            window.position = Globals.Prefs.Get("position", new Rect(50, 50, 250, 375));

            window.Show();
        }

        private static void LoadResources()
        {
            if (list == null)
            {
                list = ListData.LoadList();
            }

            if (DEFAULT_ICON == null)
            {
                BORDER_SPACE = ICON_SPACE.x - ICON_SIZE * 2 - 20; // 20 because of scrollbar
                DEFAULT_ICON = EditorGUIUtility.IconContent("cs Script Icon").image;
                CLOSE_ICON = EditorGUIUtility.FindTexture("winbtn_mac_close_a");
                ICON_SPACE = GUIStyle.none.CalcSize(new GUIContent(DEFAULT_ICON));
            }
        }
        #endregion

        #region Unity Events
        // opening the window / Unity
        private void OnEnable()
        {
            Initialize();
        }

        // called when closing the window
        private void OnDisable()
        {
            CleanUp();
        }

        // called when closing Unity
        private void OnDestroy()
        {
            CleanUp();
        }

        private void Update()
        {
            if (framesBeforeProjectWindowUnlock > -1)
            {
                if(framesBeforeProjectWindowUnlock == 0)
                {
                    ProjectWindowToolkit.LockProjectWindow(false);
                }
                framesBeforeProjectWindowUnlock--;
            }
        }
        #endregion

        #region Instance Lifecycle
        private void Initialize()
        {
            LoadOrCreateData();
        }

        private void LoadOrCreateData()
        {
            hideFlags = HideFlags.HideAndDontSave;
        }

        private void CleanUp()
        {
            Globals.Prefs.Set("position", this.position);

            // nothing to clean up here
        }
        #endregion

        #region UI
        private string searchText = "";
        private Vector2 scrollPos = Vector2.zero;
        private void OnGUI()
        {
            LoadResources();

            searchText = SearchUtils.BeginSearchbar(this, searchText);
            if (SearchUtils.Button(new GUIContent("Clear All"), GUILayout.MaxWidth(CLEAR_BUTTON_WIDTH)))
            {
                if(EditorUtility.DisplayDialog("Warning", "Do you really want to clear the whole list?", "Yes", "No")){
                    list.Clear();
                }

                Repaint();
            }
            if (SearchUtils.Button(new GUIContent("Reorder"), GUILayout.MaxWidth(CLEAR_BUTTON_WIDTH)))
            {
                reorderMode = !reorderMode;

                Repaint();
            }
            SearchUtils.EndSearchbar();

            using (var scrollView = new GUILayout.ScrollViewScope(scrollPos, false, false))
            {
                scrollPos = scrollView.scrollPosition;

                for (int i = list.References.Count - 1; i >= 0; i--)
                {
                    var reference = list.References[i];

                    if (SearchUtils.IsSearched(reference, searchText))
                        if (DrawElement(reference))
                            list.RemoveReference(reference);

                }

                list.References.RemoveAll(reference => reference == null);
            }

            DetectDragNDrop();
        }

        public void UnlockProjectWindow()
        {
            framesBeforeProjectWindowUnlock = 1;
        }

        // returns true if the element should be removed
        private bool DrawElement(ObjectReference reference)
        {
            if (reference == null)
                return true;

            var drawData = reference.GetProvidedData();
            var sceneData = drawData.SceneData;

            float inspectorWidth = EditorGUIUtility.currentViewWidth;
            float buttonWidth = inspectorWidth + BORDER_SPACE;

            buttonWidth = sceneData.IsScene ? (buttonWidth / 3) - 2.5f : buttonWidth;
            buttonWidth -= PING_BUTTON_WIDTH + 1.0f; // Ping button
            if (reorderMode)
            {
                buttonWidth -= 2 * (ARROW_BUTTON_WIDTH + 3.0f); // Reorder buttons
            }
            buttonWidth -= 8; // Scrollbar

            GUILayout.BeginHorizontal();

#if UNITY_5_4_OR_NEWER
            GUILayout.Label(drawData.Icon, GUILayout.Width(ICON_SIZE), GUILayout.Height(ICON_SIZE), GUILayout.MaxWidth(ICON_SIZE), GUILayout.MaxHeight(ICON_SIZE));
#else
            buttonWidth += ICON_SIZE + 10;
#endif

            GUIContent pingContent = new GUIContent(EditorGUIUtility.IconContent("Search On Icon").image, reference.GetPath());
            if (GUILayout.Button(pingContent, GUILayout.Width(PING_BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
            {
                reference.UpdateCachedData();
                reference.Ping();
            }

            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.alignment = TextAnchor.MiddleLeft;
            GUIContent contentText = new GUIContent(drawData.Name, reference.GetTypeString());

            if (GUILayout.Button(contentText, style, GUILayout.MaxWidth(buttonWidth), GUILayout.Width(buttonWidth), GUILayout.ExpandWidth(false)))
            {
                reference.UpdateCachedData();
                reference.Select();
            }

            if (reorderMode)
            {
                EditorGUI.BeginDisabledGroup(list.References.IndexOf(reference) == list.References.Count - 1);
                if (GUILayout.Button("↑", GUILayout.Width(ARROW_BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
                {
                    list.MoveReferenceUp(reference);
                }
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(list.References.IndexOf(reference) == 0);
                if (GUILayout.Button("↓", GUILayout.Width(ARROW_BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
                {
                    list.MoveReferenceDown(reference);
                }
                EditorGUI.EndDisabledGroup();
            }

#if UNITY_5_4_OR_NEWER
                if (sceneData.IsScene)
            {
                var scene = sceneData.Scene;
                // Playmode
                if (Application.isPlaying)
                {
                    if (GUILayout.Button("Load", GUILayout.MaxWidth(buttonWidth * 2 + 2.5f), GUILayout.Width(buttonWidth * 2 + 2.5f), GUILayout.ExpandWidth(false)))
                    {
                        SceneManager.LoadScene(AssetDatabase.GetAssetOrScenePath(scene));
                    }
                }
                else
                {
                    if (GUILayout.Button("Load", GUILayout.MaxWidth(buttonWidth), GUILayout.Width(buttonWidth), GUILayout.ExpandWidth(false)))
                    {
                        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                            EditorSceneManager.OpenScene(AssetDatabase.GetAssetOrScenePath(scene), OpenSceneMode.Single);
                    }

                    if (GUILayout.Button("Play", GUILayout.MaxWidth(buttonWidth), GUILayout.Width(buttonWidth), GUILayout.ExpandWidth(false)))
                    {
                        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                            EditorSceneManager.OpenScene(AssetDatabase.GetAssetOrScenePath(scene), OpenSceneMode.Single);
                        EditorApplication.isPlaying = true;
                    }
                }
            }
#endif

            var alignmentBckp = GUIStyle.none.alignment;
            GUIStyle.none.alignment = TextAnchor.LowerCenter;
            if (GUILayout.Button(CLOSE_ICON, GUIStyle.none, GUILayout.Width(ICON_SIZE), GUILayout.Height(ICON_SIZE)))
                return true;

            GUIStyle.none.alignment = alignmentBckp;
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            return false;
        }

        private void DetectDragNDrop()
        {
            var eventType = Event.current.type;
            if (eventType == EventType.DragUpdated || eventType == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (eventType == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    if (DragAndDrop.objectReferences.Length > 0)
                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            var reference = new ObjectReference(obj);
                            list.AddReference(reference);
                        }
                }

                Event.current.Use();
            }
        }
        #endregion

    }
}
