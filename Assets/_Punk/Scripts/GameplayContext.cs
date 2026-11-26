using Unity.Cinemachine;
using UnityEngine;

namespace PunkGame
{
    [DisallowMultipleComponent]
    public sealed class GameplayContext : MonoBehaviour
    {
        [SerializeField] private CinemachineBrain _cameraBrain;
        [SerializeField] private RectTransform _screenSpaceRoot;

        public CinemachineBrain CameraBrain => _cameraBrain;
        public Camera OutputCamera => _cameraBrain != null ? _cameraBrain.OutputCamera : null;
        public RectTransform ScreenSpaceRoot => _screenSpaceRoot;
    }
}
