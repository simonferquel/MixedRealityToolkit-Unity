﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.Toolkit.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using MRConfig = Microsoft.MixedReality.Toolkit.Utilities.Editor.MixedRealityProjectConfigurator.Configurations;

namespace Microsoft.MixedReality.Toolkit.Utilities.Editor
{

    public class MixedRealityProjectConfiguratorWindow : EditorWindow
    {
        private const float Default_Window_Height = 500.0f;
        private const float Default_Window_Width = 300.0f;
        private const string XRPipelineDocsUrl = "https://";
        private const string XRSDKUnityDocsUrl = "https://";
        private const string MRFTDocsUrl = "https://docs.microsoft.com/windows/mixed-reality/develop/unity/welcome-to-mr-feature-tool";
        private readonly GUIContent ApplyButtonContent = new GUIContent("Apply", "Apply configurations to this Unity Project");
        private readonly GUIContent SkipButtonContent = new GUIContent("Skip", "Skip to next step");
        private readonly GUIContent LaterButtonContent = new GUIContent("Later", "Do not show this pop-up notification until next session");
        private readonly GUIContent IgnoreButtonContent = new GUIContent("Ignore", "Modify this preference under Edit > Project Settings > Mixed Reality Toolkit");

        private static ConfigurationStage currentStage
        {
            get => MixedRealityProjectPreferences.ConfiguratorState;
            set => MixedRealityProjectPreferences.ConfiguratorState = value;
        }

        public static MixedRealityProjectConfiguratorWindow Instance { get; private set; }

        public static bool IsOpen => Instance != null;

        private static bool? isTMPEssentialsImported = null;
#if UNITY_2019_3_OR_NEWER
        private static bool? isMRTKExamplesPackageImportedViaUPM = null;
#endif

        private void OnEnable()
        {
            Instance = this;
            EditorApplication.projectChanged += resetNullableBoolState;
#if UNITY_2019_3_OR_NEWER
            CompilationPipeline.compilationStarted += CompilationPipeline_compilationStarted;
#else
            CompilationPipeline.assemblyCompilationStarted += CompilationPipeline_compilationStarted;
#endif // UNITY_2019_3_OR_NEWER

            MixedRealityProjectConfigurator.SelectedSpatializer = SpatializerUtilities.CurrentSpatializer;
        }

        private void CompilationPipeline_compilationStarted(object obj)
        {
            resetNullableBoolState();
            // There should be only one pop-up window which is generally tracked by IsOpen
            // However, when recompiling, Unity will call OnDestroy for this window but not actually destroy the editor window
            // This ensure we have a clean close on recompiles when this EditorWindow was open beforehand
            //ShowWindow();
        }

        private static void resetNullableBoolState()
        {
            isMRTKExamplesPackageImportedViaUPM = null;
            isTMPEssentialsImported = null;
        }

        [MenuItem("Ut/Configure Demo", false, 499)]
        public static void ShowWindowFromMenu()
        {
            currentStage = ConfigurationStage.Init;
            ShowWindow();
        }

        public static void ShowWindowOnInit()
        {
            if (!IsOpen && currentStage == ConfigurationStage.Done)
            {
                currentStage = ConfigurationStage.ProjectConfiguration;
            }

            ShowWindow();
        }

        public static void ShowWindow()
        {
            // There should be only one configurator window open as a "pop-up". If already open, then just force focus on our instance
            if (IsOpen)
            {
                Instance.Focus();
            }
            else
            {
                var window = CreateInstance<MixedRealityProjectConfiguratorWindow>();
                window.titleContent = new GUIContent("MRTK Project Configurator", EditorGUIUtility.IconContent("_Popup").image);
                window.position = new Rect(Screen.width / 2.0f, Screen.height / 2.0f, Default_Window_Height, Default_Window_Width);
                window.ShowUtility();
            }
        }

