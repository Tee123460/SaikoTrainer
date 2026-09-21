using System;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(SaikoTrainer.Main), "Saiko Trainer", "1.19.0", "Sinjiratha")]
[assembly: MelonGame("Habupain", "Saiko no sutoka")]

namespace SaikoTrainer
{
    public static class Toggles
    {
        public static bool GodMode;
        public static bool ESP;
        public static bool KeyESP;
        public static bool NoClip;
        public static bool FreezeAI;
        public static bool SpeedHack;
        public static bool InfiniteStamina;
    }

    [HarmonyPatch(typeof(Il2Cpp.HealthManager), "ApplyDamage")]
    public static class ApplyDamagePatch
    {
        public static bool Prefix() => !Toggles.GodMode;
    }

    [HarmonyPatch(typeof(Il2Cpp.HealthManager), "ApplyBleedDamage")]
    public static class ApplyBleedDamagePatch
    {
        public static bool Prefix() => !Toggles.GodMode;
    }

    [HarmonyPatch(typeof(Il2Cpp.HealthManager), "Kill")]
    public static class KillPatch
    {
        public static bool Prefix() => !Toggles.GodMode;
    }

    [HarmonyPatch(typeof(Il2Cpp.YandereController), "Update")]
    public static class FreezeAIUpdatePatch
    {
        public static bool Prefix() => !Toggles.FreezeAI;
    }

    [HarmonyPatch(typeof(Il2Cpp.YandereController), "FixedUpdate")]
    public static class FreezeAIFixedUpdatePatch
    {
        public static bool Prefix() => !Toggles.FreezeAI;
    }

    [HarmonyPatch(typeof(Il2Cpp.YandereController), "OnAnimatorMove")]
    public static class FreezeAIAnimatorMovePatch
    {
        public static bool Prefix() => !Toggles.FreezeAI;
    }

    [HarmonyPatch(typeof(UnityEngine.Input), "GetAxis", new Type[] { typeof(string) })]
    public static class InputAxisPatch
    {
        public static bool Prefix(ref float __result, string axisName)
        {
            if (Main.MenuIsOpen() && (axisName == "Mouse X" || axisName == "Mouse Y"))
            {
                __result = 0f;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UnityEngine.Input), "GetAxisRaw", new Type[] { typeof(string) })]
    public static class InputAxisRawPatch
    {
        public static bool Prefix(ref float __result, string axisName)
        {
            if (Main.MenuIsOpen() && (axisName == "Mouse X" || axisName == "Mouse Y"))
            {
                __result = 0f;
                return false;
            }
            return true;
        }
    }

    public class Main : MelonMod
    {
        private static bool menuOpenShared;

        public static bool MenuIsOpen()
        {
            return menuOpenShared;
        }

        private const KeyCode MenuKey = KeyCode.Insert;

        private bool menuOpen;
        private bool renderFailed;
        private float timeScale = 1f;
        private bool infiniteTime;
        private float refillTimer;

        private UnityEngine.CursorLockMode cachedLock;
        private bool cachedVisible;

        private const float MENU_W = 280f;
        private const float MENU_H = 640f;
        private const float TITLE_H = 42f;
        private Vector2 menuPos = new Vector2(20f, 20f);
        private bool dragging;
        private Vector2 dragOffset;
        private int menuTab;

        private float espFindTimer;
        private Il2Cpp.YandereController espTarget;
        private Camera espCam;
        private float espPrevDist;

        private float keyEspFindTimer;
        private Il2Cpp.KeyNameTag[] keyCache;
        private Camera keyEspCam;

        private float noClipFindTimer;
        private float noClipDiagTimer;
        private UnityEngine.CharacterController cachedCC;
        private UnityEngine.Collider[] cachedColliders;
        private string noclipSource;
        private const float NO_CLIP_SPEED = 6f;

        private float freezeFindTimer;
        private Il2Cpp.YandereController freezeTarget;
        private UnityEngine.AI.NavMeshAgent freezeAgent;

        private float pcCacheTimer;
        private Il2Cpp.PlayerController pcCache;
        private bool speedsBaselineSet;
        private float baseWalk;
        private float baseRun;
        private const float SPEED_MULT = 1.8f;

        private bool uiReady;
        private UnityEngine.AudioSource uiSfxSource;
        private float sfxStopTimer;
        private GUIStyle borderStyle;
        private GUIStyle panelStyle;
        private GUIStyle titleBarStyle;
        private GUIStyle titleStyle;
        private GUIStyle titleShadowStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle valueStyle;
        private GUIStyle infoStyle;
        private GUIStyle buttonGreen;
        private GUIStyle buttonRed;
        private GUIStyle buttonBlue;
        private GUIStyle buttonAmber;
        private GUIStyle buttonGrey;
        private GUIStyle tabOnStyle;
        private GUIStyle tabOffStyle;
        private GUIStyle tabStripStyle;
        private GUIStyle sepStyle;
        private GUIStyle espLabelStyle;
        private GUIStyle espBoxStyle;
        private GUIStyle espKeyLabelStyle;
        private Texture2D keyMarkerTex;
        private Texture2D exitMarkerTex;
        private Texture2D panelBgTex;
        private Texture2D panelShadowTex;
        private Texture2D glassShineTex;

