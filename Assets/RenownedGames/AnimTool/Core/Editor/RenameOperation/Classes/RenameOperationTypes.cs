/* ================================================================
   ----------------------------------------------------------------
   Project   :   AnimTool
   Publisher :   Renowned Games
   Developer :   Tamerlan Shakirov
   ----------------------------------------------------------------
   Copyright 2022 Renowned Games All rights reserved.
   ================================================================ */

using System;
using System.Text;
using UnityEngine;

namespace RenownedGames.AnimTool
{
    #region [Case Operations]
    [Serializable]
    [OperationContent("To Upper", "Case/To Upper")]
    public sealed class ToUpperOperation : RenameOperation
    {
        public override void Execute(ref string source)
        {
            source = source.ToUpper();
        }
    }

    [Serializable]
    [OperationContent("To Lower", "Case/To Lower")]
    public sealed class ToLowerOperation : RenameOperation
    {
        public override void Execute(ref string source)
        {
            source = source.ToLower();
        }
    }

    [Serializable]
    [OperationContent("To Title", "Case/To Title")]
    public sealed class ToTitleOperation : RenameOperation
    {
        public override void Execute(ref string source)
        {
            if (!string.IsNullOrEmpty(source))
            {
                source = source.ToLower();
                StringBuilder stBuilder = new StringBuilder(source);
                for (int i = 0; i < stBuilder.Length; i++)
                {
                    if (i == 0)
                    {
                        stBuilder[0] = char.ToUpper(stBuilder[0]);
                    }
                    else
                    {
                        if (!char.IsLetter(stBuilder[i - 1]))
                        {
                            stBuilder[i] = char.ToUpper(stBuilder[i]);
                        }
                    }
                }
                source = stBuilder.ToString();
            }
        }
    }
    #endregion

    #region [Add Operations]
    [Serializable]
    [OperationContent("Add Prefix", "Add/Prefix")]
    public sealed class AddPrefixOperation : RenameOperation
    {
        [SerializeField]
        private string prefix;

        public override void Execute(ref string source)
        {
            source = prefix + source;
        }

        #region [Getter / Setter]
        public string GetPrefix()
        {
            return prefix;
        }

        public void SetPrefix(string value)
        {
            prefix = value;
        }
        #endregion
    }

    [Serializable]
    [OperationContent("Add Suffix", "Add/Suffix")]
    public sealed class AddSuffixOperation : RenameOperation
    {
        [SerializeField]
        private string suffix;

        public override void Execute(ref string source)
        {
            source += suffix;
        }

        #region [Getter / Setter]
        public string GetSuffix()
        {
            return suffix;
        }

        public void SetSuffix(string value)
        {
            suffix = value;
        }
        #endregion
    }
    #endregion

    #region [Edit Operations]
    [Serializable]
    [OperationContent("Replace", "Edit/Replace")]
    public sealed class ReplaceOperation : RenameOperation
    {
        [SerializeField]
        private string oldValue;

        [SerializeField]
        private string newValue;

        public override void Execute(ref string source)
        {
            if (!string.IsNullOrEmpty(oldValue))
            {
                source = source.Replace(oldValue, newValue);
            }
        }

        #region [Getter / Setter]
        public string GetOldValue()
        {
            return oldValue;
        }

        public void SetOldValue(string value)
        {
            oldValue = value;
        }

        public string GetNewValue()
        {
            return newValue;
        }

        public void SetNewValue(string value)
        {
            newValue = value;
        }
        #endregion
    }

    [Serializable]
    [OperationContent("Replace Whitespace", "Edit/Whitespace")]
    public sealed class ReplaceWhiteSpace : RenameOperation
    {
        [SerializeField]
        private string whitespace;

        public override void Execute(ref string source)
        {
            if (!string.IsNullOrEmpty(whitespace))
            {
                source = source.Replace(" ", whitespace);
            }
        }

        #region [Getter / Setter]
        public string GetWhitespace()
        {
            return whitespace;
        }

        public void SetWhitespace(string value)
        {
            whitespace = value;
        }
        #endregion
    }
    #endregion

    #region [Remove Operations]
    [Serializable]
    [OperationContent("Remove Whitespace", "Remove/Whitespace")]
    public sealed class RemoveSpaces : RenameOperation
    {
        public override void Execute(ref string source)
        {
            source = source.Replace(" ", string.Empty);
        }
    }
    #endregion
}