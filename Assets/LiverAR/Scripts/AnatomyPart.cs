using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiverAR.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AnatomyPart : MonoBehaviour
    {
        [SerializeField] string structureId = "unassigned";
        [SerializeField] string displayName = "Unassigned Anatomy";
        [SerializeField] AnatomyCategory category = AnatomyCategory.Other;
        [SerializeField] Color defaultColor = Color.white;
        [SerializeField] Renderer[] renderers = Array.Empty<Renderer>();

        MaterialPropertyBlock propertyBlock;
        AnatomySelectionOutline selectionOutline;
        readonly Dictionary<Material, int> originalRenderQueues = new Dictionary<Material, int>();
        Collider[] colliders = Array.Empty<Collider>();
        float opacity = 1f;
        bool isVisible = true;
        bool isSelected;

        public string StructureId => structureId;
        public string DisplayName => displayName;
        public AnatomyCategory Category => category;
        public Color DefaultColor => defaultColor;
        public float Opacity => opacity;
        public bool IsVisible => isVisible;
        public bool IsSelected => isSelected;
        public Renderer[] Renderers => renderers;
        public Transform ModelRoot
        {
            get
            {
                var current = transform;
                while (current != null)
                {
                    if (current.name.StartsWith("LiverModelRoot", StringComparison.Ordinal))
                        return current;
                    current = current.parent;
                }
                return transform.root;
            }
        }

        void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            CacheReferences();
            selectionOutline = GetComponent<AnatomySelectionOutline>() ?? gameObject.AddComponent<AnatomySelectionOutline>();
            ApplyAppearance();
        }

        void Reset()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        public void Configure(string id, string name, AnatomyCategory partCategory, Color color, Renderer[] rendererReferences)
        {
            structureId = string.IsNullOrWhiteSpace(id) ? "unassigned" : id;
            displayName = string.IsNullOrWhiteSpace(name) ? structureId : name;
            category = partCategory;
            defaultColor = color;
            renderers = rendererReferences ?? Array.Empty<Renderer>();
            CacheReferences();
            ApplyAppearance();
        }

        public void SetVisible(bool visible)
        {
            isVisible = visible;
            ApplyRendererVisibility();
        }

        void ApplyRendererVisibility()
        {
            var renderVisible = isVisible && opacity > 0.001f;
            foreach (var partRenderer in renderers)
            {
                if (partRenderer != null)
                    partRenderer.enabled = renderVisible;
            }

            foreach (var partCollider in colliders)
            {
                if (partCollider != null)
                    partCollider.enabled = renderVisible;
            }

            UpdateSelectionOutline();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (selectionOutline == null)
                selectionOutline = GetComponent<AnatomySelectionOutline>() ?? gameObject.AddComponent<AnatomySelectionOutline>();
            UpdateSelectionOutline();
        }

        public void SetColor(Color color)
        {
            defaultColor = color;
            ApplyAppearance();
        }

        public void SetOpacity(float value)
        {
            opacity = TransparencyController.ClampOpacity(value);
            ApplyAppearance();
            ApplyRendererVisibility();
        }

        public void ResetOpacity()
        {
            opacity = 1f;
            ApplyAppearance();
            ApplyRendererVisibility();
        }

        public void ResetAppearance()
        {
            opacity = 1f;
            isSelected = false;
            ApplyAppearance();
            SetVisible(true);
        }

        public void CacheReferences()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(true);

            colliders = GetComponentsInChildren<Collider>(true);
        }

        void ApplyAppearance()
        {
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();

            var color = defaultColor;
            color.a = opacity;

            foreach (var partRenderer in renderers)
            {
                if (partRenderer == null)
                    continue;

                partRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", color);
                propertyBlock.SetColor("_Color", color);
                partRenderer.SetPropertyBlock(propertyBlock);
                ConfigureMaterialTransparency(partRenderer, opacity);
            }
        }

        void UpdateSelectionOutline()
        {
            if (selectionOutline != null)
                selectionOutline.SetVisible(isSelected && isVisible && opacity > 0.001f);
        }

        void ConfigureMaterialTransparency(Renderer partRenderer, float alpha)
        {
            var materials = partRenderer.materials;
            foreach (var material in materials)
            {
                if (material == null)
                    continue;

                EnsureRuntimeOpacityShader(material, alpha);

                if (!originalRenderQueues.ContainsKey(material))
                    originalRenderQueues[material] = material.renderQueue;

                var materialColor = defaultColor;
                materialColor.a = alpha;
                material.SetColor("_BaseColor", materialColor);
                material.SetColor("_Color", materialColor);

                if (alpha >= 0.99f)
                {
                    material.SetFloat("_Surface", 0f);
                    material.SetFloat("_Blend", 0f);
                    material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                    material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetFloat("_ZWrite", 1f);
                    material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.SetOverrideTag("RenderType", "Opaque");
                    material.SetShaderPassEnabled("ShadowCaster", true);
                    material.renderQueue = originalRenderQueues[material];
                    continue;
                }

                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetShaderPassEnabled("ShadowCaster", false);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }

        static void EnsureRuntimeOpacityShader(Material material, float alpha)
        {
            if (alpha >= 0.99f || (material.shader != null && material.shader.name == "Universal Render Pipeline/Lit"))
                return;

            var transparentShader = Shader.Find("Universal Render Pipeline/Lit");
            if (transparentShader == null)
                return;

            // glTFast materials are generated as opaque Shader Graph materials.
            // URP Lit provides a runtime blend mode that responds to decimal alpha.
            var mainTexture = material.mainTexture;
            material.shader = transparentShader;
            if (mainTexture != null)
                material.mainTexture = mainTexture;
        }
    }
}

