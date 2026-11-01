using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools; 
using Cairo; 

namespace OreHighlight
{
    // On crée une classe universelle pour stocker soit un Bloc, soit une Entité
    public class ScanTarget 
    {
        public bool IsEntity;
        public Block Block;
        public EntityProperties Entity;
        
        public string CodePath => IsEntity ? Entity.Code.Path : Block.Code.Path;
        public string Domain => IsEntity ? Entity.Code.Domain : Block.Code.Domain;
        public string UniqueId => IsEntity ? "e_" + Math.Abs(CodePath.GetHashCode()) : "b_" + Block.Id;
        
        public string DisplayName 
        {
            get 
            {
                if (!IsEntity) return Lang.Get($"{Domain}:block-{CodePath}");
                
                // Essaye de traduire le nom de l'animal, sinon affiche son code
                string translated = Lang.Get($"{Domain}:item-creature-{CodePath}");
                return translated.Contains("item-creature") ? CodePath : translated;
            }
        }
    }

    public class ScannerGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => "scannergui";

        private OreScanner scanner;
        private OreRenderer renderer;

        private List<ScanTarget> searchResults = new List<ScanTarget>();
        private List<ScanTarget> selectedTargets = new List<ScanTarget>();

        private int currentScanRadius = 100;
        private int currentLabelRadius = 16;
        private string currentSearchText = "";

        public ScannerGui(ICoreClientAPI capi, OreScanner scanner, OreRenderer renderer) : base(capi)
        {
            this.scanner = scanner;
            this.renderer = renderer;
            RebuildDialog();
        }

