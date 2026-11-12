using UnityEngine;

namespace Byeolmalsseum
{
    /// <summary>음절 그룹 (예: '길'). 옮기면 안의 초·중·종성이 한꺼번에 움직임. 백업할 때 ID로 찾음.</summary>
    [DisallowMultipleComponent]
    public class SyllableGroup : MonoBehaviour
    {
        public string id;
        public string syllable;
    }
}
