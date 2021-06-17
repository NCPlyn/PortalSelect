﻿using System;
using System.Linq;
using MelonLoader;
using UnityEngine;
using UnityEngine.XR;
using UIExpansionKit.API;
using VRC.Core;
using UnhollowerRuntimeLib.XrefScans;
using System.Reflection;
using System.Collections;

namespace PortalSelect
{
    public static class BuildInfo
    {
        public const string Name = "PortalSelect";
        public const string Author = "NCPlyn";
        public const string Company = "NCPlyn";
        public const string Version = "0.1.1";
        public const string DownloadLink = "https://github.com/NCPlyn/PortalSelect";
    }

    public class PortalSelect : MelonMod
    {
        GameObject ControllerRight;
        public const string RightTrigger = "Oculus_CrossPlatform_SecondaryIndexTrigger";

        public override void OnApplicationStart()
        {
            MelonCoroutines.Start(UiManagerInitializer());
        }

        private IEnumerator UiManagerInitializer()
        {
            while (VRCUiManager.prop_VRCUiManager_0 == null) yield return null;
            OnUiManagerInit();
        }

        private void OnUiManagerInit()
        {
            if (Environment.CurrentDirectory.Contains("vrchat-vrchat"))
            {
                ControllerRight = GameObject.Find("/_Application/TrackingVolume/TrackingOculus(Clone)/OVRCameraRig/TrackingSpace/RightHandAnchor/PointerOrigin (1)");
            }
            else
            {
                ControllerRight = GameObject.Find("/_Application/TrackingVolume/TrackingSteam(Clone)/SteamCamera/[CameraRig]/Controller (right)/PointerOrigin");
            }

            if (!XRDevice.isPresent)
            {
                ExpansionKitApi.GetExpandedMenu(ExpandedMenu.WorldMenu).AddSimpleButton("Select Portal", () =>
                {
                    OpenPortalPage();
                });
            }
            MelonLogger.Msg("Init done");
        }

        public bool TriggerIsDown
        {
            get
            {
                return Input.GetButtonDown(RightTrigger) || Input.GetAxisRaw(RightTrigger) != 0 || Input.GetAxis(RightTrigger) >= 0.75f;
            }
        }

        bool conti = true;
        public override void OnUpdate()
        {
            if(TriggerIsDown && conti && GameObject.Find("UserInterface/MenuContent/Screens/Worlds").active)
            {
                conti = false;
                OpenPortalPage();
            } else if(conti == false && GameObject.Find("UserInterface/MenuContent/Screens/Worlds").active == false)
            {
                conti = true;
            }
        }

        public void OpenPortalPage()
        {
            Vector3 rforward, rposition;

            if (XRDevice.isPresent) {
                rforward = ControllerRight.transform.forward;
                rposition = ControllerRight.transform.position;
            } else {
                rforward = VRCPlayer.field_Internal_Static_VRCPlayer_0.transform.forward;
                rposition = VRCPlayer.field_Internal_Static_VRCPlayer_0.transform.position;
            }

            RaycastHit hit2;
            if (Physics.Raycast(rposition, rforward, out hit2, 200f))
            {
                PortalInternal portalGet = hit2.collider.gameObject.GetComponentInChildren<PortalInternal>();
                if (portalGet)
                {
                    var instanceID = new ApiWorldInstance { name = portalGet.field_Private_String_1, instanceId = portalGet.field_Private_String_1, count = portalGet.field_Private_Int32_0 };

                    var world = new ApiWorld { id = portalGet.field_Private_ApiWorld_0.id };

                    world.Fetch(new Action<ApiContainer>(_ =>
                    {
                        if (portalGet.field_Private_String_1 != null) {
                            ScanningReflectionCache.DisplayWorldInfoPage(world, instanceID, true, null);
                        } else {
                            ScanningReflectionCache.DisplayWorldInfoPage(world, null, false, null);
                        }

                    }), new Action<ApiContainer>(c =>
                    {
                        if (MelonDebug.IsEnabled())
                            MelonLogger.Msg("API request errored with " + c.Code + " - " + c.Error);
                        if (c.Code == 404)
                        {
                            var menu = ExpansionKitApi.CreateCustomFullMenuPopup(LayoutDescription.WideSlimList);
                            menu.AddSpacer();
                            menu.AddSpacer();
                            menu.AddLabel("This world is not available anymore (deleted)");
                            menu.AddSpacer();
                            menu.AddSpacer();
                            menu.AddSpacer();
                            menu.AddSimpleButton("Close", menu.Hide);
                            menu.Show();
                        }
                    }));
                }
            }
        }
    }

    public static class ScanningReflectionCache
    {
        private static Action<ApiWorld, ApiWorldInstance?, bool, APIUser?>? ourShowWorldInfoPageDelegate;

        public static void DisplayWorldInfoPage(ApiWorld world, ApiWorldInstance? instance, bool hasInstanceId, APIUser? user)
        {
            if (ourShowWorldInfoPageDelegate == null)
            {
                var target = typeof(UiWorldList)
                    .GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static).Single(it =>
                        it.Name.StartsWith("Method_Public_Static_Void_ApiWorld_ApiWorldInstance_Boolean_APIUser_") &&
                        XrefScanner.XrefScan(it).Any(jt =>
                            jt.Type == XrefType.Global && jt.ReadAsObject()?.ToString() ==
                            "UserInterface/MenuContent/Screens/WorldInfo"));

                ourShowWorldInfoPageDelegate = (Action<ApiWorld, ApiWorldInstance?, bool, APIUser?>)Delegate.CreateDelegate(typeof(Action<ApiWorld, ApiWorldInstance?, bool, APIUser?>), target);
            }

            ourShowWorldInfoPageDelegate(world, instance, hasInstanceId, user);
        }
    }
}
