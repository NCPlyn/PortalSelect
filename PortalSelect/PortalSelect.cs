using System;
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
        public const string Version = "0.1.3";
        public const string DownloadLink = "https://github.com/NCPlyn/PortalSelect";
    }

    public class PortalSelect : MelonMod
    {
        GameObject ControllerRight, ControllerLeft;
        public const string RightTrigger = "Oculus_CrossPlatform_SecondaryIndexTrigger";
        public const string LeftTrigger = "Oculus_CrossPlatform_PrimaryIndexTrigger";

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
                ControllerLeft = GameObject.Find("/_Application/TrackingVolume/TrackingOculus(Clone)/OVRCameraRig/TrackingSpace/LeftHandAnchor/PointerOrigin (1)");
            }
            else
            {
                ControllerRight = GameObject.Find("/_Application/TrackingVolume/TrackingSteam(Clone)/SteamCamera/[CameraRig]/Controller (right)/PointerOrigin");
                ControllerLeft = GameObject.Find("/_Application/TrackingVolume/TrackingSteam(Clone)/SteamCamera/[CameraRig]/Controller (left)/PointerOrigin");
            }

            if (!XRDevice.isPresent)
            {
                ExpansionKitApi.GetExpandedMenu(ExpandedMenu.WorldMenu).AddSimpleButton("Select Portal", () =>
                {
                    OpenPortalPage(null);
                });
            }
            MelonLogger.Msg("Init done");
        }

        public bool? TriggerIsDown
        {
            get
            {
                if (Input.GetButtonDown(RightTrigger) || Input.GetAxisRaw(RightTrigger) != 0 || Input.GetAxis(RightTrigger) >= 0.75f) return true;
                else if (Input.GetButtonDown(LeftTrigger) || Input.GetAxisRaw(LeftTrigger) != 0 || Input.GetAxis(LeftTrigger) >= 0.75f) return false;
                else return null;
            }
        }

        bool conti = true, released = true;
        public override void OnUpdate() //checking for pressed/unpressed trigger and open/close of Worlds page
        {
            if(TriggerIsDown == true || TriggerIsDown == false)
            {
                if(released && conti && GameObject.Find("UserInterface/MenuContent/Screens/Worlds").active)
                {
                    conti = false;
                    OpenPortalPage(TriggerIsDown);
                }
                released = false;
            } else if (conti == false && GameObject.Find("UserInterface/MenuContent/Screens/Worlds").active == false)
            {
                conti = true;
            } else if (TriggerIsDown == null)
            {
                released = true;
            }
        }

        public void OpenPortalPage(bool? whichController)
        {
            //get position and rotation of object to start raycast from
            Vector3 rforward, rposition;
            if(whichController == true) //true if right, false if left, null should get here only if desktop
            {
                rforward = ControllerRight.transform.forward;
                rposition = ControllerRight.transform.position;
            } else if (whichController == false)
            {
                rforward = ControllerLeft.transform.forward;
                rposition = ControllerLeft.transform.position;
            } else
            {
                rforward = VRCPlayer.field_Internal_Static_VRCPlayer_0.transform.forward;
                rposition = VRCPlayer.field_Internal_Static_VRCPlayer_0.transform.position;
            }

            if (Physics.Raycast(rposition, rforward, out RaycastHit hit2, 300f)) //if ray hit anything
            {
                PortalInternal portalGet = hit2.collider.gameObject.GetComponentInChildren<PortalInternal>();
                if (portalGet) //and it is object with PortalInternal **I think, from here it can be broken easily by game update**
                {
                    var insString = portalGet.field_Private_String_1; //get instanceID from portal
                    var world = new ApiWorld { id = portalGet.field_Private_ApiWorld_0.id }; //get worldID from portal
                    string insName, insOwner = null;
                    InstanceAccessType insType = InstanceAccessType.Public; //default to public
                    NetworkRegion insRegion = NetworkRegion.US; //default to USA

                    if (insString != null) //instanceID is empty if the portal is not placed by player in game
                    {
                        if (!int.TryParse(insString, out int i)) //if the world is public and in USA, it has only instanceID (example: "55368") - skip to line 180
                        {
                            string[] splitArray = insString.Split(char.Parse("~"));
                            insName = splitArray[0];

                            //get REGION
                            if (splitArray.Length == 2) //if the world is public and in JP/EU, it has instanceID and region (example: "55368~region(eu)")
                            {
                                string temp = splitArray[1].Split(char.Parse("("))[1].Remove(2);
                                if (temp == "jp")
                                {
                                    insRegion = NetworkRegion.Japan;
                                }
                                else
                                {
                                    insRegion = NetworkRegion.Europe;
                                }
                            }
                            else //if the instance is anything other (invite(only),friends(+)) (example: "55368~hidden(userID)~region(eu)~nonce(....)")
                            {
                                if (splitArray[splitArray.Length - 2] == "canRequestInvite") //if we get "canRequestInvite" in the second position from end, it means that the instance is Invite+ and in USA (example: "55368~private(userID)~canRequestInvite~nonce(....)")
                                {
                                    insRegion = NetworkRegion.US;
                                }
                                else //if not, it means that the instance is not based in US (can be but with the splitArray at second position from back we will get userID, which is not equal to "jp" or "eu")
                                {
                                    string temp = splitArray[splitArray.Length - 2].Split(char.Parse("("))[1].Remove(2);
                                    if (temp == "jp")
                                    {
                                        insRegion = NetworkRegion.Japan;
                                    }
                                    else if (temp == "eu")
                                    {
                                        insRegion = NetworkRegion.Europe;
                                    }
                                }
                            }

                            //get TYPE
                            if (splitArray.Length != 2 || splitArray.Length != 1) //if the instance is anything other (invite(only),friends(+)) and it's not public
                            {
                                string temp = splitArray[1].Split(char.Parse("("))[0];
                                if (temp == "private")
                                {
                                    if (splitArray[2] == "canRequestInvite")
                                    {
                                        insType = InstanceAccessType.InvitePlus;
                                    }
                                    else
                                    {
                                        insType = InstanceAccessType.InviteOnly;
                                    }
                                }
                                else if (temp == "friends")
                                {
                                    insType = InstanceAccessType.FriendsOnly;
                                }
                                else if (temp == "hidden")
                                {
                                    insType = InstanceAccessType.FriendsOfGuests;
                                }
                            }

                            //get creator of instance if it's not public
                            if (splitArray.Length > 2)
                            {
                                insOwner = splitArray[1].Split(char.Parse("("))[1].Remove(40);
                            }
                        } else //in USA and public
                        {
                            insName = insString;
                            insRegion = NetworkRegion.US;
                            insType = InstanceAccessType.Public;
                        }

                        var instanceID = new ApiWorldInstance { name = insName, instanceId = insString, count = portalGet.field_Private_Int32_0, region = insRegion, type = insType, ownerId = insOwner};

                        OpenPage(world, instanceID, true);
                    } else
                    {
                        OpenPage(world, null, false);
                    }
                }
            }
        }

        //everything under this is from knahs FavCat mod, Thanks
        public void OpenPage(ApiWorld world, ApiWorldInstance instanceID, bool isWithIsnt)
        {
            world.Fetch(new Action<ApiContainer>(_ =>
            {
                if (isWithIsnt == true)
                {
                    ScanningReflectionCache.DisplayWorldInfoPage(world, instanceID, true, null);
                }
                else
                {
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
