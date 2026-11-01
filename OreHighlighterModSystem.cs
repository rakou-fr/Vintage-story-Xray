using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace OreHighlight
{
    public class OreHighlightMod : ModSystem
    {
        private ICoreClientAPI capi;
        private ScannerGui scannerGui;
        
        private OreScanner scanner;
        private OreRenderer renderer;

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;
            
            this.scanner = new OreScanner(api);
            this.renderer = new OreRenderer(api);
            
            // 1er bouton : Ouvrir l'interface (Touche P)
            capi.Input.RegisterHotKey("scannergui", "Ouvrir l'interface du Scanner", GlKeys.P, HotkeyType.HelpAndOverlays);
            capi.Input.SetHotKeyHandler("scannergui", ToggleGui);

            // 2ème bouton : Activer/Désactiver l'affichage (Touche O par défaut)
            capi.Input.RegisterHotKey("scannertoggle", "Activer/Désactiver l'affichage du Scanner", GlKeys.O, HotkeyType.HelpAndOverlays);
            capi.Input.SetHotKeyHandler("scannertoggle", ToggleScannerRenderer);
        }

        private bool ToggleGui(KeyCombination comb)
        {
            if (scannerGui == null)
            {
                scannerGui = new ScannerGui(capi, scanner, renderer);
            }

            if (scannerGui.IsOpened())
                scannerGui.TryClose();
            else
                scannerGui.TryOpen();

            return true;
        }

        // Nouvelle méthode pour activer/désactiver visuellement le scanner
        private bool ToggleScannerRenderer(KeyCombination comb)
        {
            if (renderer != null)
            {
                renderer.IsActive = !renderer.IsActive; // Inverse l'état (vrai/faux)
                string etat = renderer.IsActive ? "Activé" : "Désactivé";
                capi.ShowChatMessage($"Affichage du scanner : {etat}.");
            }
            return true;
        }
        
        public override void Dispose()
        {
            renderer?.Dispose();
            base.Dispose();
        }
    }
}