        private void OnGUI()
        {
            MixedRealityInspectorUtility.RenderMixedRealityToolkitLogo();
            if (currentStage != ConfigurationStage.Done)
            {
                EditorGUILayout.LabelField("Welcome to MRTK!", MixedRealityStylesUtility.BoldLargeTitleStyle);
                createSpace(5);
                EditorGUILayout.LabelField("This configurator will go through some settings to make sure the project is ready for MRTK.");
                createSpace(20);
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            }


            switch (currentStage)
            {
                case ConfigurationStage.Init:
                    RenderXRPipelineSelection();
                    break;
                case ConfigurationStage.SelectXRSDKPlugin:
                    RenderSelectXRSDKPlugin();
                    break;
                case ConfigurationStage.InstallOpenXR:
                    RenderEnableOpenXRPlugin();
                    break;
                case ConfigurationStage.InstallMSOpenXR:
                    RenderEnableMicrosoftOpenXRPlugin();
                    break;
                case ConfigurationStage.InstallBuiltinPlugin:
                    RenderEnableXRSDKBuiltinPlugin();
                    break;
                case ConfigurationStage.ProjectConfiguration:
                    RenderProjectConfigurations();
                    break;
                case ConfigurationStage.ImportTMP:
                    RenderImportTMP();
                    break;
                case ConfigurationStage.ShowExamples:
                    RenderShowUPMExamples();
                    break;
                case ConfigurationStage.Done:
                    RenderConfigurationCompleted();
                    break;
                default:
                    break;
            }
        }

        private void RenderXRPipelineSelection()
        {
            if (!XRSettingsUtilities.XREnabled)
            {
                RenderNoPipeline();
            }
            else if (XRSettingsUtilities.LegacyXREnabled)
            {
                RenderLegacyXRPipelineDetected();
            }
            else
            {
                if (XRSettingsUtilities.MicrosoftOpenXREnabled)
                {
                    RenderMicrosoftOpenXRPipelineDetected();
                }
                else if (XRSettingsUtilities.OpenXREnabled)
                {
                    RenderOpenXRPipelineDetected();
                }
                else
                {
                    RenderXRSDKBuiltinPluginPipelineDetected();
                }
            }
        }