        private void RebuildDialog()
        {
            if (SingleComposer != null)
            {
                currentScanRadius = (int)SingleComposer.GetNumberInput("chunkInput").GetValue();
                currentLabelRadius = (int)SingleComposer.GetNumberInput("labelRadiusInput").GetValue();
                currentSearchText = SingleComposer.GetTextInput("searchInput").GetText();
                SingleComposer.Dispose();
            }

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            ElementBounds scanRadTextBounds = ElementBounds.Fixed(0, 30, 150, 20);
            ElementBounds labelRadTextBounds = scanRadTextBounds.RightCopy(10);
            ElementBounds scanRadInputBounds = scanRadTextBounds.BelowCopy(0).WithFixedHeight(30);
            ElementBounds labelRadInputBounds = labelRadTextBounds.BelowCopy(0).WithFixedHeight(30);
            ElementBounds savedGroupsBtnBounds = labelRadInputBounds.RightCopy(10).WithFixedWidth(110);

            ElementBounds searchTextBounds = scanRadInputBounds.BelowCopy(10).WithFixedSize(300, 20);
            ElementBounds searchInputBounds = searchTextBounds.BelowCopy(0).WithFixedSize(310, 30);
            ElementBounds searchBtnBounds = searchInputBounds.RightCopy(10).WithFixedWidth(110);

            ElementBounds leftLabelBounds = searchInputBounds.BelowCopy(15).WithFixedSize(240, 20);
            ElementBounds rightLabelBounds = leftLabelBounds.RightCopy(30).WithFixedSize(240, 20);

            ElementBounds leftClipBounds = leftLabelBounds.BelowCopy(5).WithFixedSize(240, 250);
            ElementBounds leftContentBounds = ElementBounds.Fixed(0, 0, 240, 250);
            leftClipBounds.WithChildren(leftContentBounds);
            ElementBounds leftScrollbarBounds = leftClipBounds.RightCopy(5).WithFixedWidth(15);

            ElementBounds rightClipBounds = rightLabelBounds.BelowCopy(5).WithFixedSize(240, 250);
            ElementBounds rightContentBounds = ElementBounds.Fixed(0, 0, 240, 250);
            rightClipBounds.WithChildren(rightContentBounds);
            ElementBounds rightScrollbarBounds = rightClipBounds.RightCopy(5).WithFixedWidth(15);

            ElementBounds scanBtnBounds = ElementBounds.FixedSize(100, 30).FixedUnder(rightClipBounds, 20).WithAlignment(EnumDialogArea.RightFixed);

            bgBounds.WithChildren(
                scanRadTextBounds, labelRadTextBounds, scanRadInputBounds, labelRadInputBounds, savedGroupsBtnBounds,
                searchTextBounds, searchInputBounds, searchBtnBounds,
                leftLabelBounds, rightLabelBounds, leftClipBounds, leftScrollbarBounds, rightClipBounds, rightScrollbarBounds,
                scanBtnBounds
            );

            var composer = capi.Gui.CreateCompo("scannergui", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Select targets to overlay", OnTitleBarClose)
                .AddStaticText("Scan radius (blocks)", CairoFont.WhiteSmallText(), scanRadTextBounds)
                .AddStaticText("Label radius (blocks)", CairoFont.WhiteSmallText(), labelRadTextBounds)
                .AddNumberInput(scanRadInputBounds, OnChunkRadiusChanged, CairoFont.WhiteSmallText(), "chunkInput")
                .AddNumberInput(labelRadInputBounds, (val) => {}, CairoFont.WhiteSmallText(), "labelRadiusInput")
                .AddSmallButton("Saved groups", () => true, savedGroupsBtnBounds)
                .AddStaticText("Search for a block or entity", CairoFont.WhiteSmallText(), searchTextBounds)
                .AddTextInput(searchInputBounds, (text) => {}, CairoFont.WhiteSmallText(), "searchInput")
                .AddSmallButton("Go", OnSearchClicked, searchBtnBounds)
                .AddStaticText($"Results ({searchResults.Count})", CairoFont.WhiteSmallText(), leftLabelBounds)
                .AddStaticText("Active targets", CairoFont.WhiteSmallText(), rightLabelBounds);

            // ==========================================
            // LISTE DE GAUCHE : RÉSULTATS
            // ==========================================
            composer.BeginClip(leftClipBounds);
            double currentYLeft = 0;
            foreach (ScanTarget target in searchResults)
            {
                ElementBounds iconBounds = ElementBounds.Fixed(0, currentYLeft, 30, 30);
                ElementBounds textBounds = ElementBounds.Fixed(35, currentYLeft, 170, 50);
                ElementBounds btnBounds = ElementBounds.Fixed(210, currentYLeft + 2, 25, 25);
                
                leftContentBounds.WithChildren(iconBounds, textBounds, btnBounds);

                var comp = composer;
                if (!target.IsEntity)
                {
                    ItemStack stack = new ItemStack(target.Block);
                    comp = comp.AddInteractiveElement(new GuiElementItemStackPreview(capi, iconBounds, leftClipBounds, stack));
                }
                else 
                {
                    comp = comp.AddDynamicText("🐾", CairoFont.WhiteSmallText(), iconBounds.WithFixedOffset(5, 5), "iconL_" + target.UniqueId);
                }

                comp.AddDynamicText(target.DisplayName, CairoFont.WhiteSmallText(), textBounds, "leftTxt_" + target.UniqueId)
                    .AddSmallButton("+", () => {
                        if (!selectedTargets.Any(t => t.CodePath == target.CodePath)) {
                            selectedTargets.Add(target);
                            RebuildDialog();
                        }
                        return true;
                    }, btnBounds);
                
                currentYLeft += 60;
            }
            leftContentBounds.fixedHeight = Math.Max(250, currentYLeft);
            composer.EndClip();

            composer.AddVerticalScrollbar((value) => {
                leftContentBounds.fixedY = 0 - value;
                leftContentBounds.CalcWorldBounds();
            }, leftScrollbarBounds, "leftScrollbar");

            // ==========================================
            // LISTE DE DROITE : CIBLES ACTIVÉES
            // ==========================================
            composer.BeginClip(rightClipBounds);
            double currentYRight = 0;
            foreach (ScanTarget target in selectedTargets)
            {
                ElementBounds iconBounds = ElementBounds.Fixed(0, currentYRight, 30, 30);
                ElementBounds textBounds = ElementBounds.Fixed(35, currentYRight, 170, 50);
                ElementBounds btnBounds = ElementBounds.Fixed(210, currentYRight + 2, 25, 25);
                
                rightContentBounds.WithChildren(iconBounds, textBounds, btnBounds);

                var comp = composer;
                if (!target.IsEntity)
                {
                    ItemStack stack = new ItemStack(target.Block);
                    comp = comp.AddInteractiveElement(new GuiElementItemStackPreview(capi, iconBounds, rightClipBounds, stack));
                }
                else 
                {
                    comp = comp.AddDynamicText("🐾", CairoFont.WhiteSmallText(), iconBounds.WithFixedOffset(5, 5), "iconR_" + target.UniqueId);
                }
                
                comp.AddDynamicText(target.DisplayName, CairoFont.WhiteSmallText(), textBounds, "rightTxt_" + target.UniqueId)
                    .AddSmallButton("-", () => {
                        selectedTargets.Remove(target);
                        RebuildDialog();
                        return true;
                    }, btnBounds);
                
                currentYRight += 60;
            }
            rightContentBounds.fixedHeight = Math.Max(250, currentYRight);
            composer.EndClip();

            composer.AddVerticalScrollbar((value) => {
                rightContentBounds.fixedY = 0 - value;
                rightContentBounds.CalcWorldBounds();
            }, rightScrollbarBounds, "rightScrollbar");

            composer.AddButton("Scan", OnScanClicked, scanBtnBounds, CairoFont.WhiteMediumText());
            SingleComposer = composer.Compose();

            SingleComposer.GetScrollbar("leftScrollbar").SetHeights((float)leftClipBounds.fixedHeight, (float)leftContentBounds.fixedHeight);
            SingleComposer.GetScrollbar("rightScrollbar").SetHeights((float)rightClipBounds.fixedHeight, (float)rightContentBounds.fixedHeight);

            SingleComposer.GetNumberInput("chunkInput").SetValue(currentScanRadius);
            SingleComposer.GetNumberInput("labelRadiusInput").SetValue(currentLabelRadius);
            SingleComposer.GetTextInput("searchInput").SetValue(currentSearchText);
        }

        private bool OnSearchClicked()
        {
            string query = SingleComposer.GetTextInput("searchInput").GetText().ToLowerInvariant();
            if (string.IsNullOrEmpty(query)) return true;

            searchResults.Clear();
            
            // 1. Recherche parmi les blocs (minerais, silex au sol, cailloux, branches...)
            foreach (Block block in capi.World.Blocks)
            {
                if (block.Code == null || block.Id == 0) continue;
                string name = Lang.Get($"{block.Code.Domain}:block-{block.Code.Path}").ToLowerInvariant();
                
                if (name.Contains(query) || block.Code.Path.Contains(query))
                {
                    searchResults.Add(new ScanTarget { IsEntity = false, Block = block });
                }
            }

            // 2. Recherche parmi les entités (loups, moutons, monstres...)
            foreach (EntityProperties entityType in capi.World.EntityTypes)
            {
                if (entityType.Code == null) continue;
                string path = entityType.Code.Path.ToLowerInvariant();
                string name = Lang.Get($"{entityType.Code.Domain}:item-creature-{entityType.Code.Path}").ToLowerInvariant();
                
                if (name.Contains(query) || path.Contains(query))
                {
                    searchResults.Add(new ScanTarget { IsEntity = true, Entity = entityType });
                }
            }

            RebuildDialog();
            return true;
        }

        private void OnChunkRadiusChanged(string value) { }

        private bool OnScanClicked()
        {
            int radius = (int)SingleComposer.GetNumberInput("chunkInput").GetValue();
            
            if (selectedTargets.Count == 0)
            {
                capi.ShowChatMessage("Sélectionnez au moins une cible !");
                return true;
            }

            capi.ShowChatMessage($"Lancement du scan en arrière-plan (Rayon : {radius} blocs)... Veuillez patienter.");

            OreConfig config = new OreConfig();
            config.ScanRadius = radius;
            config.Targets.Clear(); 

            Random rand = new Random();
            foreach (ScanTarget target in selectedTargets)
            {
                int randomColor = ColorUtil.ToRgba(255, rand.Next(100, 255), rand.Next(100, 255), rand.Next(100, 255));
                
                // On préfixe la clé pour que le scanner sache s'il cherche un bloc ou une entité
                string key = target.IsEntity ? "entity:" + target.CodePath : "block:" + target.CodePath;

                if (!config.Targets.ContainsKey(key)) 
                {
                    config.Targets.Add(key, randomColor);
                }
            }

            renderer.ClearHighlights();

            scanner.ScanBlocksAsync(config, (bPos, bCol, bNames, eEnt, eCol, eNames) => 
            {
                renderer.UpdateHighlights(bPos, bCol, bNames, eEnt, eCol, eNames);
                capi.ShowChatMessage($"Scan terminé ! {bPos.Count} blocs et {eEnt.Count} entités trouvés.");
            });

            TryClose();
            return true;
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }
    }

    public class GuiElementItemStackPreview : GuiElement
    {
        private ItemSlot dummySlot;
        private ElementBounds clipBounds;

        public GuiElementItemStackPreview(ICoreClientAPI capi, ElementBounds bounds, ElementBounds clipBounds, ItemStack stack) : base(capi, bounds)
        {
            this.dummySlot = new DummySlot(stack);
            this.clipBounds = clipBounds;
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (clipBounds != null) api.Render.PushScissor(clipBounds);

            api.Render.RenderItemstackToGui(
                dummySlot, 
                Bounds.renderX + Bounds.InnerWidth / 2, 
                Bounds.renderY + Bounds.InnerHeight / 2, 
                100, 
                (float)Bounds.InnerWidth / 2f, 
                ColorUtil.WhiteArgb
            );

            if (clipBounds != null) api.Render.PopScissor();
        }
    }
}