        public override void OnInitializeMelon()
        {
            HarmonyInstance.PatchAll();
            LoggerInstance.Msg("Saiko Trainer v1.19 by Sinjiratha loaded. Press Insert for menu.");
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(MenuKey))
            {
                PlayUiSound(!menuOpen);
                if (menuOpen)
                    RestoreCursor();
                else
                    SaveAndUnlockCursor();
                menuOpen = !menuOpen;
            }

            if (menuOpen)
            {
                UnityEngine.Cursor.lockState = UnityEngine.CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }

            menuOpenShared = menuOpen;

            if (sfxStopTimer > 0f)
            {
                sfxStopTimer -= Time.unscaledDeltaTime;
                if (sfxStopTimer <= 0f && uiSfxSource != null)
                    uiSfxSource.Stop();
            }

            if (infiniteTime)
                Time.timeScale = 1f;
            else
                Time.timeScale = timeScale;

            if (Toggles.GodMode)
                RefillHealthBackup();

            UpdateNoClip();
            UpdateFreezeAI();
            UpdatePlayerMods();

            if (menuOpen)
                UpdateDrag();
        }

        private void SaveAndUnlockCursor()
        {
            cachedLock = UnityEngine.Cursor.lockState;
            cachedVisible = UnityEngine.Cursor.visible;
            UnityEngine.Cursor.lockState = UnityEngine.CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private void RestoreCursor()
        {
            UnityEngine.Cursor.lockState = cachedLock;
            UnityEngine.Cursor.visible = cachedVisible;
        }

        private void UpdateDrag()
        {
            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);

            if (Input.GetMouseButtonDown(0) &&
                new Rect(menuPos.x + 6f, menuPos.y + 4f, MENU_W - 12f, 92f).Contains(mouse))
            {
                dragging = true;
                dragOffset = mouse - menuPos;
            }

            if (Input.GetMouseButton(0) && dragging)
            {
                menuPos = mouse - dragOffset;
                menuPos.x = Mathf.Clamp(menuPos.x, 0f, Mathf.Max(0f, Screen.width - MENU_W));
                menuPos.y = Mathf.Clamp(menuPos.y, 0f, Mathf.Max(0f, Screen.height - MENU_H));
            }

            if (Input.GetMouseButtonUp(0))
                dragging = false;
        }

        private void RefillHealthBackup()
        {
            refillTimer -= Time.unscaledDeltaTime;
            if (refillTimer > 0f)
                return;
            refillTimer = 0.5f;

            foreach (var hm in UnityEngine.Object.FindObjectsOfType<Il2Cpp.HealthManager>())
            {
                hm.Health = hm.maximumHealth;
                hm.isDead = false;
            }
        }

        private void UpdateNoClip()
        {
            if (Toggles.NoClip)
            {
                noClipFindTimer -= Time.unscaledDeltaTime;
                if (noClipFindTimer <= 0f)
                {
                    noClipFindTimer = 0.5f;
                    FindNoClipCC();
                }

                if (cachedCC != null)
                {
                    if (cachedCC.enabled)
                        cachedCC.enabled = false;

                    if (cachedColliders != null)
                        foreach (var col in cachedColliders)
                            if (col != null && col.enabled)
                                col.enabled = false;

                    var cam = Camera.main;
                    if (cam != null)
                    {
                        float h = Input.GetAxis("Horizontal");
                        float v = Input.GetAxis("Vertical");
                        float upDown = 0f;
                        if (Input.GetKey(KeyCode.Space))
                            upDown = 1f;
                        if (Input.GetKey(KeyCode.LeftControl))
                            upDown = -1f;

                        Vector3 dir = cam.transform.right * h + cam.transform.forward * v + Vector3.up * upDown;
                        if (dir.sqrMagnitude > 0f)
                        {
                            dir.Normalize();
                            cachedCC.transform.position += dir * NO_CLIP_SPEED * Time.unscaledDeltaTime;
                        }
                    }
                }

                noClipDiagTimer -= Time.unscaledDeltaTime;
                if (noClipDiagTimer > 0f)
                {
                    LoggerInstance.Msg($"NoClip diag: source={noclipSource} cc={cachedCC?.gameObject.name ?? "NULL"} enabled={cachedCC?.enabled} pos={cachedCC?.transform.position}");
                    noClipDiagTimer = 1f;
                }
            }
            else if (cachedCC != null)
            {
                if (cachedColliders != null)
                    foreach (var col in cachedColliders)
                        if (col != null)
                            col.enabled = true;

                cachedCC.enabled = true;
                cachedCC = null;
                cachedColliders = null;
                noclipSource = null;
            }
        }

