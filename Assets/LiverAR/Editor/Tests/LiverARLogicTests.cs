using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using LiverAR.Runtime;

namespace LiverAR.Tests.EditMode
{
    public sealed class LiverARLogicTests
    {
        [Test]
        public void ScaleLimitsClampUniformScale()
        {
            Assert.That(ModelInteractionController.ClampScale(0.01f, 0.05f, 2f), Is.EqualTo(0.05f));
            Assert.That(ModelInteractionController.ClampScale(6f, 0.05f, 5f), Is.EqualTo(5f));
            Assert.That(ModelInteractionController.ClampScale(0.75f, 0.05f, 2f), Is.EqualTo(0.75f));
        }

        [Test]
        public void CameraDistanceClampKeepsModelInReach()
        {
            Assert.That(ModelInteractionController.ClampDistance(0.1f, 0.35f, 3f), Is.EqualTo(0.35f));
            Assert.That(ModelInteractionController.ClampDistance(4f, 0.35f, 3f), Is.EqualTo(3f));
        }

        [Test]
        public void VerticalClampKeepsModelInConfiguredRange()
        {
            Assert.That(ModelInteractionController.ClampVertical(3f, -0.6f, 0.8f), Is.EqualTo(0.8f));
            Assert.That(ModelInteractionController.ClampVertical(-2f, -0.6f, 0.8f), Is.EqualTo(-0.6f));
        }

