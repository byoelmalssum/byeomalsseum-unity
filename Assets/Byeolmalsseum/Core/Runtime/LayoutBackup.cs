using System;
using System.Collections.Generic;
using UnityEngine;

namespace Byeolmalsseum
{
    /// <summary>획 배치 백업 파일. 자모/음절 그룹의 ID별 위치·회전·크기만 저장.</summary>
    public class LayoutBackup : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string id;
            public bool isGroup;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }

        public string word;
        public string savedAt;
        public List<Entry> entries = new List<Entry>();
    }
}