        private void RenderNoPipeline()
        {
            if (!XRSettingsUtilities.LegacyXRAvailable)
            {
#if UNITY_2020_2_OR_NEWER
                currentStage = ConfigurationStage.SelectXRSDKPlugin;
#else
                currentStage = ConfigurationStage.InstallBuiltinPlugin;
#endif // UNITY_2020_2_OR_NEWER

                Repaint();
            }
            EditorGUILayout.LabelField("XR Pipeline Setting", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("To build applications targeting AR/VR headsets you need to specify an XR pipeline. "
#if UNITY_2019_3_OR_NEWER
                + $"Unity currently provides the following pipelines in this version ({Application.unityVersion}). Please choose the one you would like to use. "
#else
                +$"Unity currently provides the Legacy XR pipeline in this version ({Application.unityVersion}). Please click on the Enable Legacy XR button if you are targeting AR/VR headsets. "
#endif // UNITY_2019_3_OR_NEWER
                + "You may also skip this step and configure manually later. "
                + $"More information can be found at {XRPipelineDocsUrl}", EditorStyles.wordWrappedLabel);
            createSpace(15);
            using (new EditorGUILayout.HorizontalScope())
            {
#if UNITY_2019_3_OR_NEWER
                if (GUILayout.Button("Legacy XR"))
#else
                if (GUILayout.Button("Enable Legacy XR"))
#endif // UNITY_2019_3_OR_NEWER

                {
                    XRSettingsUtilities.LegacyXREnabled = true;
                }

#if UNITY_2019_3_OR_NEWER
                if (GUILayout.Button("XR SDK/XR Management (Recommended)"))
                {
                    currentStage = ConfigurationStage.InstallBuiltinPlugin;
                    Repaint();
                }
#endif
                if (GUILayout.Button("Learn more"))
                {
                    Application.OpenURL(XRPipelineDocsUrl);
                }
            }
            RenderSetupLaterSection(true, () => {
                currentStage = ConfigurationStage.ProjectConfiguration;
                Repaint();
            });
        }

        private void RenderLegacyXRPipelineDetected()
        {
            EditorGUILayout.LabelField("XR Pipeline Setting - LegacyXR in use", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("To build applications targeting AR/VR headsets you need to specify an XR pipeline. "
                + $"\n\nThe LegacyXR pipeline is detected in the project. Please be aware that the LegacyXR pipeline is deprecated in Unity 2019 and is removed in Unity 2020."
                + $"\n\nFor more information on alternative pipelines, please visit {XRPipelineDocsUrl}", EditorStyles.wordWrappedLabel);
            createSpace(15);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Next"))
                {
                    currentStage = ConfigurationStage.ProjectConfiguration;
                    Repaint();
                }
                if (GUILayout.Button("Learn more"))
                {
                    Application.OpenURL(XRPipelineDocsUrl);
                }
            }
            RenderSetupLaterSection();
        }

        private void RenderMicrosoftOpenXRPipelineDetected()
        {
            EditorGUILayout.LabelField("XR Pipeline Setting - XR SDK with Unity + Microsoft OpenXR plugins in use", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("To build applications targeting AR/VR headsets you need to specify an XR pipeline. "
                + $"\n\nThe XR SDK pipeline with Unity and Microsoft OpenXR plugins are detected in the project. You are good to go."
                + $"\n\nFor more information on alternative pipelines, please visit {XRPipelineDocsUrl}", EditorStyles.wordWrappedLabel);
            createSpace(15);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Next"))
                {
                    currentStage = ConfigurationStage.ProjectConfiguration;
                    Repaint();
                }
                if (GUILayout.Button("Learn more"))
                {
                    Application.OpenURL(XRPipelineDocsUrl);
                }

            }
            RenderSetupLaterSection();
        }

        private void RenderOpenXRPipelineDetected()
        {
            EditorGUILayout.LabelField("XR Pipeline Setting - XR SDK with Unity OpenXR plugin in use", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("To build applications targeting AR/VR headsets you need to specify an XR pipeline. "
                + $"\n\nThe XR SDK pipeline with Unity OpenXR plugin is detected in the project. You are good to go."
                + $"\n\nNote: If you are targeting HoloLens 2 or HP Reverb G2 headset you need to click on the Acquire Microsoft OpenXR plugin button and follow the instructions."
                + $"\n\nFor more information on alternative pipelines, please visit {XRPipelineDocsUrl}", EditorStyles.wordWrappedLabel);
            createSpace(15);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Next"))
                {
                    currentStage = ConfigurationStage.ProjectConfiguration;
                    Repaint();
                }
                if (GUILayout.Button("Acquire Microsoft OpenXR plugin"))
                {
                    currentStage = ConfigurationStage.InstallMSOpenXR;
                    Repaint();
                }
                if (GUILayout.Button("Learn more"))
                {
                    Application.OpenURL(XRPipelineDocsUrl);
                }

            }
            RenderSetupLaterSection();
        }

        private void RenderXRSDKBuiltinPluginPipelineDetected()
        {
            EditorGUILayout.LabelField("XR Pipeline Setting - XR SDK with builtin plugin in use", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("To build applications targeting AR/VR headsets you need to specify an XR pipeline. "
                + $"\n\nThe XR SDK pipeline with builtin plugin is detected in the project. You are good to go."
                + $"\n\nFor more information on alternative pipelines, please visit {XRPipelineDocsUrl}", EditorStyles.wordWrappedLabel);
            createSpace(15);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Next"))
                {
                    currentStage = ConfigurationStage.ProjectConfiguration;
                    Repaint();
                }
                if (GUILayout.Button("Learn more"))
                {
                    Application.OpenURL(XRPipelineDocsUrl);
                }
            }
            RenderSetupLaterSection();
        }

        private void RenderSelectXRSDKPlugin()
        {
            if (XRSettingsUtilities.XRSDKEnabled)
            {
                currentStage = ConfigurationStage.Init;
                Repaint();
            }
            EditorGUILayout.LabelField("XR Pipeline Setting - Enabling the XR SDK Pipeline", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("To build applications targeting AR/VR headsets you need to enable an XR pipeline. "
                + "With this pipeline there are two categories of provider plugins for the XR SDK pipeline:"
                + $"\n\nThe Unity OpenXR plugin (possibly along with vender-specific extension plugins) is recommended if you are targeting HoloLens 2 and/or Windows Mixed Reality (WMR) headsets."
                + "\nThe built-in plugins provided by Unity offers a wide range of supported devices, including HoloLens 2 and WMR headsets. "
                + $"\n\nMore information can be found at {XRPipelineDocsUrl}.", EditorStyles.wordWrappedLabel);
            createSpace(15);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use the Unity OpenXR plugin"))
                {

#if UNITY_2020_2_OR_NEWER
                    var request = UnityEditor.PackageManager.Client.Add("com.unity.xr.openxr");
                    while (!request.IsCompleted) { }
                    currentStage = ConfigurationStage.InstallOpenXR;
#endif // UNITY_2020_2_OR_NEWER
                    Repaint();
                }
                if (GUILayout.Button("Use built-in Unity plugins"))
                {
                    currentStage = ConfigurationStage.InstallBuiltinPlugin;
                    Repaint();
                }
                if (GUILayout.Button("Learn more"))
                {
                    Application.OpenURL(XRPipelineDocsUrl);
                }
            }
            RenderSetupLaterSection(true, () => {
                currentStage = ConfigurationStage.ProjectConfiguration;
                Repaint();
            });
        }

        private void RenderEnableXRSDKBuiltinPlugin()
        {
            if (XRSettingsUtilities.XRSDKEnabled)
            {
                currentStage = ConfigurationStage.Init;
                Repaint();
            }
            EditorGUILayout.LabelField("XR Pipeline Setting - Enabling the XR SDK Pipeline with built-in Plugins", EditorStyles.boldLabel);

            if (XRSettingsUtilities.XRManagementPresent)
            {
                EditorGUILayout.LabelField("To enable the XR SDK pipeline with built-in Plugins, first press the Show Settings button. "
                + $"\n\nIn the XR management plug-in window that shows up, check the plugin(s) you want to use based on your target device. "
                + "\n\nBe sure to switch to the correct build target (e.g. UWP, Windows standalone) tab first by clicking on the icon(s) right below the XR Plug-in Management title. "
                + $"After checking the desired plugin(s) click on the Next button to continue."
                + $"\n\nMore information can be found at {XRSDKUnityDocsUrl} (Only the first three steps are needed if following instructions on the page)", EditorStyles.wordWrappedLabel);
            }
            else
            {
                EditorGUILayout.LabelField("To enable the XR SDK pipeline with built-in Plugins, first press the Show Settings button. "
                + $"\n\nIn the XR management plug-in window that shows up, click on the install XR Plugin Management button. "
                + "After clicking on that button, please check the plugin(s) you want to use based on your target device. "
                + "\n\nBe sure to switch to the correct build target (e.g. UWP, Windows standalone) tab first by clicking on the icon(s) right below the XR Plug-in Management title. "
                + $"After checking the desired plugin(s) click on the Next button to continue."
                + $"\n\nMore information can be found at {XRSDKUnityDocsUrl} (Only the first three steps are needed if following instructions on the page)", EditorStyles.wordWrappedLabel);
            }
            
            createSpace(15);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Show Settings"))
                {

                    if (XRSettingsUtilities.XRManagementPresent)
                    {
                        SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
                    }
                    else
                    {
                        SettingsService.OpenProjectSettings("Project/XR Plugin Management");
                    }
                }
                if (GUILayout.Button("Learn more"))
                {
                    Application.OpenURL(XRSDKUnityDocsUrl);
                }
            }
            RenderSetupLaterSection(true, () => {
                currentStage = ConfigurationStage.ProjectConfiguration;
                Repaint();
            });
        }