        private void UpdateFreezeAI()
        {
            if (Toggles.FreezeAI)
            {
                freezeFindTimer -= Time.unscaledDeltaTime;
                if (freezeFindTimer <= 0f)
                {
                    freezeFindTimer = 0.5f;
                    freezeTarget = UnityEngine.Object.FindObjectOfType<Il2Cpp.YandereController>();
                    freezeAgent = freezeTarget != null
                        ? freezeTarget.GetComponent<UnityEngine.AI.NavMeshAgent>()
                        : null;
                }

                if (freezeAgent != null)
                {
                    if (!freezeAgent.isStopped)
                        freezeAgent.isStopped = true;
                    if (freezeAgent.velocity.sqrMagnitude > 0.0001f)
                        freezeAgent.velocity = Vector3.zero;
                }
            }
            else
            {
                freezeTarget = null;
                freezeAgent = null;
            }
        }

        private void UpdatePlayerMods()
        {
            pcCacheTimer -= Time.unscaledDeltaTime;
            if (pcCacheTimer <= 0f)
            {
                pcCacheTimer = 0.5f;
                pcCache = UnityEngine.Object.FindObjectOfType<Il2Cpp.PlayerController>();
                if (pcCache == null)
                {
                    speedsBaselineSet = false;
                }
            }

            if (pcCache == null)
                return;

            if (Toggles.SpeedHack)
            {
                if (!speedsBaselineSet)
                {
                    baseWalk = pcCache.walkSpeed;
                    baseRun = pcCache.runSpeed;
                    speedsBaselineSet = true;
                }
                pcCache.walkSpeed = baseWalk * SPEED_MULT;
                pcCache.runSpeed = baseRun * SPEED_MULT;
            }
            else if (speedsBaselineSet)
            {
                pcCache.walkSpeed = baseWalk;
                pcCache.runSpeed = baseRun;
                speedsBaselineSet = false;
            }

            if (Toggles.InfiniteStamina)
            {
                pcCache.unlimitedStamina = true;
                pcCache.staminaMeter = 9999f;
            }
            else
            {
                pcCache.unlimitedStamina = false;
            }
        }

        private void FindNoClipCC()
        {
            noclipSource = null;

            var fp = UnityEngine.Object.FindObjectOfType<Il2Cpp.FPController>();
            if (fp != null && fp.cc != null)
            {
                cachedCC = fp.cc;
                noclipSource = "FPController.cc";
            }
            else if (fp != null)
            {
                LoggerInstance.Msg("NoClip: FPController found but its cc is null");
            }

            if (cachedCC == null)
            {
                var p = UnityEngine.Object.FindObjectOfType<Il2Cpp.PlayerController>();
                if (p != null && p.controller != null)
                {
                    cachedCC = p.controller;
                    noclipSource = "PlayerController.controller";
                }
                else if (p != null)
                {
                    LoggerInstance.Msg("NoClip: PlayerController found but its controller is null");
                }
            }

            if (cachedCC == null)
            {
                LoggerInstance.Msg("NoClip: NO CharacterController found!");
                return;
            }

            cachedColliders = cachedCC.GetComponentsInChildren<UnityEngine.Collider>(true);
            LoggerInstance.Msg($"NoClip: using {noclipSource} on '{cachedCC.gameObject.name}', colliders={cachedColliders.Length}");
            noClipDiagTimer = 5f;
        }

        private static void Heal()
        {
            foreach (var hm in UnityEngine.Object.FindObjectsOfType<Il2Cpp.HealthManager>())
                hm.ApplySuperHeal(999f);
        }

        private static void TeleportToSaiko()
        {
            var saiko = UnityEngine.Object.FindObjectOfType<Il2Cpp.YandereController>();
            if (saiko == null)
                return;

            var cam = Camera.main;
            if (cam == null)
                return;

            Vector3 offset = saiko.transform.position - cam.transform.position;
            offset.y = 0f;

            Vector3 side = offset.sqrMagnitude > 0.0001f
                ? -offset.normalized * 1.5f
                : -saiko.transform.forward * 1.5f;

            Vector3 target = saiko.transform.position + side + Vector3.up * 0.3f;

            var pc = UnityEngine.Object.FindObjectOfType<Il2Cpp.PlayerController>();
            if (pc != null && pc.controller != null)
            {
                pc.controller.transform.position = target;
                return;
            }

            var fp = UnityEngine.Object.FindObjectOfType<Il2Cpp.FPController>();
            if (fp != null && fp.cc != null)
                fp.cc.transform.position = target;
        }

        private static void TeleportSaikoToYou()
        {
            var saiko = UnityEngine.Object.FindObjectOfType<Il2Cpp.YandereController>();
            if (saiko == null)
                return;

            var cam = Camera.main;
            if (cam == null)
                return;

            Vector3 target = cam.transform.position + cam.transform.forward * 2f + Vector3.up * 0.3f;

            var agent = saiko.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
                agent.Warp(target);
            else
                saiko.transform.position = target;

            Vector3 dir = cam.transform.position - saiko.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                saiko.transform.rotation = Quaternion.LookRotation(dir.normalized);
        }

