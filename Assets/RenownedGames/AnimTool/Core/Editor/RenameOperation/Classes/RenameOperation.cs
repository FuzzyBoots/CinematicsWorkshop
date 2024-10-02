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
    [Serializable]
    public abstract class RenameOperation : ICloneable
    {
        /// <summary>
        /// Execute operation.
        /// </summary>
        public abstract void Execute(ref string source);

        #region [IClonable Implementation]
        public object Clone()
        {
            return MemberwiseClone();
        }
        #endregion
    }
}