        private void RenderEnableOpenXRPlugin()
        {
            if (XRSettingsUtilities.OpenXREnabled)
            {
                currentStage = ConfigurationStage.Init;
                Repaint();
            }
            EditorGUILayout.LabelField("XR Pipeline Setting - Enabling the XR SDK Pipeline with OpenXR", EditorStyles.boldLabel);
            //createSpace();
            EditorGUILayout.LabelField("To enable the XR SDK pipeline with OpenXR, first press the Show Settings button. "
                + $"\n\nIn the XR management plug-in window that shows up, please switch to the correct build target (e.g. UWP, Windows standalone) tab first by clicking on the icon(s) right below the XR Plug-in Management title. "
                + "Then please check the OpenXR plugin. A new page confirming the detection of OpenXR will be shown in place of this page once you finish the steps.", EditorStyles.wordWrappedLabel);
            createSpace(15);
            if (GUILayout.Button("Show XR Plug-in Management Settings"))
            {
                SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
            }
            
            RenderSetupLaterSection(true, () => {
                currentStage = ConfigurationStage.ProjectConfiguration;
                Repaint();
            });
        }

        private void RenderEnableMicrosoftOpenXRPlugin()
        {
            if (XRSettingsUtilities.MicrosoftOpenXREnabled)
            {
                currentStage = ConfigurationStage.Init;
                Repaint();
            }
            EditorGUILayout.LabelField("XR Pipeline Setting - Enabling the Microsoft OpenXR Plugin", EditorStyles.boldLabel);
            //createSpace();
            EditorGUILayout.LabelField("The Microsoft OpenXR plugin is required if you are targeting HoloLens 2 or HP Reverb G2 headset. You may skip this step if that is not the case for you."
                + $"\n\nFirst click on the Show XR Plug-in Management Settings button. In the window popping up/getting focus, switch to switch to the correct build target (i.e. UWP or Windows standalone) tab "
                + "by clicking on the icon(s) right below the XR Plug-in Management title. Then you should click on the question mark sign to the right of the \"Enable HoloLens 2 feature set\" chekcbox."
                + "\n\nFollow the \"Manual setup without MRTK\" section of the instructions as MRTK is already in the project. Also note you do not need to manually select MRTK in the feature tool no matter it is shown as installed or not."
                + "\n\nKeep this window and the Unity project open during the process. A new page confirming the detection of the Microsoft OpenXR plugin will be shown in place of this page once you finish the steps.", EditorStyles.wordWrappedLabel);
            createSpace(15);
            if (GUILayout.Button("Show XR Plug-in Management Settings"))
            {
                SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
            }
            RenderSetupLaterSection(true, () => {
                currentStage = ConfigurationStage.ProjectConfiguration;
                Repaint();
            });
        }