        private void UnlockExitDoor()
        {
            try
            {
                var mgr = Il2Cpp.DoorAndKeyManager.instance;
                if (mgr == null)
                {
                    LoggerInstance.Msg("UnlockExit: DoorAndKeyManager not found");
                    return;
                }

                mgr.UnlockExitDoor();
                mgr.exitGateActivated = true;

                if (mgr.exitDoor != null)
                {
                    mgr.exitDoor.UnlockDoor();
                    mgr.exitDoor.ForceOpenDoor(false);
                }
                LoggerInstance.Msg("UnlockExit: exit unlocked and opened");
            }
            catch (Exception e)
            {
                LoggerInstance.Error("UnlockExit failed: " + e);
            }
        }

        private void PlayUiSound(bool isOpen)
        {
            if (uiSfxSource == null)
                EnsureSfxSource();
            if (uiSfxSource == null)
                return;

            var cam = Camera.main;
            if (cam != null)
                uiSfxSource.transform.position = cam.transform.position;

            UnityEngine.AudioClip clip = null;
            foreach (var s in UnityEngine.Object.FindObjectsOfType<UnityEngine.AudioSource>())
            {
                if (s != null && s.clip != null && s.clip.length < 2f)
                {
                    clip = s.clip;
                    break;
                }
            }
            if (clip == null)
                return;

            float start = Mathf.Max(0f, UnityEngine.Random.Range(0f, clip.length - 0.1f));
            uiSfxSource.clip = clip;
            uiSfxSource.time = start;
            uiSfxSource.pitch = isOpen ? 1.2f : 0.85f;
            uiSfxSource.volume = 0.4f;
            uiSfxSource.Play();
            sfxStopTimer = 0.07f;
        }

        private void EnsureSfxSource()
        {
            var cam = Camera.main;
            if (cam == null)
                return;

            var go = new GameObject("SaikoTrainerSFX");
            go.transform.parent = cam.transform.parent;
            go.transform.position = cam.transform.position;
            uiSfxSource = go.AddComponent<UnityEngine.AudioSource>();
            uiSfxSource.playOnAwake = false;
            uiSfxSource.spatialBlend = 0f;
        }

        private void DrawESP()
        {
            espFindTimer -= Time.unscaledDeltaTime;
            if (espFindTimer <= 0f)
            {
                espFindTimer = 0.5f;
                espTarget = UnityEngine.Object.FindObjectOfType<Il2Cpp.YandereController>();
                espCam = Camera.main;
            }

            if (espTarget == null || espCam == null)
                return;

            var skins = espTarget.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true);
            UnityEngine.Renderer rend;
            if (skins.Length > 0)
                rend = skins[0];
            else
                rend = espTarget.GetComponentInChildren<UnityEngine.Renderer>();

            if (rend == null)
                return;

            var bounds = rend.bounds;
            for (int i = 1; i < skins.Length; i++)
                bounds.Encapsulate(skins[i].bounds);

            Vector3 center = bounds.center;
            Vector3 ext = bounds.extents;

            Vector3 cs = espCam.WorldToScreenPoint(center);
            if (cs.z <= 0f)
                return;

            float sx = cs.x;
            float sy = Screen.height - cs.y;

            float dist = Vector3.Distance(espCam.transform.position, center);
            float tanV = Mathf.Tan(espCam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect = (float)Screen.width / (float)Screen.height;
            float ppuY = (Screen.height * 0.5f) / Mathf.Max(dist, 0.01f) / tanV;
            float ppuX = ppuY / aspect;

            float w = Mathf.Max(ext.x, ext.z) * 2f * ppuX;
            float h = ext.y * 2f * ppuY + 6f;

            if (w < 3f || h < 3f)
                return;

            float minX = sx - w * 0.5f;
            float maxX = sx + w * 0.5f;
            float minY = sy - h * 0.5f;
            float maxY = sy + h * 0.5f;

            var line = espBoxStyle.normal.background;

            GUI.DrawTexture(new Rect(minX, minY, w, 2f), line);
            GUI.DrawTexture(new Rect(minX, maxY - 2f, w, 2f), line);
            GUI.DrawTexture(new Rect(minX, minY, 2f, h), line);
            GUI.DrawTexture(new Rect(maxX - 2f, minY, 2f, h), line);

            GUI.Label(new Rect(sx - 60f, minY - 34f, 120f, 20f),
                "<color=red><b>SAIKO</b></color>", espLabelStyle);

            GUI.Label(new Rect(sx - 90f, maxY + 2f, 180f, 18f),
                string.Format("Pos: {0:0.0}, {1:0.0}, {2:0.0}", center.x, center.y, center.z), espLabelStyle);
            GUI.Label(new Rect(sx - 90f, maxY + 20f, 180f, 18f),
                string.Format("Dist: {0:0}m", dist), espLabelStyle);

            float delta = dist - espPrevDist;
            espPrevDist = dist;
            bool approaching = delta < -0.01f;
            bool lostSight = espTarget.GetLastTimeDetected() > 2f && espTarget.NotSeenSinceSomeTime();

            string aiLabel;
            bool attacking = espTarget.CheckIfAttacking();
            bool canKill = espTarget.CanDirectlyKillPlayer();
            bool canAttack = espTarget.CheckIfCanAttack();

            if (attacking)
                aiLabel = "<color=red><b>AI: ATTACKING!</b></color>";
            else if (canKill)
                aiLabel = "<color=red><b>AI: KILL MODE!</b></color>";
            else if (canAttack)
                aiLabel = "<color=orange><b>AI: Hostile!</b></color>";
            else if (dist < 3f)
                aiLabel = "<color=red><b>AI: ON TOP OF YOU!</b></color>";
            else if (approaching && dist < 12f)
                aiLabel = "<color=orange><b>AI: Getting closer!</b></color>";
            else if (approaching)
                aiLabel = "<color=orange>AI: Approaching</color>";
            else if (lostSight)
                aiLabel = "<color=#8CFF9E>AI: She lost you</color>";
            else if (dist < 8f)
                aiLabel = "AI: Nearby";
            else
                aiLabel = "AI: Far away";
            GUI.Label(new Rect(sx - 100f, maxY + 38f, 200f, 18f), aiLabel, espLabelStyle);
        }

