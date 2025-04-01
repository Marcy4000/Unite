using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShadowController : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private MeshRenderer _shadowMeshRenderer;
    [SerializeField] private LayerMask _renderTarget;
    [SerializeField] private LayerMask _ground;
    [SerializeField] private Transform _rayStart;

    private RenderTexture _shadowTexture;
    private Transform _shadowTransform;

    [SerializeField] private float _targetFPS = 45f;
    private float _timeSinceLastRender;
    private float _renderInterval;

    private void Start()
    {
        _shadowTransform = _shadowMeshRenderer.transform;
        _shadowTransform.parent = null;
        _shadowTexture = new RenderTexture(256, 256, 16); // Set depth buffer to 16 bits

        _camera.targetTexture = _shadowTexture;
        _shadowMeshRenderer.material.SetTexture("_BaseMap", _shadowTexture);

        _camera.cullingMask = _renderTarget;
        _renderInterval = 1f / _targetFPS;

        _timeSinceLastRender = Random.Range(0f, _renderInterval);
    }

    private void Update()
    {
        _timeSinceLastRender += Time.deltaTime;

        if (_timeSinceLastRender >= _renderInterval)
        {
            _camera.Render();
            _timeSinceLastRender = 0f;
        }

        _shadowTransform.position = new Vector3(transform.position.x, _shadowTransform.position.y, transform.position.z);
        _shadowTransform.rotation = transform.rotation;
    }

    private void FixedUpdate()
    {
        if (Physics.Raycast(_rayStart.position, Vector3.down, out RaycastHit hit, 100, _ground))
        {
            _shadowTransform.position = new Vector3(_shadowTransform.position.x, hit.point.y + 0.01f, _shadowTransform.position.z);
        }
    }
}