namespace LiverAR.Runtime
{
    [DisallowMultipleComponent]
    sealed class AnatomySelectionOutline : MonoBehaviour
    {
        static Material sharedMaterial;
        readonly List<GameObject> outlines = new List<GameObject>();
        bool initialized;

        public void SetVisible(bool visible)
        {
            EnsureInitialized();
            foreach (var outline in outlines)
            {
                if (outline != null)
                    outline.SetActive(visible);
            }
        }

        void EnsureInitialized()
        {
            if (initialized)
                return;

            initialized = true;
            var material = GetSharedMaterial();
            if (material == null)
                return;

            foreach (var source in GetComponentsInChildren<MeshFilter>(true))
            {
                if (source.sharedMesh == null || source.transform.name == "Selection Outline")
                    continue;

                var outline = new GameObject("Selection Outline");
                // An outline is owned by its part, preventing detached contours.
                outline.transform.SetParent(transform, true);
                outline.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                outline.transform.localScale = source.transform.lossyScale;
                outline.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                var outlineRenderer = outline.AddComponent<MeshRenderer>();
                outlineRenderer.sharedMaterial = material;

                var properties = new MaterialPropertyBlock();
                properties.SetVector("_OutlineCenter", source.sharedMesh.bounds.center);
                properties.SetFloat("_OutlineScale", 1.015f);
                outlineRenderer.SetPropertyBlock(properties);
                outline.SetActive(false);
                outlines.Add(outline);
            }
        }

        Material GetSharedMaterial()
        {
            if (sharedMaterial != null)
                return sharedMaterial;

            // A Resources material gives Unity an explicit Android build dependency.
            // Runtime copies of anatomy materials could use the wrong culling mode,
            // leaving the selection mesh indistinguishable from the coloured part.
            sharedMaterial = Resources.Load<Material>("AnatomySelectionOutline");
            if (sharedMaterial == null)
                Debug.LogError("Missing Resources/AnatomySelectionOutline material. Selection outlines cannot render.");

            return sharedMaterial;
        }
    }
}
