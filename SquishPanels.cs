using HarmonyLib;
using ResoniteModLoader;
using FrooxEngine;
using FrooxEngine.UIX;
using Elements.Core;



namespace SquishPanels;
public class SquishPanels : ResoniteMod
{
    public override string Author => "Cyro";
    public override string Name => "SquishPanels";
    public override string Version => "3.0.0";

    public static float DopplerLevel = 0.0f;
    public static AudioDistanceSpace DistSpace = AudioDistanceSpace.Global;
    public static float TweenSpeed = 0.22f;
    private static ModConfiguration? Config;

    [AutoRegisterConfigKey]
    private static ModConfigurationKey<bool> Enabled = new ModConfigurationKey<bool>("Enabled", "Enables or disables the mod", () => true);

    [AutoRegisterConfigKey]
    private static ModConfigurationKey<bool> PlaySoundLocally = new ModConfigurationKey<bool>("PlaySoundLocally", "Makes the sound local for the mod user if true", () => false);

    [AutoRegisterConfigKey]
    private static ModConfigurationKey<string> OpenSoundURL = new ModConfigurationKey<string>("OpenSoundURL", "The URL of the sound to play when the panel opens", () => "resdb:///bbdf36b8f036a5c30f7019d68c1fbdd4032bb1d4c9403bcb926bb21cd0ca3c1a.wav");

    [AutoRegisterConfigKey]
    private static ModConfigurationKey<string> CloseSoundURL = new ModConfigurationKey<string>("CloseSoundURL", "The URL of the sound to play when the panel closes", () => "resdb:///e600ed8a6895325613b82a50fd2a8ea2ac64151adc5c48c913d33d584fdf75d5.wav");

    [AutoRegisterConfigKey]
    private static ModConfigurationKey<float> PanelOpenSpeed = new ModConfigurationKey<float>("PanelOpenSpeed", "The speed (in seconds) at which the panel opens", () => 0.22f);

    [AutoRegisterConfigKey]
    private static ModConfigurationKey<float> PanelCloseSpeed = new ModConfigurationKey<float>("PanelCloseSpeed", "The speed (in seconds) at which the panel closes", () => 0.22f);
    public override void OnEngineInit()
    {
        Config = GetConfiguration();
        
        if (Config == null)
            throw new NullReferenceException("Config is null");
        
        Config.Save(true);
        Harmony harmony = new Harmony("net.Cyro.SquishPanels");
        harmony.PatchAll();
    }

    [HarmonyPatch(typeof(LegacyPanel), "OnAttach")]
    public static class LegacyPanel_OnAttach_Patch
    {

        private static bool ShouldPlayLocally()
        {
            string? description = Engine.Current.WorldManager.FocusedWorld.Description;
            return description != null && description.Contains("##SquishPanels.ForceLocal##") || Config!.GetValue<bool>(PlaySoundLocally);
        }
        public static void PlayOpenSound(LegacyPanel __instance)
        {
            StaticAudioClip clip = __instance.World.GetSharedComponentOrCreate<StaticAudioClip>(Config!.GetValue<string>(OpenSoundURL), a => a.URL.Value = new Uri(Config!.GetValue<string>(OpenSoundURL)));
            AudioOutput audio = __instance.World.PlayOneShot(__instance.Slot.GlobalPosition, clip, 1f, true, 1f, __instance.Slot, AudioDistanceSpace.Local, ShouldPlayLocally());
            // UniLog.Log("Playing Open Sound " + (ShouldPlayLocally() ? "Locally" : "Globally").ToString());
            audio.DopplerLevel.Value = DopplerLevel;
            audio.DistanceSpace.Value = DistSpace;
        }

