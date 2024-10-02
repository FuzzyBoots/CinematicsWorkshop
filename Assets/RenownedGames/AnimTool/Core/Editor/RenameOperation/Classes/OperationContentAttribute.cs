/* ================================================================
   ----------------------------------------------------------------
   Project   :   AnimTool
   Publisher :   Renowned Games
   Developer :   Tamerlan Shakirov
   ----------------------------------------------------------------
   Copyright 2022 Renowned Games All rights reserved.
   ================================================================ */

using System;

namespace RenownedGames.AnimTool
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class OperationContentAttribute : Attribute
    {
        public readonly string label;
        public readonly string path;

        public OperationContentAttribute(string label)
        {
            this.label = label;
            this.path = string.Format("Custom Operations/{0}", label);
        }

        public OperationContentAttribute(string label, string path) : this(label)
        {
            this.path = path;
        }
    }
}