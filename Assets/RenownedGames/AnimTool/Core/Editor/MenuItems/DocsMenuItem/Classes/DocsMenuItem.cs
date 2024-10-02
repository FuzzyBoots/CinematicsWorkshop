/* ================================================================
   ----------------------------------------------------------------
   Project   :   AnimTool
   Publisher :   Renowned Games
   Developer :   Tamerlan Shakirov
   ----------------------------------------------------------------
   Copyright 2022 Renowned Games All rights reserved.
   ================================================================ */

using UnityEditor;

namespace RenownedGames.AnimTool
{
    public static class DocsMenuItem
    {
        [MenuItem("Renowned Games/AnimTool/Documentation", false, 51)]
        public static void Open()
        {
            Help.BrowseURL("https://renownedgames.gitbook.io/animtool/");
        } 
    }
}