        private void DrawKeyESP()
        {
            keyEspFindTimer -= Time.unscaledDeltaTime;
            if (keyEspFindTimer <= 0f)
            {
                keyEspFindTimer = 0.5f;
                keyEspCam = Camera.main;
                try { keyCache = UnityEngine.Object.FindObjectsOfType<Il2Cpp.KeyNameTag>(); }
                catch { keyCache = null; }
            }

            if (keyEspCam == null)
                return;

            Il2Cpp.DoorAndKeyManager mgr = null;
            try { mgr = Il2Cpp.DoorAndKeyManager.instance; }
            catch { }

            if (mgr != null && mgr.exitDoor != null)
            {
                float dist = Vector3.Distance(keyEspCam.transform.position, mgr.exitDoor.transform.position);
                Vector3 cs = keyEspCam.WorldToScreenPoint(mgr.exitDoor.transform.position + Vector3.up * 1.2f);
                if (cs.z > 0f)
                {
                    float sx = cs.x;
                    float sy = Screen.height - cs.y;
                    bool exitOpen = mgr.exitDoor.isOpened;
                    GUI.DrawTexture(new Rect(sx - 5f, sy - 5f, 10f, 10f), exitMarkerTex);
                    GUI.Label(new Rect(sx + 10f, sy - 8f, 320f, 18f),
                        exitOpen
                            ? "<color=#8CFF9E><b>EXIT DOOR (OPEN)</b></color> " + string.Format("{0:0}m", dist)
                            : "<color=#FFE08A><b>EXIT DOOR</b></color> " + string.Format("{0:0}m", dist),
                        espKeyLabelStyle);
                }
            }

            if (keyCache == null)
                return;

            for (int i = 0; i < keyCache.Length; i++)
            {
                var kn = keyCache[i];
                if (kn == null || kn.pickedUp)
                    continue;

                try
                {
                    if (kn.gameObject == null || !kn.gameObject.activeInHierarchy)
                        continue;
                }
                catch { continue; }

                Vector3 cs = keyEspCam.WorldToScreenPoint(kn.transform.position + Vector3.up * 0.8f);
                if (cs.z <= 0f)
                    continue;

                float sx = cs.x;
                float sy = Screen.height - cs.y;
                float dist = Vector3.Distance(keyEspCam.transform.position, kn.transform.position);

                string keyName = null;
                try { keyName = mgr != null ? mgr.GetKeyNameFromId(kn.keyId) : null; }
                catch { }

                if (string.IsNullOrEmpty(keyName))
                    keyName = "Key #" + kn.keyId;

                bool isExit = keyName.IndexOf("exit", StringComparison.OrdinalIgnoreCase) >= 0
                              || keyName.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0;

                GUI.DrawTexture(new Rect(sx - 4f, sy - 4f, 8f, 8f), isExit ? exitMarkerTex : keyMarkerTex);

                string label = isExit
                    ? "<color=#FFE08A><b>" + keyName + "</b></color>"
                    : "<color=#7FD7FF><b>" + keyName + "</b></color>";
                if (!string.IsNullOrEmpty(kn.roomName))
                    label += "  <color=#AAAAAA>(" + kn.roomName + ")</color>";
                label += string.Format("  {0:0}m", dist);

                GUI.Label(new Rect(sx + 9f, sy - 9f, 300f, 18f), label, espKeyLabelStyle);
            }
        }

