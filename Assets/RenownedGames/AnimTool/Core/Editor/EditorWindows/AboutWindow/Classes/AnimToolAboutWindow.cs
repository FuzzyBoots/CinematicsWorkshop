/* ================================================================
   ----------------------------------------------------------------
   Project   :   AnimTool
   Publisher :   Renowned Games
   Developer :   Tamerlan Shakirov
   ----------------------------------------------------------------
   Copyright 2022 Renowned Games All rights reserved.
   ================================================================ */

using RenownedGames.ExLibEditor;
using RenownedGames.ExLibEditor.Windows;
using UnityEditor;
using UnityEngine;

namespace RenownedGames.AnimTool
{
    public sealed class AnimToolAboutWindow : AboutWindow
    {
        /// <summary>
        /// Implement this method to add project name.
        /// </summary>
        protected override void InitializeProjectName(out string projectName)
        {
            projectName = "AnimTool";
        }

        /// <summary>
        /// Implement this method to add version label.
        /// </summary>
        protected override void InitializeVersion(out string version)
        {
            version = "Version: 1.0.3";
        }

        /// <summary>
        /// Implement this method to add all the people involved in the development.
        /// </summary>
        protected override void InitializeDevelopers(out Developer[] developers)
        {
            developers = new Developer[2]
            {
                new Developer("Publisher: ", "Renowned Games"),
                new Developer("Lead Developer: ", "Tamerlan Shakirov")
            };
        }

        /// <summary>
        /// Implement this method to add logotype.
        /// </summary>
        public override void InitializeLogotype(out Texture2D texture, out float width, out float height)
        {
            texture = EditorResources.Load<Texture2D>("Images/Logotype/AnimTool_420x280.png");
            width = 168.0f;
            height = 112.0f;
        }

        /// <summary>
        /// Implement this method to add copyright.
        /// </summary>
        protected override void InitializeCopyright(out string copyright)
        {
            copyright = "Copyright 2022 Renowned Games All rights reserved.";
        }

        /// <summary>
        /// Implement this method to add publisher link button.
        /// </summary>
        protected override void InitializePublisherLink(out string url)
        {
            url = "https://assetstore.unity.com/publishers/26774";
        }

        [MenuItem("Renowned Games/AnimTool/About", false, 50)]
        public static void Open()
        {
            Open<AnimToolAboutWindow>(new GUIContent("About"), new Vector2(470, 170));
        }
    }
}