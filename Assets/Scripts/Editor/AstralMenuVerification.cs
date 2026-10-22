using System;
using System.IO;
using System.Linq;
using AstralAlchemy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AstralMenuVerification
{
    private static bool previousOptionsEnabled, entered;
    private static EnterPlayModeOptions previousOptions;
    private static int phase, frames;
    private static double deadline;
    private static string failure;

    public static void Run()
    {
        previousOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        previousOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        phase = 0; failure = null; deadline = EditorApplication.timeSinceStartup + 100;
        EditorApplication.update += Tick;
        EnterTitle();
    }
    private static void EnterTitle()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/AstralAlchemyTitle.unity", OpenSceneMode.Single);
        frames = 0; entered = false; EditorApplication.isPlaying = true;
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Menu test timed out.");
            if (!EditorApplication.isPlaying)
            {
                if (!entered) return;
                if (phase == 1 && failure == null) { phase = 2; EnterTitle(); return; }
                Finish(); return;
            }
            entered = true;
            if (++frames < 100) return;
            if (phase == 0)
            {
                var menu = UnityEngine.Object.FindFirstObjectByType<TitleMenuController>();
                Require(menu != null, "Title menu missing");
                Require(menu.buttons.Length == 6, "Not all six menu actions are bound");
                foreach (var t in menu.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                    Require(!t.text.Any(c => c >= 0x4e00 && c <= 0x9fff), "Title contains Chinese text");
                Require(!menu.ActivateAtRay(new Ray(Camera.main.transform.position, Vector3.up)), "Blank click activated a menu action");
                Capture("Title_Main");
                Click(menu, TitleMenuButton.Command.Help);
                Require(menu.helpPage.activeSelf && !menu.mainMenu.activeSelf, "Help did not open");
                var hiddenStart = menu.buttons.First(b => b.command == TitleMenuButton.Command.Start);
                Require(menu.ButtonAtRay(RayTo(hiddenStart)) == null, "Hidden START remains clickable");
                Capture("Title_Help");
                Click(menu, TitleMenuButton.Command.Back);
                Require(menu.mainMenu.activeSelf && !menu.helpPage.activeSelf, "Back did not return");
                Click(menu, TitleMenuButton.Command.Quit);
                Require(menu.quitPage.activeSelf, "Quit confirmation missing");
                Capture("Title_Quit");
                Click(menu, TitleMenuButton.Command.CancelQuit);
                Require(menu.mainMenu.activeSelf, "Cancel did not return");
                Click(menu, TitleMenuButton.Command.Quit);
                phase = 1;
                Click(menu, TitleMenuButton.Command.ConfirmQuit);
                Debug.Log("ASTRAL_MENU: ray hits, blank-space rejection, Help/Back, Quit/Cancel and confirmed exit passed.");
                return;
            }
            if (phase == 2)
            {
                var menu = UnityEngine.Object.FindFirstObjectByType<TitleMenuController>();
                Click(menu, TitleMenuButton.Command.Start);
                Require(menu.IsLoading, "Start did not initiate scene loading");
                phase = 3; frames = 0; return;
            }
            if (phase == 3 && SceneManager.GetActiveScene().name == "AstralAlchemyArena")
            {
                var game = UnityEngine.Object.FindFirstObjectByType<ArenaGameController>();
                Require(game != null && game.statusLabel.text.Contains("Tutorial 1/3"), "START did not enter tutorial");
                Capture("HUD_Tutorial");
                var orbs = UnityEngine.Object.FindObjectsByType<ElementOrb>(FindObjectsSortMode.None);
                game.ReceiveElement(orbs.First(o => o.element == ElementKind.Fire && o.IsAvailable));
                game.ReceiveElement(orbs.First(o => o.element == ElementKind.Frost && o.IsAvailable));
                Require(game.IsSpellReady, "Tutorial pair did not prepare a spell");
                Capture("HUD_SelectedElements");
                Debug.Log("ASTRAL_MENU: START loads gameplay and the first pair prepares a spell. ALL MENU TESTS PASSED.");
                phase = 4; EditorApplication.isPlaying = false;
            }
        }
        catch (Exception e)
        {
            failure = e.ToString(); Debug.LogException(e);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false; else Finish();
        }
    }
    private static Ray RayTo(TitleMenuButton button) => new Ray(Camera.main.transform.position, button.transform.position - Camera.main.transform.position);
    private static void Click(TitleMenuController menu, TitleMenuButton.Command command)
    {
        Physics.SyncTransforms();
        var button = menu.buttons.First(b => b.command == command);
        Require(button.gameObject.activeInHierarchy, "Button hidden: " + command);
        Require(menu.ButtonAtRay(RayTo(button)) == button, "Wrong ray target: " + command);
        Require(menu.ActivateAtRay(RayTo(button)), "Button did not activate: " + command);
    }
    private static void Require(bool result, string message) { if (!result) throw new Exception(message); }
    private static void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        Directory.CreateDirectory("Preview/CoverRefinement");
        var camera = Camera.main; var target = new RenderTexture(1600, 1000, 24);
        var old = camera.targetTexture; var oldActive = RenderTexture.active;
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var pixels = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); pixels.Apply();
        File.WriteAllBytes("Preview/CoverRefinement/" + name + ".png", pixels.EncodeToPNG());
        camera.targetTexture = old; RenderTexture.active = oldActive;
        UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(target);
    }
    private static void Finish()
    {
        EditorApplication.update -= Tick;
        EditorSettings.enterPlayModeOptionsEnabled = previousOptionsEnabled;
        EditorSettings.enterPlayModeOptions = previousOptions;
        EditorApplication.Exit(failure == null && phase == 4 ? 0 : 1);
    }
}