        private void EnsureStyles()
        {
            if (uiReady)
                return;
            uiReady = true;

            panelShadowTex = MakeRoundedSolid(64, 64, 16, new Color(0f, 0f, 0f, 0.55f), new Color(0f, 0f, 0f, 0f), 0f);
            panelBgTex = MakeRoundedGradient((int)MENU_W, (int)MENU_H, 18,
                new Color(0.05f, 0.07f, 0.13f, 0.97f),
                new Color(0.015f, 0.02f, 0.055f, 0.99f),
                new Color(0.20f, 0.72f, 1f, 0.85f), 1.6f);
            glassShineTex = MakeRoundedGradient(280, 16, 4,
                new Color(1f, 1f, 1f, 0.14f),
                new Color(1f, 1f, 1f, 0f),
                new Color(0f, 0f, 0f, 0f), 0f);

            borderStyle = new GUIStyle();
            borderStyle.normal.background = panelBgTex;
            borderStyle.border = MakeOffset(18, 18, 18, 18);

            panelStyle = new GUIStyle();
            panelStyle.normal.background = panelBgTex;
            panelStyle.border = MakeOffset(18, 18, 18, 18);

            titleBarStyle = new GUIStyle();
            titleBarStyle.normal.background = MakeRoundedGradient(280, 60, 12,
                new Color(0.12f, 0.55f, 0.95f, 0.99f),
                new Color(0.25f, 0.13f, 0.72f, 0.99f),
                new Color(0.45f, 0.95f, 1f, 0.95f), 1.5f);
            titleBarStyle.border = MakeOffset(12, 12, 12, 12);

            titleStyle = new GUIStyle();
            titleStyle.fontSize = 21;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.richText = true;
            titleStyle.normal.textColor = Color.white;

            titleShadowStyle = new GUIStyle();
            titleShadowStyle.fontSize = 21;
            titleShadowStyle.fontStyle = FontStyle.Bold;
            titleShadowStyle.alignment = TextAnchor.MiddleCenter;
            titleShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.6f);

            subtitleStyle = new GUIStyle();
            subtitleStyle.fontSize = 10;
            subtitleStyle.alignment = TextAnchor.MiddleCenter;
            subtitleStyle.normal.textColor = new Color(0.70f, 0.82f, 1f, 0.85f);

            sectionStyle = new GUIStyle();
            sectionStyle.fontSize = 12;
            sectionStyle.fontStyle = FontStyle.Bold;
            sectionStyle.normal.textColor = new Color(0.35f, 0.85f, 1f);
            sectionStyle.margin = MakeOffset(0, 0, 8, 3);

            valueStyle = new GUIStyle();
            valueStyle.fontSize = 16;
            valueStyle.fontStyle = FontStyle.Bold;
            valueStyle.alignment = TextAnchor.MiddleCenter;
            valueStyle.normal.textColor = Color.white;

            infoStyle = new GUIStyle();
            infoStyle.fontSize = 12;
            infoStyle.alignment = TextAnchor.MiddleCenter;
            infoStyle.normal.textColor = new Color(0.62f, 0.72f, 0.85f);

            buttonGreen = MakeButtonStyle(new Color(0.05f, 0.45f, 0.22f), new Color(0.12f, 0.80f, 0.40f));
            buttonRed = MakeButtonStyle(new Color(0.50f, 0.10f, 0.12f), new Color(0.95f, 0.28f, 0.25f));
            buttonBlue = MakeButtonStyle(new Color(0.06f, 0.33f, 0.60f), new Color(0.16f, 0.65f, 0.98f));
            buttonAmber = MakeButtonStyle(new Color(0.52f, 0.29f, 0.05f), new Color(0.95f, 0.62f, 0.12f));
            buttonGrey = MakeButtonStyle(new Color(0.10f, 0.13f, 0.18f), new Color(0.24f, 0.31f, 0.40f));

            tabOnStyle = MakeButtonStyle(new Color(0.08f, 0.50f, 0.85f), new Color(0.25f, 0.85f, 1f));
            tabOffStyle = MakeButtonStyle(new Color(0.06f, 0.08f, 0.13f), new Color(0.12f, 0.16f, 0.24f));

            tabStripStyle = new GUIStyle();
            tabStripStyle.normal.background = MakeRoundedSolid(280, 40, 10,
                new Color(0.03f, 0.04f, 0.09f, 0.95f),
                new Color(0.16f, 0.55f, 0.85f, 0.45f), 1.2f);
            tabStripStyle.border = MakeOffset(10, 10, 10, 10);

            sepStyle = new GUIStyle();
            sepStyle.normal.background = MakeRoundedSolid(32, 4, 2,
                new Color(1f, 1f, 1f, 0.12f), new Color(1f, 1f, 1f, 0f), 0f);
            sepStyle.margin = MakeOffset(0, 0, 6, 6);

            espLabelStyle = new GUIStyle();
            espLabelStyle.fontSize = 13;
            espLabelStyle.fontStyle = FontStyle.Bold;
            espLabelStyle.alignment = TextAnchor.MiddleCenter;
            espLabelStyle.richText = true;
            espLabelStyle.normal.textColor = Color.white;

            espBoxStyle = new GUIStyle();
            espBoxStyle.normal.background = MakeTex(2, 2, new Color(1f, 0.22f, 0.22f, 0.9f));

            espKeyLabelStyle = new GUIStyle();
            espKeyLabelStyle.fontSize = 12;
            espKeyLabelStyle.fontStyle = FontStyle.Bold;
            espKeyLabelStyle.alignment = TextAnchor.MiddleLeft;
            espKeyLabelStyle.richText = true;
            espKeyLabelStyle.normal.textColor = Color.white;

