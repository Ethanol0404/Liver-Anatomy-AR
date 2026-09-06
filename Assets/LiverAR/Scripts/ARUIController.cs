using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace LiverAR.Runtime
{
    public sealed class ARUIController : MonoBehaviour
    {
        [SerializeField] Text instructionText;
        [SerializeField] Text selectedNameText;
        [SerializeField] Text selectedCategoryText;
        [SerializeField] Text surfaceStatusText;
        [SerializeField] Text informationBodyText;
        [SerializeField] Text modelMessageText;
        [FormerlySerializedAs("transparencySlider")]
        [SerializeField] Slider selectedOpacitySlider;
        [SerializeField] Slider interactionSensitivitySlider;
        [SerializeField] Slider rotationSpeedSlider;
        [SerializeField] Slider scaleSensitivitySlider;
        [SerializeField] Toggle hapticToggle;
        [SerializeField] Toggle cameraBackgroundToggle;
        [SerializeField] Button placeLiverButton;
        [SerializeField] Button virtualSurfaceButton;
        [SerializeField] Button diseaseModelButton;
        [SerializeField] GameObject compactMenuPanel;
        [SerializeField] GameObject segmentationMenuPanel;
        [FormerlySerializedAs("segmentationPanel")]
        [SerializeField] GameObject couinaudSegmentsPanel;
        [SerializeField] GameObject vesselPanel;
        [SerializeField] GameObject tumorPanel;
        [SerializeField] GameObject informationPanel;
        [SerializeField] GameObject compactInformationPanel;
        [SerializeField] GameObject transparencyPanel;
        [SerializeField] Text transparencyTitleText;
        [SerializeField] GameObject settingsPanel;
        [SerializeField] GameObject editorModeLabel;
        [SerializeField] AnatomyManager anatomyManager;
        [SerializeField] AnatomyInfoDatabase infoDatabase;
        [SerializeField] TransparencyController transparencyController;
        [SerializeField] ModelInteractionController modelInteractionController;
        [SerializeField] ARSessionResetController sessionResetController;
        [SerializeField] ARPlacementController placementController;
        [SerializeField] LiverModelSwitcher modelSwitcher;
        [SerializeField] ARBackgroundController backgroundController;
        [SerializeField] LiverModelWorkspace modelWorkspace;
        [SerializeField] RuntimePatientGlbLoader patientGlbLoader;
        GameObject modelMenuPanel;
        AnatomyManager activeAnatomyManager;

        AnatomyManager CurrentAnatomyManager =>
            modelWorkspace != null && modelWorkspace.ActiveModel != null && modelWorkspace.ActiveModel.AnatomyManager != null
                ? modelWorkspace.ActiveModel.AnatomyManager
                : activeAnatomyManager != null ? activeAnatomyManager : anatomyManager;

        LiverARSettings settings;
        GraphicRaycaster graphicRaycaster;
        Coroutine modelMessageRoutine;
        Image modelMessageBackground;
        readonly List<Toggle> segmentToggles = new List<Toggle>();
        readonly List<Toggle> vesselToggles = new List<Toggle>();
        readonly List<Toggle> tumorToggles = new List<Toggle>();
        bool buttonsBound;

        void Awake()
        {
            graphicRaycaster = GetComponent<GraphicRaycaster>();
            if (graphicRaycaster == null)
                graphicRaycaster = gameObject.AddComponent<GraphicRaycaster>();

            EnsureInputSystemUiModule();
            AutoWireMissingReferences();
            BuildRuntimeUiIfMissing();
            EnsureSplitMenuHierarchy();
            EnsureModelMessageBackground();
            EnsureCameraBackgroundToggle();
            BindSceneButtons();
            Debug.Log("Liver AR UI controller ready. Scene buttons bound once.");
        }

        void OnEnable()
        {
            if (anatomyManager != null)
                anatomyManager.SelectionChanged += OnSelectionChanged;
            if (backgroundController != null)
                backgroundController.StatusMessageChanged += OnBackgroundStatusMessageChanged;
            if (patientGlbLoader != null)
                patientGlbLoader.StatusChanged += OnPatientModelStatusChanged;

            BindSelectedOpacitySlider();

            settings = LiverARSettings.Load();
            BindSettingsControls();
            OnSelectionChanged(anatomyManager != null ? anatomyManager.SelectedPart : null);
            SetNavigationPanel(null);
            SetEditorModeVisible(Application.isEditor);
        }

        void OnDisable()
        {
            if (anatomyManager != null)
                anatomyManager.SelectionChanged -= OnSelectionChanged;
            if (backgroundController != null)
                backgroundController.StatusMessageChanged -= OnBackgroundStatusMessageChanged;
            if (patientGlbLoader != null)
                patientGlbLoader.StatusChanged -= OnPatientModelStatusChanged;

            if (selectedOpacitySlider != null)
                selectedOpacitySlider.onValueChanged.RemoveListener(OnTransparencyChanged);

            if (interactionSensitivitySlider != null)
                interactionSensitivitySlider.onValueChanged.RemoveListener(OnInteractionSensitivityChanged);
            if (rotationSpeedSlider != null)
                rotationSpeedSlider.onValueChanged.RemoveListener(OnRotationSpeedChanged);
            if (scaleSensitivitySlider != null)
                scaleSensitivitySlider.onValueChanged.RemoveListener(OnScaleSensitivityChanged);
            if (hapticToggle != null)
                hapticToggle.onValueChanged.RemoveListener(OnHapticChanged);
            if (cameraBackgroundToggle != null)
                cameraBackgroundToggle.onValueChanged.RemoveListener(OnCameraBackgroundChanged);
        }

        public void SetInstruction(string message)
        {
            if (instructionText != null)
                instructionText.text = message;
        }

        public void SetSurfaceState(ARSurfaceState state)
        {
            var message = ARSurfaceStatusMessage.GetMessage(state);
            SetInstruction(message);
            if (surfaceStatusText != null)
                surfaceStatusText.text = message;
            if (instructionText != null)
                instructionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
            if (surfaceStatusText != null)
                surfaceStatusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
            if (placeLiverButton != null)
                placeLiverButton.interactable = state != ARSurfaceState.Placed && state != ARSurfaceState.Unsupported;
        }

        public void SetVirtualSurfaceOptionVisible(bool visible)
        {
            if (virtualSurfaceButton != null)
                virtualSurfaceButton.gameObject.SetActive(visible);
        }

        public void PlaceLiver()
        {
            if (placementController == null)
            {
                Debug.LogWarning("Place Liver clicked, but no ARPlacementController is available.");
                ShowTemporaryModelMessage("Liver placement failed. Please try again.", 2.5f, false);
                return;
            }

            ShowPersistentModelMessage("Placing liver...", false);
            if (!placementController.PlaceLiver())
            {
                var failure = string.IsNullOrWhiteSpace(placementController.LastPlacementFailureMessage)
                    ? "Liver placement failed. Please try again."
                    : placementController.LastPlacementFailureMessage;
                Debug.LogWarning("Place Liver clicked, but no placement pose is available yet.");
                ShowTemporaryModelMessage(failure, 2.5f, false);
                return;
            }

            if (placementController.TryValidatePlacedModelVisibility(out var reason))
            {
                ShowTemporaryModelMessage("Liver placed successfully.", 2.5f, true);
                return;
            }

            ShowTemporaryModelMessage(string.IsNullOrWhiteSpace(reason) ? "Liver placement failed. Please try again." : reason, 3f, false);
        }

        public void ImportPatientModel()
        {
            if (patientGlbLoader == null)
            {
                ShowTemporaryModelMessage("Patient GLB loader is not configured.", 2.5f, false);
                return;
            }
            patientGlbLoader.PickPatientModel();
        }

        public void UseVirtualSurface() => placementController?.UseVirtualSurface();
        public void ToggleMenu() => SetNavigationPanel(compactMenuPanel != null && compactMenuPanel.activeSelf ? null : compactMenuPanel);
        public void OpenModelMenu()
        {
            EnsureModelMenu();
            RebuildModelRows();
            SetNavigationPanel(modelMenuPanel);
        }
        public void OpenSegmentationMenu()
        {
            UpdateVesselOptionVisibility();
            UpdateTumorOptionVisibility();
            SetNavigationPanel(segmentationMenuPanel);
        }
        public void OpenCouinaudSegmentsPanel()
        {
            CurrentAnatomyManager?.ShowLiverSegments();
            RebuildSegmentToggles();
            SetNavigationPanel(couinaudSegmentsPanel);
        }
        public void OpenVesselsPanel()
        {
            // Opening the vessel controls must not change the user's liver visibility choices.
            ShowVesselsWithoutChangingLiverState();
            RebuildVesselToggles();
            SetNavigationPanel(vesselPanel);
        }
        public void OpenTumorPanel()
        {
            RebuildTumorToggles();
            SetNavigationPanel(tumorPanel);
        }
        public void OpenInformationPanelForSelection()
        {
            EnsureDetailPanels();
            RenderCompactInformation(AnatomyInformationCatalog.ForPart(CurrentAnatomyManager != null ? CurrentAnatomyManager.SelectedPart : null));
        }
        public void OpenInformationMenu()
        {
            EnsureDetailPanels();
            RenderInformationHome();
        }
        public void CloseInformationPanel() => SetPanelActive(informationPanel, null);
        public void OpenTransparencyPanelForSelection()
        {
            EnsureDetailPanels();
            OnSelectionChanged(CurrentAnatomyManager != null ? CurrentAnatomyManager.SelectedPart : null);
            SetPanelActive(transparencyPanel, transparencyPanel);
        }
        public void ToggleTransparencyPanelForSelection()
        {
            OnSelectionChanged(CurrentAnatomyManager != null ? CurrentAnatomyManager.SelectedPart : null);
            SetPanelActive(transparencyPanel, transparencyPanel != null && transparencyPanel.activeSelf ? null : transparencyPanel);
        }
        public void OpenSettingsPanel() => SetNavigationPanel(settingsPanel);
        public void OpenUserManual()
        {
            EnsureDetailPanels();
            RenderInformationDetail(new AnatomyInformationRecord
            {
                DisplayName = "User Manual", Category = "Help",
                Overview = "Place the liver on a detected plane or use Virtual Surface.",
                Location = "Use one finger to select and move. Use two fingers to rotate and pinch to scale.",
                BloodSupply = "Open Segmentation to show or hide liver segments and vessels.",
                VenousDrainage = "Double-tap a selected structure to open its information.",
                Function = "Open Model to import a patient GLB or manage models. Open Settings to adjust interaction sensitivity.",
                Description = "When an information panel is open, scrolling and buttons take priority over AR gestures. Close the panel to return to AR interaction."
            }, false);
        }
        public void ClosePanels() => SetNavigationPanel(null);
        public void ResetPlacement() => sessionResetController?.ResetSession();

        public void SelectNormalModel()
        {
            if (modelWorkspace == null)
                modelWorkspace = FindAnyObjectByType<LiverModelWorkspace>();
            if (modelWorkspace != null && modelWorkspace.ActiveModel == null)
            {
                placementController?.PlaceLiver();
                return;
            }
            if (modelWorkspace != null && modelWorkspace.Models.Count > 0)
            {
                var root = modelWorkspace.Models[0];
                modelWorkspace.Activate(root);
                activeAnatomyManager = root.AnatomyManager;
                return;
            }
            if (placementController != null && placementController.SwitchModel(LiverModelType.Normal))
            {
                CurrentAnatomyManager?.ClearSelection();
                SetModelMessage(string.Empty);
                return;
            }

            if (modelSwitcher != null && modelSwitcher.SwitchTo(LiverModelType.Normal))
                SetModelMessage(string.Empty);
        }

        public void CreateNewModel()
        {
            placementController?.PlaceLiver();
            OpenModelMenu();
        }

        public void DuplicateActiveModel()
        {
            if (modelWorkspace?.DuplicateActive() != null)
                OpenModelMenu();
        }

        public void DeleteActiveModel()
        {
            if (modelWorkspace != null && modelWorkspace.DeleteActive())
                OpenModelMenu();
        }
        public void CancelModelAction() => ClosePanels();

        public void SelectDiseaseModel()
        {
            if (placementController != null && placementController.SwitchModel(LiverModelType.Disease))
            {
                CurrentAnatomyManager?.ClearSelection();
                SetModelMessage(string.Empty);
                return;
            }

            if (modelSwitcher == null || !modelSwitcher.SwitchTo(LiverModelType.Disease))
                SetModelMessage("Disease model not yet assigned.");
        }

        public void ShowSegments()
        {
            CurrentAnatomyManager?.ShowLiverSegments();
            RebuildSegmentToggles();
        }
        public void ShowVessels() => ShowCategory(AnatomyCategory.Vessel);
        public void ShowAllSegments()
        {
            CurrentAnatomyManager?.ShowLiverSegments();
            SetTogglesOn(segmentToggles, true);
        }

        public void HideAllSegments() => SetCategoryVisible(CurrentAnatomyManager, AnatomyCategory.LiverSegment, false, segmentToggles);
        public void ShowAllVessels() => SetCategoryVisible(CurrentAnatomyManager, AnatomyCategory.Vessel, true, vesselToggles);
        public void HideAllVessels() => SetCategoryVisible(CurrentAnatomyManager, AnatomyCategory.Vessel, false, vesselToggles);
        public void ShowAllTumors() => SetCategoryVisible(CurrentAnatomyManager, AnatomyCategory.Lesion, true, tumorToggles);
        public void HideAllTumors() => SetCategoryVisible(CurrentAnatomyManager, AnatomyCategory.Lesion, false, tumorToggles);

        public void IsolateSelected()
        {
            if (CurrentAnatomyManager == null || CurrentAnatomyManager.SelectedPart == null)
                return;

            var selected = CurrentAnatomyManager.SelectedPart;
            foreach (var part in CurrentAnatomyManager.Parts)
                part.SetVisible(part == selected);
        }

        public void ShowAll() => CurrentAnatomyManager?.ShowAll();
        public void HideAll()
        {
            CurrentAnatomyManager?.HideAll();
            foreach (var toggle in segmentToggles)
            {
                toggle.SetIsOnWithoutNotify(false);
                UpdateToggleStatusText(toggle);
            }
        }
        public void ResetAppearance()
        {
            CurrentAnatomyManager?.ResetAllAppearances();
            foreach (var toggle in segmentToggles)
            {
                toggle.SetIsOnWithoutNotify(true);
                UpdateToggleStatusText(toggle);
            }
        }
        public void ResetModelTransform() => modelInteractionController?.ResetTransform();
        public void ResetARSession() => sessionResetController?.ResetSession();
        public void ClearSelection() => CurrentAnatomyManager?.ClearSelection();
        public void ShowSelected() => CurrentAnatomyManager?.SelectedPart?.SetVisible(true);
        public void HideSelected() => CurrentAnatomyManager?.SelectedPart?.SetVisible(false);

        public void SetActiveSelection(AnatomyManager manager, AnatomyPart part)
        {
            activeAnatomyManager = manager;
            OnSelectionChanged(part);
        }

        public void SetSelectedColor(Color color)
        {
            Debug.Log("Colour editing is intentionally disabled; imported model materials are preserved.");
        }

        void OnTransparencyChanged(float value)
        {
            var manager = CurrentAnatomyManager;
            if (manager != null && manager.SelectedPart != null)
            {
                manager.SelectedPart.SetOpacity(value);
                return;
            }

            transparencyController?.SetSelectedOpacity(value);
        }

        public void ResetSelectedTransparency()
        {
            if (transparencyController != null && transparencyController.ResetSelectedOpacity())
            {
                var selected = CurrentAnatomyManager != null ? CurrentAnatomyManager.SelectedPart : null;
                if (selectedOpacitySlider != null && selected != null)
                    selectedOpacitySlider.SetValueWithoutNotify(selected.Opacity);
            }
        }

        void OnSelectionChanged(AnatomyPart part)
        {
            var hasSelection = part != null;
            if (selectedNameText != null)
                selectedNameText.text = hasSelection ? part.DisplayName : "No structure selected";
            if (selectedCategoryText != null)
                selectedCategoryText.text = hasSelection ? part.Category.ToString() : "Tap an anatomical structure";
            if (selectedOpacitySlider != null)
            {
                selectedOpacitySlider.interactable = hasSelection;
                if (hasSelection)
                    selectedOpacitySlider.SetValueWithoutNotify(part.Opacity);
            }

            if (transparencyTitleText != null)
                transparencyTitleText.text = hasSelection ? $"Opacity: {part.DisplayName}" : "Opacity: no selection";

            UpdateInformationPanel(part);
        }

        public void CloseDetailOverlays()
        {
            SetPanelActive(informationPanel, null);
            SetPanelActive(compactInformationPanel, null);
            SetPanelActive(transparencyPanel, null);
        }

        void UpdateVesselOptionVisibility()
        {
            if (segmentationMenuPanel == null)
                return;

            var vesselButton = segmentationMenuPanel.transform.Find("Blood Vessel Button") ?? segmentationMenuPanel.transform.Find("Vessels Button");
            if (vesselButton != null)
                vesselButton.gameObject.SetActive(HasAnatomyPart(AnatomyCategory.Vessel));
        }

        void UpdateTumorOptionVisibility()
        {
            if (segmentationMenuPanel == null)
                return;

            var tumorButton = segmentationMenuPanel.transform.Find("Tumor Button");
            if (tumorButton != null)
                tumorButton.gameObject.SetActive(HasAnatomyPart(AnatomyCategory.Lesion));
        }

        bool HasAnatomyPart(AnatomyCategory category)
        {
            if (CurrentAnatomyManager == null)
                return false;

            foreach (var part in CurrentAnatomyManager.Parts)
            {
                if (part != null && part.Category == category)
                    return true;
            }

            return false;
        }

        void ShowCategory(AnatomyCategory category)
        {
            if (CurrentAnatomyManager == null)
                return;

            foreach (var part in CurrentAnatomyManager.Parts)
                part.SetVisible(part.Category == category);
            CurrentAnatomyManager.ClearSelection();
        }

        void ShowVesselsWithoutChangingLiverState()
        {
            if (CurrentAnatomyManager == null)
                return;

            foreach (var part in CurrentAnatomyManager.Parts)
            {
                if (part != null && part.Category == AnatomyCategory.Vessel)
                    part.SetVisible(true);
            }
        }

        void SetNavigationPanel(GameObject activePanel)
        {
            SetPanelActive(compactMenuPanel, activePanel);
            SetPanelActive(segmentationMenuPanel, activePanel);
            SetPanelActive(couinaudSegmentsPanel, activePanel);
            SetPanelActive(vesselPanel, activePanel);
            SetPanelActive(tumorPanel, activePanel);
            SetPanelActive(settingsPanel, activePanel);
            SetPanelActive(modelMenuPanel, activePanel);
            SetPanelActive(informationPanel, activePanel);
            SetPanelActive(compactInformationPanel, activePanel);
            SetPanelActive(transparencyPanel, activePanel);
        }

        void EnsureModelMenu()
        {
            if (modelMenuPanel != null) return;
            modelMenuPanel = CreateRuntimePanel(transform, "Model Menu", new Vector2(0.58f, 0.28f), new Vector2(0.36f, 0.54f));
        }

        void RebuildModelRows()
        {
            if (modelMenuPanel == null) return;
            var old = modelMenuPanel.transform.Find("Model Rows");
            if (old != null)
            {
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }
            old = modelMenuPanel.transform.Find("Model Actions");
            if (old != null)
            {
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }
            var rows = new GameObject("Model Rows", typeof(RectTransform)); rows.transform.SetParent(modelMenuPanel.transform, false);
            var rowsRect = rows.GetComponent<RectTransform>(); rowsRect.anchorMin = Vector2.zero; rowsRect.anchorMax = Vector2.one; rowsRect.offsetMin = Vector2.zero; rowsRect.offsetMax = Vector2.zero;
            var roots = modelWorkspace != null ? modelWorkspace.Models : null;
            var count = roots != null ? roots.Count : 0;

            var viewport = new GameObject("Model List Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(rows.transform, false);
            SetAnchors(viewport.GetComponent<RectTransform>(), new Vector2(.08f, .30f), new Vector2(.84f, .60f));
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.02f);

            var content = new GameObject("Model List Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 4, 4);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = viewport.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 32f;

            if (count == 0)
                CreateModelListButton(content.transform, "Place Liver", SelectNormalModel);
            else
                for (var i = 0; i < count; i++)
                {
                    var root = roots[i];
                    if (root == null) continue;
                    CreateModelListButton(content.transform, root.DisplayName, () =>
                    {
                        modelWorkspace.Activate(root);
                        activeAnatomyManager = root.AnatomyManager;
                        OpenModelActions();
                    });
                }
            CreateRuntimeButton(rows.transform, "Import Patient Model", new Vector2(.08f,.18f), new Vector2(.40f,.12f), ImportPatientModel);
            CreateRuntimeButton(rows.transform, "New Model", new Vector2(.52f,.18f), new Vector2(.40f,.12f), CreateNewModel);
            CreateRuntimeButton(rows.transform, "Close", new Vector2(.30f,.04f), new Vector2(.40f,.10f), CancelModelAction);
        }

        static Button CreateModelListButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var button = CreateRuntimeButton(parent, label, Vector2.zero, Vector2.one, action);
            var element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 62f;
            element.minHeight = 62f;
            return button;
        }

        void OpenModelActions()
        {
            var old = modelMenuPanel.transform.Find("Model Actions");
            if (old != null)
            {
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }
            var rows = modelMenuPanel.transform.Find("Model Rows");
            if (rows != null) rows.gameObject.SetActive(false);
            var actions = new GameObject("Model Actions", typeof(RectTransform)); actions.transform.SetParent(modelMenuPanel.transform, false);
            var actionsRect = actions.GetComponent<RectTransform>(); actionsRect.anchorMin = Vector2.zero; actionsRect.anchorMax = Vector2.one; actionsRect.offsetMin = Vector2.zero; actionsRect.offsetMax = Vector2.zero;
            CreateRuntimeButton(actions.transform, "Duplicate", new Vector2(.08f,.52f), new Vector2(.84f,.14f), DuplicateActiveModel);
            CreateRuntimeButton(actions.transform, "Delete", new Vector2(.08f,.34f), new Vector2(.84f,.14f), DeleteActiveModel);
            CreateRuntimeButton(actions.transform, "Back", new Vector2(.08f,.16f), new Vector2(.84f,.14f), OpenModelMenu);
        }

        static void SetPanelActive(GameObject panel, GameObject activePanel)
        {
            if (panel != null)
            {
                panel.SetActive(panel == activePanel);
                if (panel == activePanel)
                    panel.transform.SetAsLastSibling();
            }
        }

        void UpdateInformationPanel(AnatomyPart part)
        {
            if (informationBodyText == null)
                return;

            if (part == null)
            {
                informationBodyText.text = "Select an anatomical structure to view details.";
                return;
            }

            informationBodyText.text = AnatomyInformationCatalog.ForPart(part).ToDisplayText();
        }

        void RenderInformationHome()
        {
            RebuildInformationPanel("Information", new[] { "Liver", "Couinaud Segmentation", "Blood Vessel", "Tumor", "Liver Disease" },
                new[] { (UnityEngine.Events.UnityAction)(() => RenderInformationDetail(AnatomyInformationCatalog.Liver, true)),
                    () => RenderInformationList("Couinaud Segmentation", AnatomyInformationCatalog.SegmentNames, AnatomyInformationCatalog.ForSegment),
                    () => RenderInformationList("Blood Vessel", GetAvailableVesselNames(), AnatomyInformationCatalog.ForVessel),
                    () => RenderInformationList("Tumor", GetAvailableTumorNames(), AnatomyInformationCatalog.ForTumor),
                    () => RenderInformationList("Liver Disease", AnatomyInformationCatalog.DiseaseNames, AnatomyInformationCatalog.ForDisease) }, true);
        }

        void RenderInformationList(string title, string[] names, Func<string, AnatomyInformationRecord> recordFactory)
        {
            EnsureDetailPanels();
            ClearInformationPanel();
            CreateRuntimeText(informationPanel.transform, "Information Title", title, 24, TextAnchor.MiddleCenter, new Vector2(.06f, .88f), new Vector2(.94f, .98f));
            var viewport = new GameObject("Information List Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(informationPanel.transform, false);
            SetAnchors(viewport.GetComponent<RectTransform>(), new Vector2(.06f, .18f), new Vector2(.94f, .86f));
            var content = new GameObject("Information List Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f); contentRect.anchorMax = new Vector2(1f, 1f); contentRect.pivot = new Vector2(.5f, 1f);
            var layout = content.GetComponent<VerticalLayoutGroup>(); layout.spacing = 6f; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.AddComponent<ScrollRect>(); scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.content = contentRect; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            foreach (var name in names)
            {
                var record = recordFactory(name);
                var button = CreateRuntimeButton(content.transform, record.DisplayName, Vector2.zero, Vector2.one, () => RenderInformationDetail(record, true));
                SetRuntimeButtonFontSize(button, 18);
                var element = button.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = 52f; element.minHeight = 52f;
            }
            CreateRuntimeButton(informationPanel.transform, "Back", new Vector2(.06f, .04f), new Vector2(.40f, .11f), RenderInformationHome);
            CreateRuntimeButton(informationPanel.transform, "Close", new Vector2(.54f, .04f), new Vector2(.40f, .11f), CloseInformationPanel);
            SetNavigationPanel(informationPanel);
        }

        void RenderInformationDetail(AnatomyInformationRecord record, bool allowBack)
        {
            EnsureDetailPanels();
            ClearInformationPanel();
            CreateRuntimeText(informationPanel.transform, "Information Title", record.DisplayName, 24, TextAnchor.MiddleCenter, new Vector2(.06f, .88f), new Vector2(.94f, .98f));
            var viewport = new GameObject("Information Detail Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(informationPanel.transform, false);
            SetAnchors(viewport.GetComponent<RectTransform>(), new Vector2(.06f, .18f), new Vector2(.94f, .86f));
            var content = new GameObject("Information Detail Content", typeof(RectTransform), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>(); contentRect.anchorMin = new Vector2(0f, 1f); contentRect.anchorMax = new Vector2(1f, 1f); contentRect.pivot = new Vector2(.5f, 1f);
            var text = CreateRuntimeText(content.transform, "Information Body", record.ToDisplayText(), 20, TextAnchor.UpperLeft, Vector2.zero, Vector2.one);
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            var textRect = text.rectTransform;
            textRect.anchorMin = new Vector2(0f, 1f); textRect.anchorMax = new Vector2(1f, 1f); textRect.pivot = new Vector2(.5f, 1f);
            Canvas.ForceUpdateCanvases();
            var textHeight = Mathf.Max(640f, text.preferredHeight + 24f);
            contentRect.sizeDelta = new Vector2(0f, textHeight);
            textRect.sizeDelta = new Vector2(0f, textHeight);
            var fitter = content.GetComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.AddComponent<ScrollRect>(); scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.content = contentRect; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            CreateRuntimeButton(informationPanel.transform, "Back", new Vector2(.06f, .04f), new Vector2(.40f, .11f), allowBack ? RenderInformationHome : OpenInformationMenu);
            CreateRuntimeButton(informationPanel.transform, "Close", new Vector2(.54f, .04f), new Vector2(.40f, .11f), CloseInformationPanel);
            SetNavigationPanel(informationPanel);
        }

        string[] GetAvailableVesselNames()
        {
            var manager = CurrentAnatomyManager;
            if (manager != null && HasAnatomyPart(AnatomyCategory.Vessel))
            {
                var names = new List<string>();
                foreach (var part in manager.Parts)
                    if (part != null && part.Category == AnatomyCategory.Vessel && !names.Contains(part.DisplayName)) names.Add(part.DisplayName);
                if (names.Count > 0) return names.ToArray();
            }
            return new[] { "Blood Vessel Overview" };
        }

        string[] GetAvailableTumorNames()
        {
            var manager = CurrentAnatomyManager;
            if (manager != null && HasAnatomyPart(AnatomyCategory.Lesion))
            {
                var names = new List<string>();
                foreach (var part in manager.Parts)
                    if (part != null && part.Category == AnatomyCategory.Lesion && !names.Contains(part.DisplayName)) names.Add(part.DisplayName);
                if (names.Count > 0) return names.ToArray();
            }
            return new[] { "Tumor Overview" };
        }

        void RenderCompactInformation(AnatomyInformationRecord record)
        {
            EnsureDetailPanels();
            SetPanelActive(informationPanel, null);
            ClearCompactInformationPanel();
            CreateRuntimeText(compactInformationPanel.transform, "Information Title", record.DisplayName, 16, TextAnchor.MiddleCenter, new Vector2(.06f, .84f), new Vector2(.94f, .97f));
            var viewport = new GameObject("Compact Information Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(compactInformationPanel.transform, false);
            SetAnchors(viewport.GetComponent<RectTransform>(), new Vector2(.06f, .22f), new Vector2(.94f, .80f));
            var content = new GameObject("Compact Information Content", typeof(RectTransform), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>(); contentRect.anchorMin = new Vector2(0f, 1f); contentRect.anchorMax = new Vector2(1f, 1f); contentRect.pivot = new Vector2(.5f, 1f);
            var text = CreateRuntimeText(content.transform, "Information Body", record.ToDisplayText(), 14, TextAnchor.UpperLeft, Vector2.zero, Vector2.one);
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            Canvas.ForceUpdateCanvases();
            var height = Mathf.Max(360f, text.preferredHeight + 20f);
            contentRect.sizeDelta = new Vector2(0f, height); text.rectTransform.sizeDelta = new Vector2(0f, height);
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.AddComponent<ScrollRect>(); scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.content = contentRect; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            CreateRuntimeButton(compactInformationPanel.transform, "Close", new Vector2(.32f, .05f), new Vector2(.36f, .12f), () => SetPanelActive(compactInformationPanel, null));
            compactInformationPanel.SetActive(true);
            compactInformationPanel.transform.SetAsLastSibling();
        }

        void ClearInformationPanel()
        {
            foreach (Transform child in informationPanel.transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            informationBodyText = null;
        }

        void ClearCompactInformationPanel()
        {
            foreach (Transform child in compactInformationPanel.transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        void RebuildInformationPanel(string title, string[] labels, UnityEngine.Events.UnityAction[] actions, bool showHomeBack)
        {
            EnsureDetailPanels();
            ClearInformationPanel();
            CreateRuntimeText(informationPanel.transform, "Information Title", title, 24, TextAnchor.MiddleCenter, new Vector2(.06f, .88f), new Vector2(.94f, .98f));
            var viewport = new GameObject("Information Category Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(informationPanel.transform, false); SetAnchors(viewport.GetComponent<RectTransform>(), new Vector2(.06f, .18f), new Vector2(.94f, .86f));
            var content = new GameObject("Information Category Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)); content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>(); contentRect.anchorMin = new Vector2(0f, 1f); contentRect.anchorMax = new Vector2(1f, 1f); contentRect.pivot = new Vector2(.5f, 1f);
            var layout = content.GetComponent<VerticalLayoutGroup>(); layout.spacing = 8f; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false; content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            for (var i = 0; i < labels.Length; i++) { var button = CreateRuntimeButton(content.transform, labels[i], Vector2.zero, Vector2.one, actions[i]); SetRuntimeButtonFontSize(button, 18); var element = button.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = 58f; element.minHeight = 58f; }
            CreateRuntimeButton(informationPanel.transform, "Back", new Vector2(.06f, .04f), new Vector2(.40f, .11f), showHomeBack ? CloseInformationPanel : RenderInformationHome);
            CreateRuntimeButton(informationPanel.transform, "Close", new Vector2(.54f, .04f), new Vector2(.40f, .11f), CloseInformationPanel);
            var scroll = viewport.AddComponent<ScrollRect>(); scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.content = contentRect; scroll.horizontal = false; scroll.vertical = true;
            SetNavigationPanel(informationPanel);
        }

        static string ValueOrPlaceholder(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Placeholder - verified content not yet assigned" : value;
        }

        void BindSettingsControls()
        {
            if (settings == null)
                settings = LiverARSettings.CreateDefault();

            modelInteractionController?.ApplySettings(settings);

            if (interactionSensitivitySlider != null)
            {
                interactionSensitivitySlider.onValueChanged.RemoveListener(OnInteractionSensitivityChanged);
                interactionSensitivitySlider.SetValueWithoutNotify(settings.InteractionSensitivity);
                interactionSensitivitySlider.onValueChanged.AddListener(OnInteractionSensitivityChanged);
            }
            if (rotationSpeedSlider != null)
            {
                rotationSpeedSlider.onValueChanged.RemoveListener(OnRotationSpeedChanged);
                rotationSpeedSlider.SetValueWithoutNotify(settings.RotationSpeed);
                rotationSpeedSlider.onValueChanged.AddListener(OnRotationSpeedChanged);
            }
            if (scaleSensitivitySlider != null)
            {
                scaleSensitivitySlider.onValueChanged.RemoveListener(OnScaleSensitivityChanged);
                scaleSensitivitySlider.SetValueWithoutNotify(settings.ScaleSensitivity);
                scaleSensitivitySlider.onValueChanged.AddListener(OnScaleSensitivityChanged);
            }
            if (hapticToggle != null)
            {
                hapticToggle.onValueChanged.RemoveListener(OnHapticChanged);
                hapticToggle.SetIsOnWithoutNotify(settings.HapticFeedback);
                UpdateToggleStatusText(hapticToggle);
                hapticToggle.onValueChanged.AddListener(OnHapticChanged);
            }
            if (cameraBackgroundToggle != null)
            {
                cameraBackgroundToggle.onValueChanged.RemoveListener(OnCameraBackgroundChanged);
                cameraBackgroundToggle.SetIsOnWithoutNotify(backgroundController == null || backgroundController.IsCameraBackgroundEnabled);
                UpdateToggleStatusText(cameraBackgroundToggle);
                cameraBackgroundToggle.onValueChanged.AddListener(OnCameraBackgroundChanged);
            }

            if (diseaseModelButton != null)
                diseaseModelButton.interactable = placementController != null ? placementController.HasDiseaseModel : modelSwitcher == null || modelSwitcher.HasDiseaseModel;
        }

        void OnInteractionSensitivityChanged(float value)
        {
            settings.SetInteractionSensitivity(value);
            ApplyAndSaveSettings();
        }

        void OnRotationSpeedChanged(float value)
        {
            settings.SetRotationSpeed(value);
            ApplyAndSaveSettings();
        }

        void OnScaleSensitivityChanged(float value)
        {
            settings.SetScaleSensitivity(value);
            ApplyAndSaveSettings();
        }

        void OnHapticChanged(bool value)
        {
            settings.SetHapticFeedback(value);
            UpdateToggleStatusText(hapticToggle);
            ApplyAndSaveSettings();
        }

        void OnCameraBackgroundChanged(bool enabled)
        {
            if (backgroundController == null)
            {
                SetModelMessage(enabled ? "Camera background unavailable. Virtual environment is active." : "Virtual environment active.");
                if (cameraBackgroundToggle != null)
                {
                    cameraBackgroundToggle.SetIsOnWithoutNotify(false);
                    UpdateToggleStatusText(cameraBackgroundToggle);
                }
                return;
            }

            if (!backgroundController.SetCameraBackgroundEnabled(enabled))
            {
                if (cameraBackgroundToggle != null)
                {
                    cameraBackgroundToggle.SetIsOnWithoutNotify(false);
                    UpdateToggleStatusText(cameraBackgroundToggle);
                }
            }

            UpdateToggleStatusText(cameraBackgroundToggle);
        }

        void OnBackgroundStatusMessageChanged(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            SetInstruction(message);
            SetModelMessage(message);
        }

        public void ResetSettings()
        {
            settings.Reset();
            settings.Save();
            backgroundController?.SetCameraBackgroundEnabled(!Application.isEditor);
            BindSettingsControls();
        }

        void ApplyAndSaveSettings()
        {
            settings.Save();
            modelInteractionController?.ApplySettings(settings);
        }

        void SetModelMessage(string message)
        {
            if (modelMessageText != null)
            {
                modelMessageText.text = message;
                modelMessageText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
            }

            if (modelMessageBackground != null)
                modelMessageBackground.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        void OnPatientModelStatusChanged(string message)
        {
            var failed = patientGlbLoader != null && !string.IsNullOrWhiteSpace(patientGlbLoader.LastError);
            var finished = string.Equals(message, "Patient GLB loaded successfully.", StringComparison.Ordinal);
            var inProgress = patientGlbLoader != null && patientGlbLoader.IsLoading;
            if (inProgress && !failed)
                ShowPersistentModelMessage(message, false);
            else
                ShowTemporaryModelMessage(message, failed ? 4f : 5f, !failed);

            if (finished)
                OpenModelMenu();
        }

        void ShowPersistentModelMessage(string message, bool success)
        {
            if (modelMessageRoutine != null)
            {
                StopCoroutine(modelMessageRoutine);
                modelMessageRoutine = null;
            }

            ApplyModelMessageStyle(success);
            SetModelMessage(message);
        }

        void ShowTemporaryModelMessage(string message, float duration, bool success)
        {
            ShowPersistentModelMessage(message, success);
            if (isActiveAndEnabled)
                modelMessageRoutine = StartCoroutine(HideModelMessageAfterDelay(duration));
        }

        IEnumerator HideModelMessageAfterDelay(float duration)
        {
            yield return new WaitForSeconds(duration);
            SetModelMessage(string.Empty);
            modelMessageRoutine = null;
        }

        void ApplyModelMessageStyle(bool success)
        {
            if (modelMessageBackground != null)
                modelMessageBackground.color = success ? new Color(0.07f, 0.35f, 0.18f, 0.88f) : new Color(0.12f, 0.13f, 0.14f, 0.88f);
        }

        void SetEditorModeVisible(bool visible)
        {
            if (editorModeLabel != null)
                editorModeLabel.SetActive(visible);
        }

        void BuildRuntimeUiIfMissing()
        {
            if (placeLiverButton != null)
                return;

            HideObsoleteControls();
            var root = transform;
            placeLiverButton = CreateRuntimeButton(root, "Place Liver", new Vector2(0.38f, 0.08f), new Vector2(0.24f, 0.08f), PlaceLiver);
            CreateRuntimeButton(root, "Menu", new Vector2(0.82f, 0.82f), new Vector2(0.12f, 0.08f), ToggleMenu);
            virtualSurfaceButton = CreateRuntimeButton(root, "Use Virtual Surface", new Vector2(0.31f, 0.18f), new Vector2(0.38f, 0.07f), UseVirtualSurface);
            virtualSurfaceButton.gameObject.SetActive(false);

            compactMenuPanel = CreateRuntimePanel(root, "Compact Menu", new Vector2(0.66f, 0.45f), new Vector2(0.31f, 0.35f));
            CreateRuntimeButton(compactMenuPanel.transform, "Model", new Vector2(0.08f, 0.76f), new Vector2(0.84f, 0.16f), OpenModelMenu);
            CreateRuntimeButton(compactMenuPanel.transform, "Segmentation", new Vector2(0.08f, 0.58f), new Vector2(0.84f, 0.16f), OpenSegmentationMenu);
            CreateRuntimeButton(compactMenuPanel.transform, "Settings", new Vector2(0.08f, 0.40f), new Vector2(0.84f, 0.16f), OpenSettingsPanel);
            CreateRuntimeButton(compactMenuPanel.transform, "Information", new Vector2(0.08f, 0.22f), new Vector2(0.84f, 0.16f), OpenInformationMenu);
            CreateRuntimeButton(compactMenuPanel.transform, "Reset Placement", new Vector2(0.08f, 0.04f), new Vector2(0.84f, 0.14f), ResetPlacement);

            segmentationMenuPanel = CreateRuntimePanel(root, "Segmentation Menu", new Vector2(0.04f, 0.13f), new Vector2(0.32f, 0.38f));
            CreateRuntimeButton(segmentationMenuPanel.transform, "Couinaud Segments", new Vector2(0.08f, 0.72f), new Vector2(0.84f, 0.16f), OpenCouinaudSegmentsPanel);
            CreateRuntimeButton(segmentationMenuPanel.transform, "Blood Vessel", new Vector2(0.08f, 0.50f), new Vector2(0.84f, 0.16f), OpenVesselsPanel);
            CreateRuntimeButton(segmentationMenuPanel.transform, "Tumor", new Vector2(0.08f, 0.28f), new Vector2(0.84f, 0.16f), OpenTumorPanel).gameObject.SetActive(false);
            CreateRuntimeButton(segmentationMenuPanel.transform, "Back", new Vector2(0.08f, 0.06f), new Vector2(0.84f, 0.16f), ToggleMenu);

            couinaudSegmentsPanel = CreateRuntimePanel(root, "Couinaud Segments Panel", new Vector2(0.04f, 0.13f), new Vector2(0.32f, 0.58f));
            CreateRuntimeButton(couinaudSegmentsPanel.transform, "Show All", new Vector2(0.08f, 0.18f), new Vector2(0.40f, 0.10f), ShowAllSegments);
            CreateRuntimeButton(couinaudSegmentsPanel.transform, "Hide All", new Vector2(0.52f, 0.18f), new Vector2(0.40f, 0.10f), HideAllSegments);
            CreateRuntimeButton(couinaudSegmentsPanel.transform, "Close", new Vector2(0.30f, 0.04f), new Vector2(0.40f, 0.10f), ClosePanels);

            vesselPanel = CreateRuntimePanel(root, "Vessel Panel", new Vector2(0.04f, 0.13f), new Vector2(0.32f, 0.58f));
            CreateRuntimeButton(vesselPanel.transform, "Show All", new Vector2(0.08f, 0.18f), new Vector2(0.40f, 0.10f), ShowAllVessels);
            CreateRuntimeButton(vesselPanel.transform, "Hide All", new Vector2(0.52f, 0.18f), new Vector2(0.40f, 0.10f), HideAllVessels);
            CreateRuntimeButton(vesselPanel.transform, "Close", new Vector2(0.30f, 0.04f), new Vector2(0.40f, 0.10f), ClosePanels);

            tumorPanel = CreateRuntimePanel(root, "Tumor Panel", new Vector2(0.04f, 0.13f), new Vector2(0.32f, 0.58f));
            CreateRuntimeButton(tumorPanel.transform, "Show All", new Vector2(0.08f, 0.18f), new Vector2(0.40f, 0.10f), ShowAllTumors);
            CreateRuntimeButton(tumorPanel.transform, "Hide All", new Vector2(0.52f, 0.18f), new Vector2(0.40f, 0.10f), HideAllTumors);
            CreateRuntimeButton(tumorPanel.transform, "Close", new Vector2(0.30f, 0.04f), new Vector2(0.40f, 0.10f), ClosePanels);

            informationPanel = CreateRuntimePanel(root, "Information Panel", new Vector2(0.50f, 0.08f), new Vector2(0.46f, 0.84f));
            informationBodyText = CreateRuntimeText(informationPanel.transform, "Information Body", "Select an anatomical structure to view details.", 15, TextAnchor.UpperLeft, new Vector2(0.06f, 0.20f), new Vector2(0.94f, 0.94f));
            CreateRuntimeButton(informationPanel.transform, "Close", new Vector2(0.32f, 0.04f), new Vector2(0.36f, 0.12f), () => SetPanelActive(informationPanel, null));
            compactInformationPanel = CreateRuntimePanel(root, "Compact Information Panel", new Vector2(0.55f, 0.16f), new Vector2(0.40f, 0.30f));

            transparencyPanel = CreateRuntimePanel(root, "Transparency Panel", new Vector2(0.34f, 0.37f), new Vector2(0.34f, 0.14f));
            transparencyTitleText = CreateRuntimeText(transparencyPanel.transform, "Transparency Title", "Opacity: no selection", 14, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.94f));
            selectedOpacitySlider = CreateRuntimeSlider(transparencyPanel.transform, "Selected Opacity", new Vector2(0.08f, 0.20f), new Vector2(0.84f, 0.22f), 0f, 1f, 1f);

            settingsPanel = CreateRuntimePanel(root, "Settings Panel", new Vector2(0.58f, 0.18f), new Vector2(0.36f, 0.42f));
            interactionSensitivitySlider = CreateRuntimeSlider(settingsPanel.transform, "Interaction Sensitivity", new Vector2(0.08f, 0.70f), new Vector2(0.84f, 0.10f), 0.2f, 3f, 1f);
            rotationSpeedSlider = CreateRuntimeSlider(settingsPanel.transform, "Rotation Speed", new Vector2(0.08f, 0.54f), new Vector2(0.84f, 0.10f), 0.2f, 5f, 1f);
            scaleSensitivitySlider = CreateRuntimeSlider(settingsPanel.transform, "Scale Sensitivity", new Vector2(0.08f, 0.38f), new Vector2(0.84f, 0.10f), 0.1f, 4f, 1f);
            cameraBackgroundToggle = CreateRuntimeToggle(settingsPanel.transform, "Camera Background", new Vector2(0.08f, 0.25f), new Vector2(0.84f, 0.10f));
            CreateRuntimeButton(settingsPanel.transform, "User Manual", new Vector2(0.08f, 0.08f), new Vector2(0.40f, 0.12f), OpenUserManual);
            CreateRuntimeButton(settingsPanel.transform, "Back", new Vector2(0.54f, 0.08f), new Vector2(0.38f, 0.12f), ClosePanels);

            editorModeLabel = CreateRuntimeText(root, "Editor Test Mode Label", "Editor Test Mode", 14, TextAnchor.UpperLeft, new Vector2(0.03f, 0.92f), new Vector2(0.30f, 0.98f)).gameObject;
            modelMessageText = CreateRuntimeText(root, "Model Message", "", 14, TextAnchor.LowerCenter, new Vector2(0.20f, 0.26f), new Vector2(0.80f, 0.32f));
        }

        void EnsureSplitMenuHierarchy()
        {
            HideObsoleteControls();

            var root = transform;
            if (compactMenuPanel == null)
                compactMenuPanel = FindDirectChild("Compact Menu");
            if (couinaudSegmentsPanel == null)
                couinaudSegmentsPanel = FindDirectChild("Segmentation Panel") ?? FindDirectChild("Couinaud Segments Panel");
            if (settingsPanel == null)
                settingsPanel = FindDirectChild("Settings Panel");
            if (informationPanel == null)
                informationPanel = FindDirectChild("Information Panel");

            HideDirectChild("Information Button");
            HidePanelChild(compactMenuPanel, "Information Button");

            if (compactMenuPanel != null)
            {
                EnsurePanelButton(compactMenuPanel, "Model", new Vector2(0.08f, 0.76f), new Vector2(0.84f, 0.16f), OpenModelMenu);
                EnsurePanelButton(compactMenuPanel, "Segmentation", new Vector2(0.08f, 0.58f), new Vector2(0.84f, 0.16f), OpenSegmentationMenu);
                EnsurePanelButton(compactMenuPanel, "Settings", new Vector2(0.08f, 0.40f), new Vector2(0.84f, 0.16f), OpenSettingsPanel);
                EnsurePanelButton(compactMenuPanel, "Information", new Vector2(0.08f, 0.22f), new Vector2(0.84f, 0.16f), OpenInformationMenu);
                EnsurePanelButton(compactMenuPanel, "Reset Placement", new Vector2(0.08f, 0.04f), new Vector2(0.84f, 0.14f), ResetPlacement);
            }

            if (segmentationMenuPanel == null)
            {
                segmentationMenuPanel = CreateRuntimePanel(root, "Segmentation Menu", new Vector2(0.04f, 0.13f), new Vector2(0.32f, 0.38f));
            }
            SetAnchors(segmentationMenuPanel.GetComponent<RectTransform>(), new Vector2(0.04f, 0.13f), new Vector2(0.32f, 0.38f));
            EnsurePanelButton(segmentationMenuPanel, "Couinaud Segments", new Vector2(0.08f, 0.72f), new Vector2(0.84f, 0.16f), OpenCouinaudSegmentsPanel);
            EnsurePanelButton(segmentationMenuPanel, "Blood Vessel", new Vector2(0.08f, 0.50f), new Vector2(0.84f, 0.16f), OpenVesselsPanel);
            EnsurePanelButton(segmentationMenuPanel, "Tumor", new Vector2(0.08f, 0.28f), new Vector2(0.84f, 0.16f), OpenTumorPanel);
            EnsurePanelButton(segmentationMenuPanel, "Back", new Vector2(0.08f, 0.06f), new Vector2(0.84f, 0.16f), ToggleMenu);
            HidePanelChild(segmentationMenuPanel, "Vessels Button");

            if (couinaudSegmentsPanel == null)
                couinaudSegmentsPanel = CreateRuntimePanel(root, "Couinaud Segments Panel", new Vector2(0.04f, 0.13f), new Vector2(0.32f, 0.58f));
            EnsurePanelButton(couinaudSegmentsPanel, "Show All", new Vector2(0.08f, 0.18f), new Vector2(0.40f, 0.10f), ShowAllSegments);
            EnsurePanelButton(couinaudSegmentsPanel, "Hide All", new Vector2(0.52f, 0.18f), new Vector2(0.40f, 0.10f), HideAllSegments);
            EnsurePanelButton(couinaudSegmentsPanel, "Close", new Vector2(0.30f, 0.04f), new Vector2(0.40f, 0.10f), ClosePanels);

            if (vesselPanel == null)
            {
                vesselPanel = CreateRuntimePanel(root, "Vessel Panel", new Vector2(0.04f, 0.13f), new Vector2(0.32f, 0.58f));
                CreateRuntimeButton(vesselPanel.transform, "Show All", new Vector2(0.08f, 0.18f), new Vector2(0.40f, 0.10f), ShowAllVessels);
                CreateRuntimeButton(vesselPanel.transform, "Hide All", new Vector2(0.52f, 0.18f), new Vector2(0.40f, 0.10f), HideAllVessels);
                CreateRuntimeButton(vesselPanel.transform, "Close", new Vector2(0.30f, 0.04f), new Vector2(0.40f, 0.10f), ClosePanels);
            }

            if (tumorPanel == null)
            {
                tumorPanel = CreateRuntimePanel(root, "Tumor Panel", new Vector2(0.04f, 0.13f), new Vector2(0.32f, 0.58f));
                CreateRuntimeButton(tumorPanel.transform, "Show All", new Vector2(0.08f, 0.18f), new Vector2(0.40f, 0.10f), ShowAllTumors);
                CreateRuntimeButton(tumorPanel.transform, "Hide All", new Vector2(0.52f, 0.18f), new Vector2(0.40f, 0.10f), HideAllTumors);
                CreateRuntimeButton(tumorPanel.transform, "Close", new Vector2(0.30f, 0.04f), new Vector2(0.40f, 0.10f), ClosePanels);
            }

            if (transparencyPanel == null)
            {
                transparencyPanel = CreateRuntimePanel(root, "Transparency Panel", new Vector2(0.34f, 0.37f), new Vector2(0.34f, 0.14f));
                transparencyTitleText = CreateRuntimeText(transparencyPanel.transform, "Transparency Title", "Opacity: no selection", 14, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.94f));
                selectedOpacitySlider = CreateRuntimeSlider(transparencyPanel.transform, "Selected Opacity", new Vector2(0.08f, 0.20f), new Vector2(0.84f, 0.22f), 0f, 1f, 1f);
            }
            else
            {
                SetAnchors(transparencyPanel.GetComponent<RectTransform>(), new Vector2(0.34f, 0.34f), new Vector2(0.34f, 0.22f));
                if (transparencyTitleText != null)
                    SetTextAnchors(transparencyTitleText, new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.94f));
                if (selectedOpacitySlider != null)
                    SetAnchors(selectedOpacitySlider.GetComponent<RectTransform>(), new Vector2(0.08f, 0.43f), new Vector2(0.84f, 0.15f));
            }

            EnsureDetailPanels();
        }

        void EnsureDetailPanels()
        {
            var root = transform;
            if (informationPanel == null)
            {
                informationPanel = CreateRuntimePanel(root, "Information Panel", new Vector2(0.50f, 0.08f), new Vector2(0.46f, 0.84f));
            }
            SetAnchors(informationPanel.GetComponent<RectTransform>(), new Vector2(0.50f, 0.08f), new Vector2(0.46f, 0.84f));

            if (compactInformationPanel == null)
                compactInformationPanel = CreateRuntimePanel(root, "Compact Information Panel", new Vector2(0.55f, 0.16f), new Vector2(0.40f, 0.30f));

            EnsurePanelButton(informationPanel, "Close", new Vector2(0.32f, 0.05f), new Vector2(0.36f, 0.14f), CloseInformationPanel);

            if (transparencyPanel == null)
            {
                transparencyPanel = CreateRuntimePanel(root, "Transparency Panel", new Vector2(0.34f, 0.34f), new Vector2(0.34f, 0.22f));
                transparencyTitleText = CreateRuntimeText(transparencyPanel.transform, "Transparency Title", "Opacity: no selection", 14, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.94f));
                selectedOpacitySlider = CreateRuntimeSlider(transparencyPanel.transform, "Selected Opacity", new Vector2(0.08f, 0.43f), new Vector2(0.84f, 0.15f), 0f, 1f, 1f);
            }

            if (selectedOpacitySlider == null && transparencyPanel != null)
                selectedOpacitySlider = transparencyPanel.GetComponentInChildren<Slider>(true);
            if (selectedOpacitySlider == null && transparencyPanel != null)
                selectedOpacitySlider = CreateRuntimeSlider(transparencyPanel.transform, "Selected Opacity", new Vector2(0.08f, 0.43f), new Vector2(0.84f, 0.15f), 0f, 1f, 1f);
            BindSelectedOpacitySlider();

            EnsurePanelButton(transparencyPanel, "Reset", new Vector2(0.08f, 0.10f), new Vector2(0.36f, 0.18f), ResetSelectedTransparency);
            EnsurePanelButton(transparencyPanel, "Close", new Vector2(0.56f, 0.10f), new Vector2(0.36f, 0.18f), () => SetPanelActive(transparencyPanel, null));
            EnsurePanelButton(settingsPanel, "User Manual", new Vector2(0.08f, 0.08f), new Vector2(0.40f, 0.12f), OpenUserManual);
        }

        void BindSelectedOpacitySlider()
        {
            if (selectedOpacitySlider == null)
                return;

            selectedOpacitySlider.onValueChanged.RemoveListener(OnTransparencyChanged);
            selectedOpacitySlider.onValueChanged.AddListener(OnTransparencyChanged);
        }

        GameObject FindDirectChild(string childName)
        {
            var child = transform.Find(childName);
            return child != null ? child.gameObject : null;
        }

        void HideDirectChild(string childName)
        {
            var child = transform.Find(childName);
            if (child != null)
                child.gameObject.SetActive(false);
        }

        static void HidePanelChild(GameObject panel, string childName)
        {
            if (panel == null)
                return;

            var child = panel.transform.Find(childName);
            if (child != null)
                child.gameObject.SetActive(false);
        }

        Button EnsurePanelButton(GameObject panel, string label, Vector2 anchorMin, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            if (panel == null)
                return null;

            var buttonName = label + " Button";
            var existing = panel.transform.Find(buttonName)?.GetComponent<Button>();
            if (existing != null)
            {
                SetAnchors(existing.GetComponent<RectTransform>(), anchorMin, size);
                existing.onClick.RemoveAllListeners();
                existing.onClick.AddListener(action);
                existing.gameObject.SetActive(true);
                return existing;
            }

            return CreateRuntimeButton(panel.transform, label, anchorMin, size, action);
        }

        void EnsureCameraBackgroundToggle()
        {
            if (settingsPanel == null)
                return;

            EnsureSettingsPanelLayout();

            if (cameraBackgroundToggle == null)
                cameraBackgroundToggle = CreateRuntimeToggle(settingsPanel.transform, "Camera Background", new Vector2(0.08f, 0.48f), new Vector2(0.84f, 0.13f));

            EnsureToggleVisual(cameraBackgroundToggle, "Camera Background", null);
            if (hapticToggle != null)
                EnsureToggleVisual(hapticToggle, "Haptic Feedback", null);
        }

        void BindSceneButtons()
        {
            if (buttonsBound)
                return;

            BindButton("Place Liver Button", PlaceLiver);
            BindButton("Menu Button", ToggleMenu);
            BindButton("Use Virtual Surface Button", UseVirtualSurface);
            BindButton("Model Button", SelectNormalModel);
            BindButton("Normal Liver Button", SelectNormalModel);
            BindButton("Disease Liver Button", SelectDiseaseModel);
            BindButton("Segments Button", OpenCouinaudSegmentsPanel);
            BindButton("Isolate Button", IsolateSelected);
            BindButton("Reset Settings Button", ResetSettings);
            BindButton("Reset Segments Button", ResetAppearance);

            BindPanelButton(compactMenuPanel, "Model Button", OpenModelMenu);
            BindPanelButton(compactMenuPanel, "Segmentation Button", OpenSegmentationMenu);
            BindPanelButton(compactMenuPanel, "Information Button", OpenInformationMenu);
            BindPanelButton(compactMenuPanel, "Settings Button", OpenSettingsPanel);
            BindPanelButton(compactMenuPanel, "Reset Placement Button", ResetPlacement);
            BindPanelButton(segmentationMenuPanel, "Couinaud Segments Button", OpenCouinaudSegmentsPanel);
            BindPanelButton(segmentationMenuPanel, "Blood Vessel Button", OpenVesselsPanel);
            BindPanelButton(segmentationMenuPanel, "Vessels Button", OpenVesselsPanel);
            BindPanelButton(segmentationMenuPanel, "Back Button", ToggleMenu);
            BindPanelButton(couinaudSegmentsPanel, "Show All Button", ShowAllSegments);
            BindPanelButton(couinaudSegmentsPanel, "Hide All Button", HideAllSegments);
            BindPanelButton(couinaudSegmentsPanel, "Close Button", ClosePanels);
            BindPanelButton(vesselPanel, "Show All Button", ShowAllVessels);
            BindPanelButton(vesselPanel, "Hide All Button", HideAllVessels);
            BindPanelButton(vesselPanel, "Close Button", ClosePanels);
            BindPanelButton(settingsPanel, "Back Button", ClosePanels);
            BindPanelButton(settingsPanel, "User Manual Button", OpenUserManual);
            BindPanelButton(informationPanel, "Close Button", () => SetPanelActive(informationPanel, null));
            BindPanelButton(transparencyPanel, "Reset Button", ResetSelectedTransparency);
            BindPanelButton(transparencyPanel, "Close Button", () => SetPanelActive(transparencyPanel, null));
            buttonsBound = true;
        }

        static void BindPanelButton(GameObject panel, string objectName, UnityEngine.Events.UnityAction action)
        {
            if (panel == null)
                return;

            var button = panel.transform.Find(objectName)?.GetComponent<Button>();
            if (button == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        void BindButton(string objectName, UnityEngine.Events.UnityAction action)
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button.gameObject.name != objectName)
                    continue;

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(action);
            }
        }

        void HideObsoleteControls()
        {
            var obsoleteNames = new[]
            {
                "Show All Button", "Hide All Button", "Reset Look Button", "Reset Model Button",
                "Reset AR Button", "Clear Button", "Show Sel Button", "Hide Sel Button", "Colour Button"
            };

            foreach (var obsoleteName in obsoleteNames)
            {
                var child = transform.Find(obsoleteName);
                if (child != null)
                    child.gameObject.SetActive(false);
            }
        }

        static GameObject CreateRuntimePanel(Transform parent, string name, Vector2 anchorMin, Vector2 size)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            image.color = new Color(0.08f, 0.09f, 0.10f, 0.84f);
            SetAnchors(obj.GetComponent<RectTransform>(), anchorMin, size);
            obj.SetActive(false);
            return obj;
        }

        static Button CreateRuntimeButton(Transform parent, string label, Vector2 anchorMin, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var obj = new GameObject(label + " Button");
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            image.color = new Color(0.08f, 0.11f, 0.14f, 0.86f);
            var button = obj.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            SetAnchors(obj.GetComponent<RectTransform>(), anchorMin, size);
            CreateRuntimeText(obj.transform, "Label", label, 15, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            return button;
        }

        static void SetRuntimeButtonFontSize(Button button, int fontSize)
        {
            var label = button != null ? button.GetComponentInChildren<Text>(true) : null;
            if (label != null)
                label.fontSize = fontSize;
        }

        static Text CreateRuntimeText(Transform parent, string name, string value, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            var rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }

        static void SetTextAnchors(Text text, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (text == null)
                return;

            var rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static Slider CreateRuntimeSlider(Transform parent, string name, Vector2 anchorMin, Vector2 size, float min, float max, float value)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var background = obj.AddComponent<Image>();
            background.color = new Color(0.25f, 0.28f, 0.30f, 0.95f);
            var slider = obj.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            SetAnchors(obj.GetComponent<RectTransform>(), anchorMin, size);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(obj.transform, false);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.22f, 0.78f, 0.45f, 1f);
            SetAnchors(fill.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(obj.transform, false);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = Color.white;
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0f);
            handleRect.anchorMax = new Vector2(0.5f, 1f);
            handleRect.sizeDelta = new Vector2(28f, 0f);
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            return slider;
        }

        static Toggle CreateRuntimeToggle(Transform parent, string label, Vector2 anchorMin, Vector2 size)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(parent, false);
            var toggle = obj.AddComponent<Toggle>();
            SetAnchors(obj.GetComponent<RectTransform>(), anchorMin, size);
            EnsureToggleVisual(toggle, label, null);
            return toggle;
        }

        void EnsureSettingsPanelLayout()
        {
            SetAnchorsIfPresent(settingsPanel.transform, "Camera Background", new Vector2(0.08f, 0.48f), new Vector2(0.84f, 0.13f));
            SetAnchorsIfPresent(settingsPanel.transform, "Haptic Feedback", new Vector2(0.08f, 0.32f), new Vector2(0.84f, 0.13f));
            SetAnchorsIfPresent(settingsPanel.transform, "Reset Settings Button", new Vector2(0.08f, 0.12f), new Vector2(0.40f, 0.13f));
            SetAnchorsIfPresent(settingsPanel.transform, "Back Button", new Vector2(0.54f, 0.12f), new Vector2(0.38f, 0.13f));

            if (hapticToggle != null)
                SetAnchors(hapticToggle.GetComponent<RectTransform>(), new Vector2(0.08f, 0.32f), new Vector2(0.84f, 0.13f));
            if (cameraBackgroundToggle != null)
                SetAnchors(cameraBackgroundToggle.GetComponent<RectTransform>(), new Vector2(0.08f, 0.48f), new Vector2(0.84f, 0.13f));
        }

        void RebuildSegmentToggles()
        {
            RebuildAnatomyToggles(couinaudSegmentsPanel, "Segment Toggle Rows", AnatomyCategory.LiverSegment, segmentToggles, FormatSegmentButtonLabel);
        }

        void RebuildVesselToggles()
        {
            RebuildAnatomyToggles(vesselPanel, "Vessel Toggle Rows", AnatomyCategory.Vessel, vesselToggles, FormatVesselButtonLabel);
        }

        void RebuildTumorToggles()
        {
            RebuildAnatomyToggles(tumorPanel, "Tumor Toggle Rows", AnatomyCategory.Lesion, tumorToggles, FormatTumorButtonLabel);
        }

        void RebuildAnatomyToggles(GameObject panel, string containerName, AnatomyCategory category, List<Toggle> toggles, System.Func<AnatomyPart, int, string> labelFactory)
        {
            var manager = CurrentAnatomyManager;
            if (panel == null || manager == null)
                return;

            var existingContainer = panel.transform.Find(containerName);
            if (existingContainer != null)
                Destroy(existingContainer.gameObject);

            toggles.Clear();
            var anatomyParts = new List<AnatomyPart>();
            foreach (var part in manager.Parts)
            {
                if (part != null && part.Category == category)
                    anatomyParts.Add(part);
            }

            anatomyParts.Sort((left, right) => string.Compare(left.StructureId, right.StructureId, System.StringComparison.OrdinalIgnoreCase));
            if (anatomyParts.Count == 0)
                return;

            var container = new GameObject(containerName);
            container.transform.SetParent(panel.transform, false);
            SetAnchors(container.AddComponent<RectTransform>(), new Vector2(0.06f, 0.30f), new Vector2(0.88f, 0.64f));

            var rowHeight = Mathf.Min(0.10f, 0.92f / anatomyParts.Count);
            for (var index = 0; index < anatomyParts.Count; index++)
            {
                var part = anatomyParts[index];
                var y = 0.96f - rowHeight * (index + 1);
                var label = labelFactory(part, index);
                var toggle = CreateRuntimeToggle(container.transform, label, new Vector2(0f, y), new Vector2(1f, rowHeight * 0.86f));
                EnsureToggleVisual(toggle, label, part.DefaultColor);
                toggle.SetIsOnWithoutNotify(part.IsVisible);
                UpdateToggleStatusText(toggle);
                toggle.onValueChanged.AddListener(isOn =>
                {
                    part.SetVisible(isOn);
                    if (!isOn && manager.SelectedPart == part)
                        manager.ClearSelection();
                    UpdateToggleStatusText(toggle);
                });
                toggles.Add(toggle);
            }
        }

        void SetCategoryVisible(AnatomyManager manager, AnatomyCategory category, bool visible, List<Toggle> toggles)
        {
            if (manager != null)
            {
                foreach (var part in manager.Parts)
                {
                    if (part != null && part.Category == category)
                        part.SetVisible(visible);
                }
            }

            SetTogglesOn(toggles, visible);
        }

        static void SetTogglesOn(List<Toggle> toggles, bool isOn)
        {
            foreach (var toggle in toggles)
            {
                toggle.SetIsOnWithoutNotify(isOn);
                UpdateToggleStatusText(toggle);
            }
        }

        static string FormatSegmentButtonLabel(AnatomyPart part, int index)
        {
            if (part != null && !string.IsNullOrWhiteSpace(part.StructureId) && part.StructureId.StartsWith("segment-"))
            {
                var suffix = part.StructureId.Substring("segment-".Length);
                if (int.TryParse(suffix, out var number))
                    return $"Segment {number}";
            }

            return $"Segment {index + 1}";
        }

        static string FormatVesselButtonLabel(AnatomyPart part, int index)
        {
            return part != null && !string.IsNullOrWhiteSpace(part.DisplayName) ? part.DisplayName : $"Vessel {index + 1}";
        }

        static string FormatTumorButtonLabel(AnatomyPart part, int index)
        {
            return part != null && !string.IsNullOrWhiteSpace(part.DisplayName) ? part.DisplayName : $"Tumor {index + 1}";
        }

        static void EnsureToggleVisual(Toggle toggle, string label, Color? swatchColor)
        {
            if (toggle == null)
                return;

            var rootImage = toggle.GetComponent<Image>();
            if (rootImage == null)
                rootImage = toggle.gameObject.AddComponent<Image>();
            rootImage.color = new Color(0.08f, 0.11f, 0.14f, 0.88f);
            toggle.targetGraphic = rootImage;

            EnsureChildImage(toggle.transform, "Toggle Box", new Color(0.18f, 0.23f, 0.27f, 1f), new Vector2(0.77f, 0.20f), new Vector2(0.90f, 0.80f));
            var check = EnsureChildImage(toggle.transform, "Checkmark", new Color(0.26f, 0.86f, 0.42f, 1f), new Vector2(0.795f, 0.28f), new Vector2(0.875f, 0.72f));
            toggle.graphic = check;

            var labelText = EnsureChildText(toggle.transform, "Label", label, 16, TextAnchor.MiddleLeft, new Vector2(swatchColor.HasValue ? 0.16f : 0.04f, 0f), new Vector2(0.72f, 1f));
            labelText.raycastTarget = false;
            labelText.color = Color.white;

            var status = EnsureChildText(toggle.transform, "Status", toggle.isOn ? "ON" : "OFF", 12, TextAnchor.MiddleCenter, new Vector2(0.90f, 0f), new Vector2(1f, 1f));
            status.raycastTarget = false;

            if (swatchColor.HasValue)
                EnsureChildImage(toggle.transform, "Colour", swatchColor.Value, new Vector2(0.04f, 0.28f), new Vector2(0.12f, 0.72f));
        }

        static Image EnsureChildImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var obj = new GameObject(name);
                obj.transform.SetParent(parent, false);
                child = obj.transform;
            }

            var image = child.GetComponent<Image>();
            if (image == null)
                image = child.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        static Text EnsureChildText(Transform parent, string name, string value, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var obj = new GameObject(name);
                obj.transform.SetParent(parent, false);
                child = obj.transform;
            }

            var text = child.GetComponent<Text>();
            if (text == null)
                text = child.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            var rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }

        static void UpdateToggleStatusText(Toggle toggle)
        {
            if (toggle == null)
                return;

            var status = toggle.transform.Find("Status")?.GetComponent<Text>();
            if (status != null)
                status.text = toggle.isOn ? "ON" : "OFF";
        }

        void EnsureModelMessageBackground()
        {
            if (modelMessageText == null || modelMessageBackground != null)
                return;

            var backgroundObject = new GameObject("Model Message Background");
            backgroundObject.transform.SetParent(modelMessageText.transform.parent, false);
            modelMessageBackground = backgroundObject.AddComponent<Image>();
            modelMessageBackground.color = new Color(0.12f, 0.13f, 0.14f, 0.88f);
            var messageRect = modelMessageText.rectTransform;
            var backgroundRect = modelMessageBackground.rectTransform;
            backgroundRect.anchorMin = messageRect.anchorMin;
            backgroundRect.anchorMax = messageRect.anchorMax;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            backgroundObject.transform.SetSiblingIndex(modelMessageText.transform.GetSiblingIndex());
            modelMessageText.transform.SetAsLastSibling();
            modelMessageBackground.gameObject.SetActive(false);
            modelMessageText.gameObject.SetActive(false);
        }

        static void SetAnchorsIfPresent(Transform parent, string childName, Vector2 anchorMin, Vector2 size)
        {
            var child = parent.Find(childName);
            if (child != null)
                SetAnchors(child.GetComponent<RectTransform>(), anchorMin, size);
        }

        static void SetChildActive(Transform parent, string childName, bool active)
        {
            var child = parent.Find(childName);
            if (child != null)
                child.gameObject.SetActive(active);
        }

        static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMin + size;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void EnsureInputSystemUiModule()
        {
            var eventSystems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            EventSystem eventSystem = null;
            foreach (var candidate in eventSystems)
            {
                if (eventSystem == null)
                {
                    eventSystem = candidate;
                    eventSystem.gameObject.SetActive(true);
                    continue;
                }

                Destroy(candidate.gameObject);
            }

            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            var inputSystemModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputSystemModule == null)
                inputSystemModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

            if (inputSystemModule.actionsAsset == null)
                inputSystemModule.AssignDefaultActions();
            inputSystemModule.enabled = true;

            var standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (standaloneModule != null)
                Destroy(standaloneModule);
        }

        void AutoWireMissingReferences()
        {
            if (anatomyManager == null)
                anatomyManager = FindAnyObjectByType<AnatomyManager>();
            if (modelWorkspace == null)
                modelWorkspace = LiverModelWorkspace.GetOrCreate(Camera.main);
            if (transparencyController == null)
                transparencyController = FindAnyObjectByType<TransparencyController>();
            if (modelInteractionController == null)
                modelInteractionController = FindAnyObjectByType<ModelInteractionController>();
            if (sessionResetController == null)
                sessionResetController = FindAnyObjectByType<ARSessionResetController>();
            if (placementController == null)
                placementController = FindAnyObjectByType<ARPlacementController>();
            if (modelSwitcher == null)
                modelSwitcher = FindAnyObjectByType<LiverModelSwitcher>();
            if (backgroundController == null)
                backgroundController = FindAnyObjectByType<ARBackgroundController>();
            if (backgroundController == null)
                backgroundController = gameObject.AddComponent<ARBackgroundController>();
            if (patientGlbLoader == null)
                patientGlbLoader = FindAnyObjectByType<RuntimePatientGlbLoader>();
            if (patientGlbLoader == null)
                patientGlbLoader = gameObject.AddComponent<RuntimePatientGlbLoader>();

            EnsureAnatomyInteractionController();
        }

        void EnsureAnatomyInteractionController()
        {
            var interaction = FindAnyObjectByType<AnatomyInteractionController>();
            var selectionController = FindAnyObjectByType<AnatomySelectionController>();
            if (interaction == null)
            {
                var host = selectionController != null ? selectionController.gameObject : modelInteractionController != null ? modelInteractionController.gameObject : gameObject;
                interaction = host.AddComponent<AnatomyInteractionController>();
            }

            var camera = Camera.main;
            if (camera == null)
                camera = FindAnyObjectByType<Camera>();

            interaction.Configure(camera, anatomyManager, this, modelInteractionController);
        }
    }
}