        private void RenderProjectConfigurations()
        {
            RenderConfigurations();

            if (!MixedRealityProjectConfigurator.IsProjectConfigured())
            {
                RenderChoiceDialog();
            }
            else
            {
                RenderConfiguredConfirmation();
            }
        }

        private void RenderConfiguredConfirmation()
        {
            const string dialogTitle = "Project Configuration Confirmed";
            const string dialogContent = "This Unity project is properly configured for the Mixed Reality Toolkit. All items shown above are using recommended settings.";

            createSpace(15);
            EditorGUILayout.LabelField(dialogTitle, EditorStyles.boldLabel);
            createSpace(15);
            EditorGUILayout.LabelField(dialogContent);

            createSpace(10);
            if (GUILayout.Button("Next"))
            {
                currentStage = ConfigurationStage.ImportTMP;
                Repaint();
            }
            RenderSetupLaterSection();
        }

        private void RenderChoiceDialog()
        {
            const string dialogTitle = "Apply Default Settings?";
            const string dialogContent = "The Mixed Reality Toolkit would like to auto-apply useful settings to this Unity project. Enabled options above will be applied to the project. Disabled items are already properly configured.";

            createSpace(15);
            EditorGUILayout.LabelField(dialogTitle, EditorStyles.boldLabel);
            createSpace(15);
            EditorGUILayout.LabelField(dialogContent);

            createSpace(10);
            if (GUILayout.Button(ApplyButtonContent))
            {
                ApplyConfigurations();
            }

            RenderSetupLaterSection(true, () => {
                currentStage = ConfigurationStage.ImportTMP;
                Repaint();
            });
        }

        private void RenderImportTMP()
        {
            if (TMPEssentialsImported())
            {
                currentStage = ConfigurationStage.ShowExamples;
                Repaint();
            }
            EditorGUILayout.LabelField("Importing TMP Essentials", EditorStyles.boldLabel);
            
            EditorGUILayout.LabelField("MRTK contains components that depend on TextMeshPro. It is recommended that you import TMP by clicking the Import TMP Essentials button below.", EditorStyles.wordWrappedLabel);
            createSpace(15);
            var m_ResourceImporter = new TMP_PackageResourceImporter();
            m_ResourceImporter.OnGUI();
            createSpace(15);
            RenderSetupLaterSection(true, () => {
                currentStage = ConfigurationStage.ShowExamples;
                Repaint();
            });
        }