            keyMarkerTex = MakeTex(2, 2, new Color(0.50f, 0.85f, 1f, 0.95f));
            exitMarkerTex = MakeTex(2, 2, new Color(0.35f, 1f, 0.55f, 0.95f));
        }

        private static float SdfRoundedRect(float px, float py, float w, float h, float r)
        {
            float cx = Mathf.Abs(px - (w - 1f) * 0.5f) - ((w - 1f) * 0.5f - r);
            float cy = Mathf.Abs(py - (h - 1f) * 0.5f) - ((h - 1f) * 0.5f - r);
            float ox = Mathf.Max(cx, 0f);
            float oy = Mathf.Max(cy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(cx, cy), 0f) - r;
        }

        private static Texture2D MakeRounded(int w, int h, float r, System.Func<Vector2, float, Color> frag)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float d = SdfRoundedRect(x + 0.5f, y + 0.5f, w, h, r);
                    float a = Mathf.Clamp01(0.5f - d);
                    Color c = frag(new Vector2((x + 0.5f) / w, (y + 0.5f) / h), d);
                    c.a *= a;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeRoundedSolid(int w, int h, float r, Color fill, Color border, float borderW)
        {
            return MakeRounded(w, h, r, (uv, d) => d > -borderW ? border : fill);
        }

        private static Texture2D MakeRoundedGradient(int w, int h, float r, Color top, Color bottom, Color border, float borderW)
        {
            return MakeRounded(w, h, r, (uv, d) => d > -borderW ? border : Color.Lerp(top, bottom, uv.y));
        }

        private static Texture2D MakeTex(int w, int h, Color c)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, c);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeGradientTex(int w, int h, Color top, Color bottom)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                float t = h <= 1 ? 0f : (float)y / (h - 1);
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, Color.Lerp(top, bottom, t));
            }
            tex.Apply();
            return tex;
        }

        private static GUIStyle MakeButtonStyle(Color baseColor, Color lightColor)
        {
            const int size = 48;
            const float r = 13f;

            var normal = MakeRoundedGradient(size, size, r, baseColor, Color.Lerp(baseColor, lightColor, 0.45f), lightColor, 1.4f);
            var hover = MakeRoundedGradient(size, size, r,
                Color.Lerp(baseColor, Color.white, 0.12f),
                Color.Lerp(lightColor, Color.white, 0.35f),
                Color.Lerp(lightColor, Color.white, 0.5f), 1.7f);
            var active = MakeRoundedGradient(size, size, r,
                Color.Lerp(baseColor, Color.black, 0.35f),
                Color.Lerp(baseColor, Color.black, 0.15f),
                Color.Lerp(lightColor, Color.black, 0.2f), 1.2f);

            var style = new GUIStyle();
            style.fontSize = 14;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            style.richText = true;
            style.padding = MakeOffset(6, 6, 4, 4);
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = active;
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.border = MakeOffset((int)r, (int)r, (int)r, (int)r);
            return style;
        }

        private static RectOffset MakeOffset(int left, int right, int top, int bottom)
        {
            var offset = new RectOffset();
            offset.left = left;
            offset.right = right;
            offset.top = top;
            offset.bottom = bottom;
            return offset;
        }

        public override void OnGUI()
        {
            if (renderFailed)
                return;

            try
            {
                if (uiReady && (borderStyle.normal.background == null || panelStyle.normal.background == null))
                {
                    uiReady = false;
                    EnsureStyles();
                }

                EnsureStyles();

                if (Toggles.ESP)
                    DrawESP();
                if (Toggles.KeyESP)
                    DrawKeyESP();

                if (!menuOpen)
                    return;

                float X = menuPos.x, Y = menuPos.y, W = MENU_W, H = MENU_H;

                GUI.DrawTexture(new Rect(X + 5f, Y + 7f, W, H), panelShadowTex);
                GUI.Box(new Rect(X, Y, W, H), "", borderStyle);
                GUI.Box(new Rect(X, Y, W, H), "", panelStyle);

                GUI.DrawTexture(new Rect(X + 8f, Y + 8f, W - 20f, 16f), glassShineTex);

                GUI.Box(new Rect(X + 10f, Y + 10f, W - 20f, 52f), "", titleBarStyle);
                GUI.Label(new Rect(X + 11f, Y + 11f, W - 20f, 52f), "SAIKO TRAINER", titleShadowStyle);
                GUI.Label(new Rect(X + 10f, Y + 10f, W - 20f, 44f), "SAIKO  <color=#8AE9FF>TRAINER</color>", titleStyle);
                GUI.Label(new Rect(X + 10f, Y + 50f, W - 20f, 14f), "v1.19  |  INSERT : MENU", subtitleStyle);

                float tabY = Y + 68f;
                GUI.Box(new Rect(X + 10f, tabY, W - 20f, 38f), "", tabStripStyle);
                float tabW = (W - 28f) * 0.5f;
                if (GUI.Button(new Rect(X + 14f, tabY + 3f, tabW, 32f), "MAIN", menuTab == 0 ? tabOnStyle : tabOffStyle))
                    menuTab = 0;
                if (GUI.Button(new Rect(X + 14f + tabW + 4f, tabY + 3f, tabW, 32f), "MORE", menuTab == 1 ? tabOnStyle : tabOffStyle))
                    menuTab = 1;

                GUILayout.BeginArea(new Rect(X + 16f, tabY + 46f, W - 32f, H - 126f));

                if (menuTab == 0)
                {
                    GUILayout.Label("COMBAT", sectionStyle);

                    if (GUILayout.Button(Toggles.GodMode
                            ? "<color=#8CFF9E>ON</color>  God Mode"
                            : "God Mode",
                            Toggles.GodMode ? buttonGreen : buttonRed, GUILayout.Height(36f)))
                        Toggles.GodMode = !Toggles.GodMode;

                    if (GUILayout.Button("Full Heal", buttonBlue, GUILayout.Height(32f)))
                        Heal();

                    GUILayout.Label("UTILITY", sectionStyle);
                    if (GUILayout.Button(Toggles.ESP
                            ? "<color=#8CFF9E>ON</color>  ESP (Saiko Position)"
                            : "ESP (Saiko Position)",
                            Toggles.ESP ? buttonGreen : buttonRed, GUILayout.Height(34f)))
                        Toggles.ESP = !Toggles.ESP;

                    if (GUILayout.Button(Toggles.KeyESP
                            ? "<color=#8CFF9E>ON</color>  Key ESP (Find Keys)"
                            : "Key ESP (Find Keys)",
                            Toggles.KeyESP ? buttonGreen : buttonRed, GUILayout.Height(34f)))
                        Toggles.KeyESP = !Toggles.KeyESP;

                    if (GUILayout.Button(Toggles.NoClip
                            ? "<color=#8CFF9E>ON</color>  No Clip (WASD/Space/Ctrl)"
                            : "No Clip (WASD/Space/Ctrl)",
                            Toggles.NoClip ? buttonGreen : buttonRed, GUILayout.Height(34f)))
                        Toggles.NoClip = !Toggles.NoClip;

                    if (GUILayout.Button(Toggles.FreezeAI
                            ? "<color=#8CFF9E>ON</color>  Freeze AI (Saiko statue)"
                            : "Freeze AI (Saiko statue)",
                            Toggles.FreezeAI ? buttonGreen : buttonRed, GUILayout.Height(34f)))
                        Toggles.FreezeAI = !Toggles.FreezeAI;

                    if (GUILayout.Button("Unlock Exit Door (skip keys)", buttonAmber, GUILayout.Height(34f)))
                        UnlockExitDoor();

                    if (GUILayout.Button("Teleport Saiko To You", buttonBlue, GUILayout.Height(34f)))
                        TeleportSaikoToYou();

                    if (GUILayout.Button("Teleport To Saiko", buttonBlue, GUILayout.Height(34f)))
                        TeleportToSaiko();

                    GUILayout.Label("PLAYER", sectionStyle);

                    if (GUILayout.Button(Toggles.SpeedHack
                            ? "<color=#8CFF9E>ON</color>  Speed Hack (x1.8)"
                            : "Speed Hack (x1.8)",
                            Toggles.SpeedHack ? buttonGreen : buttonRed, GUILayout.Height(34f)))
                        Toggles.SpeedHack = !Toggles.SpeedHack;

                    if (GUILayout.Button(Toggles.InfiniteStamina
                            ? "<color=#8CFF9E>ON</color>  Infinite Stamina"
                            : "Infinite Stamina",
                            Toggles.InfiniteStamina ? buttonGreen : buttonRed, GUILayout.Height(34f)))
                        Toggles.InfiniteStamina = !Toggles.InfiniteStamina;

                    GUILayout.Box("", sepStyle, GUILayout.Height(1f));

                    GUILayout.Label("FPS: " + (int)(1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f)), infoStyle, GUILayout.Height(20f));
                    GUILayout.Label("<i>v1.19 by Sinjiratha | Insert: menu | Drag: move</i>", infoStyle, GUILayout.Height(18f));
                }
                else
                {
                    GUILayout.Label("TIME", sectionStyle);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("-", buttonGrey, GUILayout.Width(36f), GUILayout.Height(36f)))
                        timeScale = Mathf.Max(0f, timeScale - 0.1f);
                    GUILayout.Label(string.Format("{0:0.00}", timeScale), valueStyle, GUILayout.Width(100f), GUILayout.Height(36f));
                    if (GUILayout.Button("+", buttonGrey, GUILayout.Width(36f), GUILayout.Height(36f)))
                        timeScale = Mathf.Min(5f, timeScale + 0.1f);
                    GUILayout.EndHorizontal();

                    if (GUILayout.Button(infiniteTime
                            ? "<color=#FFE08A>ON</color>  Infinite Time"
                            : "Infinite Time",
                            infiniteTime ? buttonGreen : buttonAmber, GUILayout.Height(34f)))
                        infiniteTime = !infiniteTime;

                    GUILayout.Box("", sepStyle, GUILayout.Height(1f));

                    GUILayout.Label("FPS: " + (int)(1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f)), infoStyle, GUILayout.Height(20f));
                    GUILayout.Label("<i>v1.19 by Sinjiratha | Insert: menu | Drag: move</i>", infoStyle, GUILayout.Height(18f));
                }

                GUILayout.EndArea();
            }
            catch (Exception e)
            {
                renderFailed = true;
                LoggerInstance.Error("Menu rendering failed: " + e);
            }
        }
    }
}