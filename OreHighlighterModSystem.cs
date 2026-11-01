using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace OreHighlight
{
    public class OreHighlightMod : ModSystem
    {
        private ICoreClientAPI capi;
        private ScannerGui scannerGui;
        
        // Ajout des instances
        private OreScanner scanner;
        private OreRenderer renderer;

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;
            
            // Initialisation
            this.scanner = new OreScanner(api);
            this.renderer = new OreRenderer(api);
            
            capi.Input.RegisterHotKey("scannergui", "Ouvrir l'interface du Scanner", GlKeys.P, HotkeyType.HelpAndOverlays);
            capi.Input.SetHotKeyHandler("scannergui", ToggleGui);
        }

        private bool ToggleGui(KeyCombination comb)
        {
            if (scannerGui == null)
            {
                // On passe le scanner et le renderer au GUI
                scannerGui = new ScannerGui(capi, scanner, renderer);
            }

            if (scannerGui.IsOpened())
            {
                scannerGui.TryClose();
            }
            else
            {
                scannerGui.TryOpen();
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