        [Test]
        public void CameraRelativeTranslationMovesRootAndPreservesRotationAndScale()
        {
            var cameraObject = new GameObject("camera");
            var camera = cameraObject.AddComponent<Camera>();
            var model = new GameObject("model");
            model.transform.SetPositionAndRotation(new Vector3(0f, 0f, 1f), Quaternion.Euler(10f, 20f, 30f));
            model.transform.localScale = Vector3.one * 0.4f;
            var controller = CreateModelInteractionController(model.transform);

            controller.TranslateCameraRelative(new Vector2(100f, 50f), camera);

            Assert.That(model.transform.position.x, Is.EqualTo(0.15f).Within(0.001f));
            Assert.That(model.transform.position.y, Is.EqualTo(0.075f).Within(0.001f));
            Assert.That(model.transform.position.z, Is.EqualTo(1f).Within(0.001f));
            Assert.That(model.transform.rotation, Is.EqualTo(Quaternion.Euler(10f, 20f, 30f)));
            Assert.That(model.transform.localScale, Is.EqualTo(Vector3.one * 0.4f));

            Object.DestroyImmediate(controller.gameObject);
            Object.DestroyImmediate(model);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void TwoFingerAverageMovementRotatesRootWithoutTranslationOrScale()
        {
            var cameraObject = new GameObject("camera");
            var camera = cameraObject.AddComponent<Camera>();
            var model = new GameObject("model");
            model.transform.position = new Vector3(0f, 0f, 1f);
            model.transform.localScale = Vector3.one * 0.4f;
            var controller = CreateModelInteractionController(model.transform);
            var initialPosition = model.transform.position;
            var initialScale = model.transform.localScale;

            controller.ApplyTwoFingerTransform(
                new Vector2(100f, 100f),
                new Vector2(200f, 100f),
                new Vector2(140f, 100f),
                new Vector2(240f, 100f),
                camera);

            Assert.That(model.transform.position, Is.EqualTo(initialPosition));
            Assert.That(model.transform.localScale, Is.EqualTo(initialScale));
            Assert.That(Quaternion.Angle(Quaternion.identity, model.transform.rotation), Is.GreaterThan(0.1f));

            Object.DestroyImmediate(controller.gameObject);
            Object.DestroyImmediate(model);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void TwoFingerRotationKeepsRootPositionFixedForOffsetMeshes()
        {
            var cameraObject = new GameObject("camera");
            var camera = cameraObject.AddComponent<Camera>();
            var root = new GameObject("model root");
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(2f, 0f, 0f);
            var controller = CreateModelInteractionController(root.transform);
            var positionBefore = root.transform.position;

            controller.ApplyTwoFingerTransform(
                new Vector2(100f, 100f),
                new Vector2(200f, 100f),
                new Vector2(140f, 100f),
                new Vector2(240f, 100f),
                camera);

            Assert.That(root.transform.position, Is.EqualTo(positionBefore));
            Assert.That(Quaternion.Angle(Quaternion.identity, root.transform.rotation), Is.GreaterThan(0.1f));

            Object.DestroyImmediate(controller.gameObject);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void LiverModelRootCentersOffsetVisualsAtItsOwnPivot()
        {
            var root = new GameObject("liver root");
            root.transform.position = new Vector3(3f, 1f, 2f);
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(2f, 0f, 0f);
            var liverRoot = root.AddComponent<LiverModelRoot>();

            liverRoot.Initialize(1, "Normal Liver 1");

            Assert.That(Vector3.Distance(visual.GetComponent<Renderer>().bounds.center, root.transform.position), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(root.transform.position, new Vector3(3f, 1f, 2f)), Is.LessThan(0.0001f));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void TwoFingerPinchScalesRootWithoutTranslation()
        {
            var cameraObject = new GameObject("camera");
            var camera = cameraObject.AddComponent<Camera>();
            var model = new GameObject("model");
            model.transform.position = new Vector3(0f, 0f, 1f);
            var controller = CreateModelInteractionController(model.transform);
            var initialPosition = model.transform.position;

            controller.ApplyTwoFingerTransform(
                new Vector2(100f, 100f),
                new Vector2(200f, 100f),
                new Vector2(80f, 100f),
                new Vector2(220f, 100f),
                camera);

            Assert.That(model.transform.position, Is.EqualTo(initialPosition));
            Assert.That(model.transform.localScale.x, Is.GreaterThan(1f));

            Object.DestroyImmediate(controller.gameObject);
            Object.DestroyImmediate(model);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void DepthConstraintKeepsPitchedCameraForwardDistanceAndVerticalOffsetBounded()
        {
            var cameraObject = new GameObject("camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.rotation = Quaternion.Euler(85f, 0f, 0f);
            var model = new GameObject("model");
            model.transform.position = camera.transform.forward;
            var controller = CreateModelInteractionController(model.transform);
            SetModelInteractionField(controller, "minVerticalOffset", 0f);
            SetModelInteractionField(controller, "maxVerticalOffset", 0f);

            controller.AdjustDepth(1000f, camera);

            var forwardDistance = Vector3.Dot(model.transform.position - camera.transform.position, camera.transform.forward);
            Assert.That(forwardDistance, Is.EqualTo(3f).Within(0.001f));
            Assert.That(model.transform.position.y - camera.transform.position.y, Is.EqualTo(0f).Within(0.001f));

            Object.DestroyImmediate(controller.gameObject);
            Object.DestroyImmediate(model);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void AnatomyPartVisibilityControlsRenderersAndColliders()
        {
            var root = new GameObject("segment");
            var renderer = root.AddComponent<MeshRenderer>();
            var collider = root.AddComponent<BoxCollider>();
            var part = root.AddComponent<AnatomyPart>();
            part.Configure("segment-i", "Segment I", AnatomyCategory.LiverSegment, Color.red, new[] { renderer });

            part.SetVisible(false);

            Assert.That(renderer.enabled, Is.False);
            Assert.That(collider.enabled, Is.False);
            Assert.That(part.IsVisible, Is.False);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void TransparencyValuesAreClamped()
        {
            Assert.That(TransparencyController.ClampOpacity(-0.5f), Is.EqualTo(0f));
            Assert.That(TransparencyController.ClampOpacity(1.5f), Is.EqualTo(1f));
            Assert.That(TransparencyController.ClampOpacity(0.35f), Is.EqualTo(0.35f));
        }

        [Test]
        public void ResetOpacityRestoresSelectedPartToOpaque()
        {
            var part = CreatePart("segment-1", "Segment 1");
            part.SetOpacity(0.35f);

            part.ResetOpacity();

            Assert.That(part.Opacity, Is.EqualTo(1f));

            Object.DestroyImmediate(part.gameObject);
        }

        [Test]
        public void ZeroOpacityHidesAnatomyPartButKeepsItsOpacityState()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var renderer = root.GetComponent<Renderer>();
            var collider = root.GetComponent<Collider>();
            var part = root.AddComponent<AnatomyPart>();
            part.Configure("segment-2", "Segment II", AnatomyCategory.LiverSegment, Color.green, new[] { renderer });

            part.SetOpacity(0f);

            Assert.That(part.Opacity, Is.EqualTo(0f));
            Assert.That(renderer.enabled, Is.False);
            Assert.That(collider.enabled, Is.False);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void DuplicateRootCopiesEachPartsVisibilityAndOpacity()
        {
            var source = CreateLiverRootWithParts("source");
            var sourceSegment = source.AnatomyManager.TryGetPart("segment-1", out var segment) ? segment : null;
            var sourceVessel = source.AnatomyManager.TryGetPart("vessel", out var vessel) ? vessel : null;
            sourceSegment.SetVisible(false);
            sourceSegment.SetOpacity(0.4f);
            sourceVessel.SetOpacity(0.65f);

            var duplicate = CreateLiverRootWithParts("duplicate");
            duplicate.CopyRuntimeStateFrom(source);

            Assert.That(duplicate.AnatomyManager.TryGetPart("segment-1", out var copiedSegment), Is.True);
            Assert.That(copiedSegment.IsVisible, Is.False);
            Assert.That(copiedSegment.Opacity, Is.EqualTo(0.4f));
            Assert.That(duplicate.AnatomyManager.TryGetPart("vessel", out var copiedVessel), Is.True);
            Assert.That(copiedVessel.Opacity, Is.EqualTo(0.65f));

            Object.DestroyImmediate(source.gameObject);
            Object.DestroyImmediate(duplicate.gameObject);
        }

        [Test]
        public void SelectingPartRendersOnlyThatPartsSelectionOutline()
        {
            var root = new GameObject("liver root");
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(root.transform, false);
            var part = visual.AddComponent<AnatomyPart>();
            part.Configure("segment-1", "Segment I", AnatomyCategory.LiverSegment, Color.red, new[] { visual.GetComponent<Renderer>() });
            var manager = root.AddComponent<AnatomyManager>();
            manager.SetConfiguredParts(new[] { part });

            manager.Select(part);

            var outline = System.Array.Find(root.GetComponentsInChildren<MeshRenderer>(true), renderer => renderer.gameObject.name == "Selection Outline");
            Assert.That(outline, Is.Not.Null);
            Assert.That(outline.enabled, Is.True);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void AnatomyManagerSelectionReplacesPreviousSelection()
        {
            var managerObject = new GameObject("manager");
            var manager = managerObject.AddComponent<AnatomyManager>();
            var a = CreatePart("segment-i", "Segment I");
            var b = CreatePart("portal-vein", "Portal Vein");

            manager.Register(a);
            manager.Register(b);
            manager.Select(a);
            manager.Select(b);

            Assert.That(a.IsSelected, Is.False);
            Assert.That(b.IsSelected, Is.True);
            Assert.That(manager.SelectedPart, Is.EqualTo(b));

            Object.DestroyImmediate(a.gameObject);
            Object.DestroyImmediate(b.gameObject);
            Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void SelectingAnatomyPartPreservesItsBaseColour()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var renderer = root.GetComponent<Renderer>();
            var part = root.AddComponent<AnatomyPart>();
            var expected = new Color(0.2f, 0.6f, 0.9f, 1f);
            part.Configure("segment-v", "Segment V", AnatomyCategory.LiverSegment, expected, new[] { renderer });

            part.SetSelected(true);

            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetColor("_BaseColor"), Is.EqualTo(expected));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void FocusSegmentSelectsWithoutChangingOtherSegmentOpacity()
        {
            var managerObject = new GameObject("manager");
            var manager = managerObject.AddComponent<AnatomyManager>();
            var first = CreatePart("segment-i", "Segment I");
            var second = CreatePart("segment-ii", "Segment II");
            manager.SetConfiguredParts(new[] { first, second });
            first.SetOpacity(0.7f);
            second.SetOpacity(0.45f);

            manager.FocusSegment(first);

            Assert.That(first.IsSelected, Is.True);
            Assert.That(first.Opacity, Is.EqualTo(0.7f));
            Assert.That(second.Opacity, Is.EqualTo(0.45f));

            Object.DestroyImmediate(first.gameObject);
            Object.DestroyImmediate(second.gameObject);
            Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void SurfaceStatusMessagesStayShortAndMatchPlacementFlow()
        {
            Assert.That(ARSurfaceStatusMessage.GetMessage(ARSurfaceState.Initialising), Is.EqualTo("Starting AR..."));
            Assert.That(ARSurfaceStatusMessage.GetMessage(ARSurfaceState.Searching), Is.EqualTo("Move your phone slowly to detect a flat surface."));
            Assert.That(ARSurfaceStatusMessage.GetMessage(ARSurfaceState.Ready), Is.EqualTo("Surface detected. Tap Place Liver."));
            Assert.That(ARSurfaceStatusMessage.GetMessage(ARSurfaceState.VirtualMode), Is.EqualTo("Virtual surface ready. Tap Place Liver."));
            Assert.That(ARSurfaceStatusMessage.GetMessage(ARSurfaceState.VirtualEnvironment), Is.EqualTo("Virtual environment active."));
            Assert.That(ARSurfaceStatusMessage.GetMessage(ARSurfaceState.Placed), Is.EqualTo(string.Empty));
        }

        [Test]
        public void PlacementVisibilityRequiresEnabledRendererInCameraView()
        {
            var cameraObject = new GameObject("camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = Vector3.zero;
            camera.transform.rotation = Quaternion.identity;

            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.transform.position = new Vector3(0f, 0f, 2f);
            model.transform.localScale = Vector3.one * 0.2f;

            var visible = ARPlacementController.TryValidateVisibleModel(model, camera, out var reason);

            Assert.That(visible, Is.True, reason);

            Object.DestroyImmediate(model);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void PlacementVisibilityRejectsModelBehindCamera()
        {
            var cameraObject = new GameObject("camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = Vector3.zero;
            camera.transform.rotation = Quaternion.identity;

            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.transform.position = new Vector3(0f, 0f, -2f);

            var visible = ARPlacementController.TryValidateVisibleModel(model, camera, out var reason);

            Assert.That(visible, Is.False);
            Assert.That(reason, Does.Contain("front"));

            Object.DestroyImmediate(model);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void AnatomyManagerCanSwitchBetweenWholeLiverAndSegmentViews()
        {
            var managerObject = new GameObject("manager");
            var manager = managerObject.AddComponent<AnatomyManager>();
            var whole = CreatePart("whole-liver", "Whole Liver", AnatomyCategory.WholeLiver);
            var segment = CreatePart("segment-i", "Segment 1", AnatomyCategory.LiverSegment);

            manager.SetConfiguredParts(new[] { whole, segment });
            manager.ShowWholeLiverOverview();

            Assert.That(whole.IsVisible, Is.True);
            Assert.That(segment.IsVisible, Is.False);

            manager.ShowLiverSegments();

            Assert.That(whole.IsVisible, Is.False);
            Assert.That(segment.IsVisible, Is.True);

            Object.DestroyImmediate(whole.gameObject);
            Object.DestroyImmediate(segment.gameObject);
            Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void LiverSettingsClampInteractionValues()
        {
            var settings = LiverARSettings.CreateDefault();

            settings.SetInteractionSensitivity(-1f);
            settings.SetRotationSpeed(10f);
            settings.SetScaleSensitivity(0.01f);

            Assert.That(settings.InteractionSensitivity, Is.EqualTo(0.2f));
            Assert.That(settings.RotationSpeed, Is.EqualTo(5f));
            Assert.That(settings.ScaleSensitivity, Is.EqualTo(0.1f));
        }

        [Test]
        public void PatientGlbMetadataAcceptsExport2Contract()
        {
            var metadata = PatientModelImportContract.ParseMetadata(
                "{\"formatVersion\":1,\"units\":\"mm\",\"coordinateSystem\":{\"source\":\"LPS\",\"unityConversion\":\"metadata-defined\"},\"glbFile\":\"patient.glb\",\"glbRootNode\":\"PatientModelRoot\",\"models\":[{\"name\":\"Segment_I\",\"id\":\"Segment_I\",\"displayName\":\"Segment I\",\"file\":\"Segment_I.obj\",\"role\":\"anatomy\"}]}" );

            Assert.That(metadata.glbFile, Is.EqualTo("patient.glb"));
            Assert.That(metadata.Models[0].DisplayName, Is.EqualTo("Segment I"));
        }

        [Test]
        public void TumorOptionAppearsOnlyWhenTheImportedModelContainsATumor()
        {
            var managerObject = new GameObject("manager");
            var manager = managerObject.AddComponent<AnatomyManager>();
            var controller = CreateUiController(manager);

            controller.OpenSegmentationMenu();

            var tumorButton = GetPrivateField<GameObject>(controller, "segmentationMenuPanel").transform.Find("Tumor Button").gameObject;
            Assert.That(tumorButton.activeSelf, Is.False);

            var tumor = CreatePart("tumor", "Tumor", AnatomyCategory.Lesion);
            manager.Register(tumor);
            controller.OpenSegmentationMenu();

            Assert.That(tumorButton.activeSelf, Is.True);

            DestroyUiTestObjects(controller, managerObject, tumor.gameObject);
        }

        [Test]
        public void ImportedTumorStartsHidden()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var tumor = root.AddComponent<AnatomyPart>();
            tumor.Configure("tumor", "Tumor", AnatomyCategory.Lesion, Color.red, new[] { root.GetComponent<Renderer>() });

            RuntimePatientGlbLoader.ApplyInitialVisibility(tumor);

            Assert.That(tumor.IsVisible, Is.False);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void PatientMetadataRoleClassifiesBloodVesselsAsVessels()
        {
            var vessel = new PatientModelEntry { name = "BloodVessels", role = "vessels" };

            var category = RuntimePatientGlbLoader.ResolveCategory(vessel);

            Assert.That(category, Is.EqualTo(AnatomyCategory.Vessel));
        }

        [Test]
        public void ImportedSegmentsUseStableDistinctColours()
        {
            var segmentI = RuntimePatientGlbLoader.GetImportedPartColor(new PatientModelEntry { name = "Segment_I", id = "Segment_I", role = "anatomy" });
            var segmentII = RuntimePatientGlbLoader.GetImportedPartColor(new PatientModelEntry { name = "Segment_II", id = "Segment_II", role = "anatomy" });

            Assert.That(segmentI, Is.Not.EqualTo(segmentII));
            Assert.That(segmentI, Is.Not.EqualTo(new Color(0.72f, 0.12f, 0.10f, 1f)));
        }

        [Test]
        public void ImportedBloodVesselsUseDarkPurple()
        {
            var colour = RuntimePatientGlbLoader.GetImportedPartColor(new PatientModelEntry { name = "BloodVessels", role = "vessels" });

            Assert.That(colour.r, Is.LessThan(0.4f));
            Assert.That(colour.b, Is.GreaterThan(colour.r));
        }

        [Test]
        public void StudyAndCompactInformationPanelsStaySeparate()
        {
            var managerObject = new GameObject("manager");
            var manager = managerObject.AddComponent<AnatomyManager>();
            var part = CreatePart("segment-i", "Segment I", AnatomyCategory.LiverSegment);
            manager.Register(part);
            manager.Select(part);
            var controller = CreateUiController(manager);

            controller.OpenInformationPanelForSelection();

            var compactPanel = GetPrivateField<GameObject>(controller, "compactInformationPanel");
            Assert.That(compactPanel, Is.Not.Null);
            Assert.That(compactPanel.activeSelf, Is.True);
            Assert.That(GetPrivateField<GameObject>(controller, "informationPanel").activeSelf, Is.False);

            controller.OpenInformationMenu();

            Assert.That(compactPanel.activeSelf, Is.False);
            Assert.That(GetPrivateField<GameObject>(controller, "informationPanel").activeSelf, Is.True);

            DestroyUiTestObjects(controller, managerObject, part.gameObject);
        }

        [Test]
        public void InformationTextOmitsEducationalNotesAndSources()
        {
            var record = new AnatomyInformationRecord
            {
                DisplayName = "Liver",
                Overview = "Overview text.",
                Location = "Location text.",
                BloodSupply = "Blood supply text.",
                VenousDrainage = "Drainage text.",
                Function = "Function text.",
                Description = "Educational text.",
                Source = "Source text."
            };

            var displayText = record.ToDisplayText();

            Assert.That(displayText, Does.Not.Contain("Educational notes"));
            Assert.That(displayText, Does.Not.Contain("Educational text."));
            Assert.That(displayText, Does.Not.Contain("Source"));
            Assert.That(displayText, Does.Not.Contain("Source text."));
        }

        [Test]
        public void ModelSwitcherPreservesTransformAndAvoidsDuplicates()
        {
            var root = new GameObject("placement-root").transform;
            root.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 45f, 0f));
            root.localScale = Vector3.one * 0.4f;

            var normal = new GameObject("normal-liver");
            var disease = new GameObject("disease-liver");
            normal.transform.SetParent(root, false);
            disease.transform.SetParent(root, false);

            var switcher = root.gameObject.AddComponent<LiverModelSwitcher>();
            switcher.ConfigureForTests(normal, disease);

            Assert.That(switcher.SwitchTo(LiverModelType.Normal), Is.True);
            Assert.That(switcher.SwitchTo(LiverModelType.Disease), Is.True);

            Assert.That(normal.activeSelf, Is.False);
            Assert.That(disease.activeSelf, Is.True);
            Assert.That(root.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(root.rotation.eulerAngles.y, Is.EqualTo(45f).Within(0.01f));
            Assert.That(root.localScale, Is.EqualTo(Vector3.one * 0.4f));
            Assert.That(root.childCount, Is.EqualTo(2));

            Object.DestroyImmediate(root.gameObject);
        }

        [Test]
        public void BackgroundToggleKeepsRenderingCameraEnabledInVirtualWorkspace()
        {
            var cameraObject = new GameObject("camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = Color.magenta;
            var controller = cameraObject.AddComponent<ARBackgroundController>();
            controller.ConfigureForTests(camera, null);

            var disabled = controller.SetCameraBackgroundEnabled(false);

            Assert.That(disabled, Is.True);
            Assert.That(camera.enabled, Is.True);
            Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(controller.IsCameraBackgroundEnabled, Is.False);

            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void NavigationPanelsDoNotForceCloseDetailOverlays()
        {
            Assert.That(AnatomyUiPanelState.ShouldCloseDetailOverlayOnNavigationChange(true), Is.False);
            Assert.That(AnatomyUiPanelState.ShouldCloseDetailOverlayOnNavigationChange(false), Is.True);
        }

        [Test]
        public void RuntimeMenuExposesHierarchyNavigationApis()
        {
            var controllerType = typeof(ARUIController);

            Assert.That(controllerType.GetMethod("OpenSegmentationMenu", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
            Assert.That(controllerType.GetMethod("OpenCouinaudSegmentsPanel", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
            Assert.That(controllerType.GetMethod("OpenVesselsPanel", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
            Assert.That(controllerType.GetMethod("OpenInformationPanelForSelection", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
            Assert.That(controllerType.GetMethod("OpenTransparencyPanelForSelection", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
        }

        [Test]
        public void SelectingAnatomyPartLeavesOverlayChoiceToInteractionController()
        {
            var managerObject = new GameObject("manager");
            var manager = managerObject.AddComponent<AnatomyManager>();
            var part = CreatePart("segment-i", "Segment I");
            manager.Register(part);
            var controller = CreateUiController(manager);

            manager.Select(part);

            Assert.That(GetPrivateField<GameObject>(controller, "informationPanel").activeSelf, Is.False);

            controller.OpenInformationPanelForSelection();

            Assert.That(GetPrivateField<GameObject>(controller, "informationPanel").activeSelf, Is.True);

            DestroyUiTestObjects(controller, managerObject, part.gameObject);
        }

        [Test]
        public void SegmentationMenuOnlyShowsVesselsWhenVesselExists()
        {
            var managerObject = new GameObject("manager");
            var manager = managerObject.AddComponent<AnatomyManager>();
            var controller = CreateUiController(manager);

            controller.OpenSegmentationMenu();

            var vesselsButton = GetPrivateField<GameObject>(controller, "segmentationMenuPanel").transform.Find("Blood Vessel Button").gameObject;
            Assert.That(vesselsButton.activeSelf, Is.False);

            var vessel = CreatePart("portal-vein", "Portal Vein", AnatomyCategory.Vessel);
            manager.Register(vessel);
            controller.OpenSegmentationMenu();

            Assert.That(vesselsButton.activeSelf, Is.True);

            DestroyUiTestObjects(controller, managerObject, vessel.gameObject);
        }

        [Test]
        public void VesselShowAllButtonOnlyShowsVessels()
        {
            var managerObject = new GameObject("manager");
            var manager = managerObject.AddComponent<AnatomyManager>();
            var segment = CreatePart("segment-i", "Segment I");
            var vessel = CreatePart("portal-vein", "Portal Vein", AnatomyCategory.Vessel);
            manager.Register(segment);
            manager.Register(vessel);
            var controller = CreateUiController(manager);
            segment.SetVisible(false);
            vessel.SetVisible(false);

            controller.OpenVesselsPanel();
            GetPrivateField<GameObject>(controller, "vesselPanel").transform.Find("Show All Button").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

            Assert.That(vessel.IsVisible, Is.True);
            Assert.That(segment.IsVisible, Is.False);

            DestroyUiTestObjects(controller, managerObject, segment.gameObject, vessel.gameObject);
        }

        [Test]
        public void SegmentationBackButtonReturnsToMainMenu()
        {
            var managerObject = new GameObject("manager");
            var controller = CreateUiController(managerObject.AddComponent<AnatomyManager>());

            controller.OpenSegmentationMenu();
            GetPrivateField<GameObject>(controller, "segmentationMenuPanel").transform.Find("Back Button").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

            Assert.That(GetPrivateField<GameObject>(controller, "compactMenuPanel").activeSelf, Is.True);

            DestroyUiTestObjects(controller, managerObject);
        }

        [Test]
        public void SceneBinderKeepsVesselShowAllScopedToVesselPanel()
        {
            var managerObject = new GameObject("manager");
            var manager = managerObject.AddComponent<AnatomyManager>();
            var segment = CreatePart("segment-i", "Segment I");
            var vessel = CreatePart("portal-vein", "Portal Vein", AnatomyCategory.Vessel);
            manager.Register(segment);
            manager.Register(vessel);
            var controller = CreateUiController(manager);
            var vesselShowAll = GetPrivateField<GameObject>(controller, "vesselPanel").transform.Find("Show All Button").GetComponent<UnityEngine.UI.Button>();
            vesselShowAll.onClick.RemoveAllListeners();
            RebindSceneButtons(controller);
            segment.SetVisible(false);
            vessel.SetVisible(false);

            vesselShowAll.onClick.Invoke();

            Assert.That(vessel.IsVisible, Is.True);
            Assert.That(segment.IsVisible, Is.False);

            DestroyUiTestObjects(controller, managerObject, segment.gameObject, vessel.gameObject);
        }

        [Test]
        public void SceneBinderKeepsCouinaudShowAllScopedToSegmentPanel()
        {
            var managerObject = new GameObject("manager");
            var manager = managerObject.AddComponent<AnatomyManager>();
            var segment = CreatePart("segment-i", "Segment I");
            var vessel = CreatePart("portal-vein", "Portal Vein", AnatomyCategory.Vessel);
            manager.Register(segment);
            manager.Register(vessel);
            var controller = CreateUiController(manager);
            var segmentShowAll = GetPrivateField<GameObject>(controller, "couinaudSegmentsPanel").transform.Find("Show All Button").GetComponent<UnityEngine.UI.Button>();
            segmentShowAll.onClick.RemoveAllListeners();
            RebindSceneButtons(controller);
            segment.SetVisible(false);
            vessel.SetVisible(false);

            segmentShowAll.onClick.Invoke();

            Assert.That(segment.IsVisible, Is.True);
            Assert.That(vessel.IsVisible, Is.False);

            DestroyUiTestObjects(controller, managerObject, segment.gameObject, vessel.gameObject);
        }

        [Test]
        public void SceneBinderReturnsSegmentationBackToMainMenu()
        {
            var managerObject = new GameObject("manager");
            var controller = CreateUiController(managerObject.AddComponent<AnatomyManager>());
            var back = GetPrivateField<GameObject>(controller, "segmentationMenuPanel").transform.Find("Back Button").GetComponent<UnityEngine.UI.Button>();
            back.onClick.RemoveAllListeners();
            RebindSceneButtons(controller);
            controller.OpenSegmentationMenu();

            back.onClick.Invoke();

            Assert.That(GetPrivateField<GameObject>(controller, "compactMenuPanel").activeSelf, Is.True);

            DestroyUiTestObjects(controller, managerObject);
        }

        [Test]
        public void SceneBinderClosesInformationWithoutClosingNavigation()
        {
            var managerObject = new GameObject("manager");
            var controller = CreateUiController(managerObject.AddComponent<AnatomyManager>());
            var informationPanel = GetPrivateField<GameObject>(controller, "informationPanel");
            var close = informationPanel.transform.Find("Close Button").GetComponent<UnityEngine.UI.Button>();
            close.onClick.RemoveAllListeners();
            RebindSceneButtons(controller);
            controller.ToggleMenu();
            controller.OpenInformationPanelForSelection();

            close.onClick.Invoke();

            Assert.That(informationPanel.activeSelf, Is.False);
            Assert.That(GetPrivateField<GameObject>(controller, "compactMenuPanel").activeSelf, Is.True);

            DestroyUiTestObjects(controller, managerObject);
        }

        [Test]
        public void GestureClassifierPrefersDragOverLongPressAfterMovementThreshold()
        {
            var state = AnatomyGestureClassifier.ClassifySingleTouch(
                elapsedSeconds: 1.2f,
                movementPixels: 18f,
                dragThresholdPixels: 12f,
                longPressSeconds: 1f,
                longPressTolerancePixels: 10f,
                startedOnPart: true);

            Assert.That(state, Is.EqualTo(AnatomyGestureState.Dragging));
        }

        [Test]
        public void GestureClassifierTriggersLongPressOnlyInsideTolerance()
        {
            var state = AnatomyGestureClassifier.ClassifySingleTouch(
                elapsedSeconds: 1.05f,
                movementPixels: 4f,
                dragThresholdPixels: 12f,
                longPressSeconds: 1f,
                longPressTolerancePixels: 10f,
                startedOnPart: true);

            Assert.That(state, Is.EqualTo(AnatomyGestureState.LongPressTriggered));
        }

        [Test]
        public void LegacySelectionYieldsWhenCoordinatedControllerExists()
        {
            var root = new GameObject("interaction-root");
            var legacySelection = root.AddComponent<AnatomySelectionController>();

            Assert.That(legacySelection.IsSelectionHandlingEnabled, Is.True);

            root.AddComponent<AnatomyInteractionController>();

            Assert.That(legacySelection.IsSelectionHandlingEnabled, Is.False);

            Object.DestroyImmediate(root);
        }

        static AnatomyPart CreatePart(string id, string displayName, AnatomyCategory category = AnatomyCategory.LiverSegment)
        {
            var root = new GameObject(displayName);
            var renderer = root.AddComponent<MeshRenderer>();
            var part = root.AddComponent<AnatomyPart>();
            part.Configure(id, displayName, category, Color.white, new[] { renderer });
            return part;
        }

        static LiverModelRoot CreateLiverRootWithParts(string name)
        {
            var root = new GameObject(name);
            CreatePart("segment-1", "Segment I").transform.SetParent(root.transform, false);
            CreatePart("vessel", "Blood Vessel", AnatomyCategory.Vessel).transform.SetParent(root.transform, false);
            var liverRoot = root.AddComponent<LiverModelRoot>();
            liverRoot.Initialize(1, name);
            return liverRoot;
        }

        static ModelInteractionController CreateModelInteractionController(Transform modelRoot)
        {
            var controllerObject = new GameObject("model-interaction");
            controllerObject.SetActive(false);
            var controller = controllerObject.AddComponent<ModelInteractionController>();
            controller.ModelRoot = modelRoot;
            return controller;
        }

        static void SetModelInteractionField(ModelInteractionController controller, string name, object value)
        {
            typeof(ModelInteractionController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, value);
        }

        static ARUIController CreateUiController(AnatomyManager anatomyManager)
        {
            var root = new GameObject("ui");
            root.SetActive(false);
            var controller = root.AddComponent<ARUIController>();
            SetPrivateField(controller, "anatomyManager", anatomyManager);
            root.SetActive(true);
            return controller;
        }

        static T GetPrivateField<T>(object target, string name) where T : class
        {
            return (T)typeof(ARUIController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        static void SetPrivateField(object target, string name, object value)
        {
            typeof(ARUIController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        static void RebindSceneButtons(ARUIController controller)
        {
            SetPrivateField(controller, "buttonsBound", false);
            typeof(ARUIController).GetMethod("BindSceneButtons", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
        }

        static void DestroyUiTestObjects(ARUIController controller, params GameObject[] otherObjects)
        {
            Object.DestroyImmediate(controller.gameObject);
            foreach (var otherObject in otherObjects)
                Object.DestroyImmediate(otherObject);
        }
    }
}