        public static void PlayCloseSound(LegacyPanel __instance)
        {
            StaticAudioClip clip = __instance.World.GetSharedComponentOrCreate<StaticAudioClip>(Config!.GetValue<string>(CloseSoundURL), a => a.URL.Value = new Uri(Config!.GetValue<string>(CloseSoundURL)));
            AudioOutput audio = __instance.World.PlayOneShot(__instance.Slot.GlobalPosition, clip, 1f, true, 1f, __instance.Slot, AudioDistanceSpace.Local, ShouldPlayLocally());
            // UniLog.Log("Playing Close Sound " + (ShouldPlayLocally() ? "Locally" : "Globally").ToString());
            audio.DopplerLevel.Value = DopplerLevel;
            audio.DistanceSpace.Value = DistSpace;
        }

        public static void Postfix(LegacyPanel __instance)
        {
            if (!Config!.GetValue<bool>(Enabled))
                return;
            
            float3 Orig = __instance.Slot.LocalScale;

            SyncListElementsEvent<SyncRef<IBounded>>? CanvasListener = null;
            
            CanvasListener = (SyncElementList<SyncRef<IBounded>> list, int StartIndex, int count) => {
                __instance.RunInUpdates(0, () => {
                    if (list[StartIndex] == null)
                        return;
                    
                    if (list[StartIndex].Target is Canvas)
                    {
                        Canvas? c = list[StartIndex].Target as Canvas;
                        
                        if (c != null)
                        {
                            float2 OrigSize = c.Size.Value;
                            PlayOpenSound(__instance);
                            c.Size.TweenFrom(new float2(OrigSize.x, 0f), Config!.GetValue<float>(PanelOpenSpeed));
                        }
                    }
                    __instance.WhiteList.ElementsAdded -= CanvasListener;
                });
            };
            __instance.RunInUpdates(0, () => {
                if (__instance.WhiteList.Count < 1)
                {
                    float3 OrigSize = __instance.Slot.LocalScale;
                    PlayOpenSound(__instance);
                    __instance.Slot.Scale_Field.TweenFrom(new float3(OrigSize.x, 0f, OrigSize.z), Config!.GetValue<float>(PanelOpenSpeed));
                    __instance.WhiteList.ElementsAdded -= CanvasListener;
                }
            });
            __instance.WhiteList.ElementsAdded += CanvasListener;
        }
    }

    [HarmonyPatch]
    public static class LegacyPanel_OnClose_Snapshot
    {
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(LegacyPanel), "OnClose")]
        public static void OnClose(object instance, LegacyPanel.TitleButton button)
        {
            throw new NotImplementedException();
        }
    }

    [HarmonyPatch(typeof(LegacyPanel), "OnClose")]
    public static class LegacyPanel_AddCloseButton_Patch
    {
        public static bool Prefix(LegacyPanel __instance, LegacyPanel.TitleButton button)
        {
            if(!Config!.GetValue<bool>(Enabled))
                return true;
            
            Action OnTweenDoneAction = () => {
                if (__instance.World.IsUserspace())
                    __instance.Slot.Destroy();
                else
                    LegacyPanel_OnClose_Snapshot.OnClose(__instance, button);
            };

            if (__instance.WhiteList.Count < 1)
            {
                float3 OrigSize = __instance.Slot.LocalScale;
                LegacyPanel_OnAttach_Patch.PlayCloseSound(__instance);
                __instance.Slot.Scale_Field.TweenTo(new float3(OrigSize.x, 0f, OrigSize.z), Config!.GetValue<float>(PanelCloseSpeed), default, null, OnTweenDoneAction);
                return false;
            }

            if (__instance.WhiteList[0] == null)
                return true;

            if (__instance.WhiteList[0] is Canvas)
            {
                Canvas? c = __instance.WhiteList[0] as Canvas;

                if (c == null)
                    return true;
                
                LegacyPanel_OnAttach_Patch.PlayCloseSound(__instance);
                c.Size.TweenTo(new float2(c.Size.Value.x, 0f), Config!.GetValue<float>(PanelCloseSpeed), default, null, OnTweenDoneAction);
                return false;
            }
            return true;
        }
    }
}
