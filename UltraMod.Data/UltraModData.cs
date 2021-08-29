﻿using UnityEngine;

namespace UltraMod.Data
{
    [CreateAssetMenu(fileName = "ModData", menuName = "UltraMod/ModData")]
    public class UltraModData : ScriptableObject
    {
        public string ModName;
        public string Author;

        [TextArea]
        public string ModDesc;
    }
}