        private void RenderShowUPMExamples()
        {
            if (!MRTKExamplesPackageImportedViaUPM())
            {
                currentStage = ConfigurationStage.Done;
                Repaint();
            }
            EditorGUILayout.LabelField("Locating MRTK Examples", EditorStyles.boldLabel);
            //createSpace();
            EditorGUILayout.LabelField("The MRTK Examples package includes samples to help you familiarize yourself with many core features. "
                + "\nSince you imported MRTK via MRFT/UPM the examples no longer show up in the Assets folder automatically. They are now located at Window (menu bar) -> Package Manager "
                + "-> Select In Project in the \"Packages:\" dropdown -> Mixed Reality Toolkit Examples", EditorStyles.wordWrappedLabel);
            createSpace(15);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Show me the examples"))
                {
#if UNITY_2019_3_OR_NEWER
                    UnityEditor.PackageManager.UI.Window.Open("Mixed Reality Toolkit Examples");
#endif
                }
                if (GUILayout.Button("Got it, next"))
                {
                    currentStage = ConfigurationStage.Done;
                    Repaint();
                }
            }
            createSpace(15);
            RenderSetupLaterSection();
        }

        private void RenderConfigurationCompleted()
        {
            EditorGUILayout.LabelField("MRTK Setup Completed!", MixedRealityStylesUtility.BoldLargeTitleStyle);
            createSpace(5);
            EditorGUILayout.LabelField("You have finished setting up the project for Mixed Reality Toolkit. You may go through this process again by clicking Mixed Reality Toolkit on the editor menu bar -> Ultilities -> Config."
                + $"\nIf there are certain settings not set according to the recommendation you may see this configurator popping up again. You may use the Ignore or Later button to suppress the behavior. "
                + "We hope you enjoy using MRTK. Please find the links to our documentation and API references below. If you encountered something looking like a bug please report by opening an issue in our repository. "
                + "\nThese links are accessible through Mixed Reality Toolkit on the editor menu bar -> Help "
                + $"After finishing the process in the feature tool come back here to verify whether the installation is successful. A new page should be shown if you succeeded."
                + $"\n\nMore information can be found at {MRFTDocsUrl}.", EditorStyles.wordWrappedLabel);
            createSpace(15);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Show MRTK Documentation"))
                {
                    Application.OpenURL(MRFTDocsUrl);
                }
                if (GUILayout.Button("Show MRTK API References"))
                {
                    Application.OpenURL(MRFTDocsUrl);
                }
                if (GUILayout.Button("Done"))
                {
                    Close();
                }
            }
        }

        private void RenderSetupLaterSection(bool showSkipButton = false, Action skipButtonAction = null)
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.LabelField("Not ready to setup the project now?", EditorStyles.boldLabel);
            //createSpace();
            EditorGUILayout.LabelField(showSkipButton ? "You may choose to skip this step, delay the setup until next session or ignore the setup unless reenabled." :
                "You may choose to delay the setup until next session or ignore the setup unless reenabled."
                , EditorStyles.wordWrappedLabel);
            createSpace(15);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (showSkipButton)
                {
                    if (GUILayout.Button(SkipButtonContent))
                    {
                        skipButtonAction();
                    }
                }
                
                if (GUILayout.Button(LaterButtonContent))
                {
                    MixedRealityEditorSettings.IgnoreProjectConfigForSession = true;
                    Close();
                }

                if (GUILayout.Button(IgnoreButtonContent))
                {
                    MixedRealityProjectPreferences.IgnoreSettingsPrompt = true;
                    Close();
                }
            }
            createSpace(15);
        }

        private bool TMPEssentialsImported()
        {
            if (isTMPEssentialsImported.HasValue)
            {
                return isTMPEssentialsImported.Value;
            }
            isTMPEssentialsImported = File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset");
            return isTMPEssentialsImported.Value;
        }

        private bool MRTKExamplesPackageImportedViaUPM()
        {
#if !UNITY_2019_3_OR_NEWER
            return false;
#else
            if (isMRTKExamplesPackageImportedViaUPM.HasValue)
            {
                return isMRTKExamplesPackageImportedViaUPM.Value;
            }
            
            var request = UnityEditor.PackageManager.Client.List(true, true);
            while (!request.IsCompleted) { }
            if (request.Result != null && request.Result.Any(p => p.displayName == "Mixed Reality Toolkit Examples"))
            {
                isMRTKExamplesPackageImportedViaUPM = true;
                return isMRTKExamplesPackageImportedViaUPM.Value;
            }
            
            isMRTKExamplesPackageImportedViaUPM = false;
            return isMRTKExamplesPackageImportedViaUPM.Value;

#endif // !UNITY_2019_3_OR_NEWER
        }
        
        private readonly Dictionary<MRConfig, bool> trackToggles = new Dictionary<MRConfig, bool>()
        {
            { MRConfig.ForceTextSerialization, true },
            { MRConfig.VisibleMetaFiles, true },
            { MRConfig.VirtualRealitySupported, true },
            { MRConfig.OptimalRenderingPath, true },
            { MRConfig.SpatialAwarenessLayer, true },
            { MRConfig.AudioSpatializer, true },

            // UWP Capabilities
            { MRConfig.MicrophoneCapability, true },
            { MRConfig.InternetClientCapability, true },
            { MRConfig.SpatialPerceptionCapability, true },
#if UNITY_2019_3_OR_NEWER
            { MRConfig.EyeTrackingCapability, true },
#endif // UNITY_2019_3_OR_NEWER

#if UNITY_2019_3_OR_NEWER
            { MRConfig.NewInputSystem, true },
#endif // UNITY_2019_3_OR_NEWER

            // Android Settings
            { MRConfig.AndroidMultiThreadedRendering, true },
            { MRConfig.AndroidMinSdkVersion, true },

            // iOS Settings
            { MRConfig.IOSMinOSVersion, true },
            { MRConfig.IOSArchitecture, true },
            { MRConfig.IOSCameraUsageDescription, true },

#if UNITY_2019_3_OR_NEWER
            // A workaround for the Unity bug described in https://github.com/microsoft/MixedRealityToolkit-Unity/issues/8326.
            { MRConfig.GraphicsJobWorkaround, true },
#endif // UNITY_2019_3_OR_NEWER
        };

        private const string None = "None";
        
        private Vector2 scrollPosition = Vector2.zero;

        public void RenderConfigurations()
        {
            using (var scrollView = new EditorGUILayout.ScrollViewScope(scrollPosition))
            {
                scrollPosition = scrollView.scrollPosition;
                EditorGUILayout.LabelField("Project Settings", EditorStyles.boldLabel);
                RenderToggle(MRConfig.ForceTextSerialization, "Force text asset serialization");
                RenderToggle(MRConfig.VisibleMetaFiles, "Enable visible meta files");
                if (!MixedRealityOptimizeUtils.IsBuildTargetAndroid() && !MixedRealityOptimizeUtils.IsBuildTargetIOS() && XRSettingsUtilities.XREnabled)
                {
#if !UNITY_2019_3_OR_NEWER
                    RenderToggle(MRConfig.VirtualRealitySupported, "Enable VR supported");
#endif // !UNITY_2019_3_OR_NEWER
                }
#if UNITY_2019_3_OR_NEWER
                if (XRSettingsUtilities.LegacyXREnabled)
                {
                    RenderToggle(MRConfig.OptimalRenderingPath, "Set Single Pass Instanced rendering path (legacy XR API)");
                }
#else
#if UNITY_ANDROID
                RenderToggle(MRConfig.OptimalRenderingPath, "Set Single Pass Stereo rendering path");
#else
                RenderToggle(MRConfig.OptimalRenderingPath, "Set Single Pass Instanced rendering path");
#endif
#endif // UNITY_2019_3_OR_NEWER
                RenderToggle(MRConfig.SpatialAwarenessLayer, "Set default Spatial Awareness layer");

#if UNITY_2019_3_OR_NEWER
                RenderToggle(MRConfig.NewInputSystem, "Enable old input system for input simulation (won't disable new input system)");
#endif // UNITY_2019_3_OR_NEWER

                PromptForAudioSpatializer();
                EditorGUILayout.Space();

                if (MixedRealityOptimizeUtils.IsBuildTargetUWP())
                {
                    EditorGUILayout.LabelField("UWP Capabilities", EditorStyles.boldLabel);
                    RenderToggle(MRConfig.MicrophoneCapability, "Enable Microphone Capability");
                    RenderToggle(MRConfig.InternetClientCapability, "Enable Internet Client Capability");
                    RenderToggle(MRConfig.SpatialPerceptionCapability, "Enable Spatial Perception Capability");
#if UNITY_2019_3_OR_NEWER
                    RenderToggle(MRConfig.EyeTrackingCapability, "Enable Eye Gaze Input Capability");
                    RenderToggle(MRConfig.GraphicsJobWorkaround, "Avoid Unity 'PlayerSettings.graphicsJob' crash");
#endif // UNITY_2019_3_OR_NEWER
                }
                else
                {
                    trackToggles[MRConfig.MicrophoneCapability] = false;
                    trackToggles[MRConfig.InternetClientCapability] = false;
                    trackToggles[MRConfig.SpatialPerceptionCapability] = false;
#if UNITY_2019_3_OR_NEWER
                    trackToggles[MRConfig.EyeTrackingCapability] = false;
                    trackToggles[MRConfig.GraphicsJobWorkaround] = false;
#endif // UNITY_2019_3_OR_NEWER
                }

                if (MixedRealityOptimizeUtils.IsBuildTargetAndroid())
                {
                    EditorGUILayout.LabelField("Android Settings", EditorStyles.boldLabel);
                    RenderToggle(MRConfig.AndroidMultiThreadedRendering, "Disable Multi-Threaded Rendering");
                    RenderToggle(MRConfig.AndroidMinSdkVersion, "Set Minimum API Level");
                }

                if (MixedRealityOptimizeUtils.IsBuildTargetIOS())
                {
                    EditorGUILayout.LabelField("iOS Settings", EditorStyles.boldLabel);
                    RenderToggle(MRConfig.IOSMinOSVersion, "Set Required OS Version");
                    RenderToggle(MRConfig.IOSArchitecture, "Set Required Architecture");
                    RenderToggle(MRConfig.IOSCameraUsageDescription, "Set Camera Usage Descriptions");
                }
            }
        }

        public void ApplyConfigurations()
        {
            var configurationFilter = new HashSet<MRConfig>();
            foreach (var item in trackToggles)
            {
                if (item.Value)
                {
                    configurationFilter.Add(item.Key);
                }
            }

            MixedRealityProjectConfigurator.ConfigureProject(configurationFilter);
        }

        /// <summary>
        /// Provide the user with the list of spatializers that can be selected.
        /// </summary>
        private void PromptForAudioSpatializer()
        {
            string selectedSpatializer = MixedRealityProjectConfigurator.SelectedSpatializer;
            List<string> spatializers = new List<string>
            {
                None
            };
            spatializers.AddRange(SpatializerUtilities.InstalledSpatializers);
            RenderDropDown(MRConfig.AudioSpatializer, "Audio spatializer:", spatializers.ToArray(), ref selectedSpatializer);
            MixedRealityProjectConfigurator.SelectedSpatializer = selectedSpatializer;
        }

        private void RenderDropDown(MRConfig configKey, string title, string[] collection, ref string selection)
        {
            bool configured = MixedRealityProjectConfigurator.IsConfigured(configKey);
            using (new EditorGUI.DisabledGroupScope(configured))
            {
                if (configured)
                {
                    EditorGUILayout.LabelField(new GUIContent($"{title} {selection}", InspectorUIUtility.SuccessIcon));
                }
                else
                {
                    int index = 0;
                    for (int i = 0; i < collection.Length; i++)
                    {
                        if (collection[i] != selection) { continue; }

                        index = i;
                    }
                    index = EditorGUILayout.Popup(title, index, collection, EditorStyles.popup);

                    selection = collection[index];
                    if (selection == None)
                    {
                        // The user selected "None", return null. Unity uses this string where null
                        // is the underlying value.
                        selection = null;
                    }
                }
            }
        }

        private void RenderToggle(MRConfig configKey, string title)
        {
            bool configured = MixedRealityProjectConfigurator.IsConfigured(configKey);
            using (new EditorGUI.DisabledGroupScope(configured))
            {
                if (configured)
                {
                    EditorGUILayout.LabelField(new GUIContent(title, InspectorUIUtility.SuccessIcon));
                    trackToggles[configKey] = false;
                }
                else
                {
                    trackToggles[configKey] = EditorGUILayout.ToggleLeft(title, trackToggles[configKey]);
                }
            }
        }

        private void createSpace(float width)
        {
#if UNITY_2019_3_OR_NEWER
            EditorGUILayout.Space(width);
#else
            for (int i = 0; i < Math.Ceiling(width / 5); i++)
            {
                EditorGUILayout.Space();
            }
#endif // UNITY_2019_3_OR_NEWER
        }
    }
}
