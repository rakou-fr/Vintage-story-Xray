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
    // --- MODIFIÉ : La cible contient maintenant une LISTE de blocs ou d'entités (pour les variants) ---
    public class ScanTarget 
    {
        public bool IsEntity;
        public List<Block> Blocks = new List<Block>();
        public List<EntityProperties> Entities = new List<EntityProperties>();
        
        // Nom affiché, défini lors de la recherche
        public string DisplayName { get; set; } 

        // On utilise le premier élément de la liste comme référence
        public string FirstCodePath => IsEntity ? Entities[0].Code.Path : Blocks[0].Code.Path;
        public string Domain => IsEntity ? Entities[0].Code.Domain : Blocks[0].Code.Domain;
        public string UniqueId => IsEntity ? "e_" + Math.Abs(FirstCodePath.GetHashCode()) : "b_" + Blocks[0].Id;
        
        // Récupère tous les objets associés pour faire défiler les images
        public List<ItemStack> GetItemStacks(ICoreClientAPI capi) 
        {
            List<ItemStack> stacks = new List<ItemStack>();
            if (!IsEntity) 
            {
                foreach(var b in Blocks) stacks.Add(new ItemStack(b));
            } 
            else 
            {
                foreach(var e in Entities) 
                {
                    Item item = capi.World.GetItem(new AssetLocation(e.Code.Domain, e.Code.Path)) 
                             ?? capi.World.GetItem(new AssetLocation(e.Code.Domain, "creature-" + e.Code.Path));
                    if (item != null) stacks.Add(new ItemStack(item));
                }
            }
            return stacks;
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
        
        private float leftScrollY = 0f;
        private float rightScrollY = 0f;

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
                if (selectedTargets.Any(t => t.FirstCodePath == target.FirstCodePath)) continue;

                ElementBounds iconBounds = ElementBounds.Fixed(0, currentYLeft, 30, 30);
                ElementBounds textBounds = ElementBounds.Fixed(35, currentYLeft, 170, 50);
                ElementBounds btnBounds = ElementBounds.Fixed(210, currentYLeft + 2, 25, 25);
                
                leftContentBounds.WithChildren(iconBounds, textBounds, btnBounds);

                var comp = composer;
                List<ItemStack> stacks = target.GetItemStacks(capi);
                
                if (stacks.Count > 0)
                {
                    comp = comp.AddInteractiveElement(new GuiElementItemStackPreview(capi, iconBounds, leftClipBounds, stacks));
                }
                else 
                {
                    comp = comp.AddDynamicText("🐾", CairoFont.WhiteSmallText(), iconBounds.WithFixedOffset(5, 5), "iconL_" + target.UniqueId);
                }

                comp.AddDynamicText(target.DisplayName, CairoFont.WhiteSmallText(), textBounds, "leftTxt_" + target.UniqueId)
                    .AddSmallButton("+", () => {
                        if (!selectedTargets.Any(t => t.FirstCodePath == target.FirstCodePath)) {
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
                leftScrollY = value;
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
                List<ItemStack> stacks = target.GetItemStacks(capi);

                if (stacks.Count > 0)
                {
                    comp = comp.AddInteractiveElement(new GuiElementItemStackPreview(capi, iconBounds, rightClipBounds, stacks));
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
                rightScrollY = value; 
                rightContentBounds.CalcWorldBounds();
            }, rightScrollbarBounds, "rightScrollbar");

            composer.AddButton("Scan", OnScanClicked, scanBtnBounds, CairoFont.WhiteMediumText());
            SingleComposer = composer.Compose();

            var leftScrollbar = SingleComposer.GetScrollbar("leftScrollbar");
            leftScrollbar.SetHeights((float)leftClipBounds.fixedHeight, (float)leftContentBounds.fixedHeight);
            leftScrollbar.CurrentYPosition = leftScrollY;
            leftContentBounds.fixedY = 0 - leftScrollY;

            var rightScrollbar = SingleComposer.GetScrollbar("rightScrollbar");
            rightScrollbar.SetHeights((float)rightClipBounds.fixedHeight, (float)rightContentBounds.fixedHeight);
            rightScrollbar.CurrentYPosition = rightScrollY;
            rightContentBounds.fixedY = 0 - rightScrollY;

            SingleComposer.GetNumberInput("chunkInput").SetValue(currentScanRadius);
            SingleComposer.GetNumberInput("labelRadiusInput").SetValue(currentLabelRadius);
            SingleComposer.GetTextInput("searchInput").SetValue(currentSearchText);
        }

        private bool OnSearchClicked()
        {
            string query = SingleComposer.GetTextInput("searchInput").GetText().ToLowerInvariant();
            if (string.IsNullOrEmpty(query)) return true;

            searchResults.Clear();
            
            // On utilise un dictionnaire pour regrouper les objets par leur nom traduit
            Dictionary<string, ScanTarget> groups = new Dictionary<string, ScanTarget>();
            
            // 1. Recherche parmi les blocs
            foreach (Block block in capi.World.Blocks)
            {
                if (block.Code == null || block.Id == 0) continue;
                string name = Lang.Get($"{block.Code.Domain}:block-{block.Code.Path}");
                string lowerName = name.ToLowerInvariant();
                
                if (lowerName.Contains(query) || block.Code.Path.ToLowerInvariant().Contains(query))
                {
                    string groupKey = "b_" + name; // Clé de groupe basée sur le nom exact
                    if (!groups.ContainsKey(groupKey)) 
                    {
                        groups[groupKey] = new ScanTarget { IsEntity = false, DisplayName = name };
                    }
                    groups[groupKey].Blocks.Add(block);
                }
            }

            // 2. Recherche parmi les entités
            foreach (EntityProperties entityType in capi.World.EntityTypes)
            {
                if (entityType.Code == null) continue;
                string path = entityType.Code.Path;
                string name = Lang.Get($"{entityType.Code.Domain}:item-creature-{path}");
                if (name.Contains("item-creature")) name = path; // Secours si pas de traduction

                string lowerName = name.ToLowerInvariant();
                
                if (lowerName.Contains(query) || path.ToLowerInvariant().Contains(query))
                {
                    string groupKey = "e_" + name;
                    if (!groups.ContainsKey(groupKey)) 
                    {
                        groups[groupKey] = new ScanTarget { IsEntity = true, DisplayName = name };
                    }
                    groups[groupKey].Entities.Add(entityType);
                }
            }

            searchResults = groups.Values.ToList();
            leftScrollY = 0f;
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
                // Une couleur par groupe, comme ça tous les variants de silex auront la même couleur !
                int randomColor = ColorUtil.ToRgba(255, rand.Next(100, 255), rand.Next(100, 255), rand.Next(100, 255));
                
                if (!target.IsEntity)
                {
                    foreach (Block b in target.Blocks)
                    {
                        string key = "block:" + b.Code.Path;
                        if (!config.Targets.ContainsKey(key)) config.Targets.Add(key, randomColor);
                    }
                }
                else
                {
                    foreach (EntityProperties e in target.Entities)
                    {
                        string key = "entity:" + e.Code.Path;
                        if (!config.Targets.ContainsKey(key)) config.Targets.Add(key, randomColor);
                    }
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

    // --- MODIFIÉ : Gère maintenant une animation si on lui passe une liste d'objets ---
    public class GuiElementItemStackPreview : GuiElement
    {
        private List<ItemSlot> dummySlots = new List<ItemSlot>();
        private ElementBounds clipBounds;

        public GuiElementItemStackPreview(ICoreClientAPI capi, ElementBounds bounds, ElementBounds clipBounds, List<ItemStack> stacks) : base(capi, bounds)
        {
            this.clipBounds = clipBounds;
            foreach(var stack in stacks) 
            {
                this.dummySlots.Add(new DummySlot(stack));
            }
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (dummySlots.Count == 0) return;

            if (clipBounds != null) api.Render.PushScissor(clipBounds);

            // Change d'image toutes les 800 millisecondes
            int currentIndex = (int)((api.World.ElapsedMilliseconds / 800) % dummySlots.Count);
            ItemSlot slotToRender = dummySlots[currentIndex];

            api.Render.RenderItemstackToGui(
                slotToRender, 
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